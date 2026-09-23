/**
 * 自定义关卡 BGM：浏览器端解码 + 压缩重编码。
 *
 * 输入：浏览器可解码的任意音频（mp3 / ogg / wav / m4a / flac ...）。
 * 输出：Unity 2017.4 桌面端可导入的两种格式 ——
 *   - WAV（PCM 16bit，手写 RIFF，零依赖）
 *   - OGG Vorbis（wasm-media-encoders，wasm 内联 base64，离线可用）
 * MP3 不做输出格式：Unity 2017.4 Standalone 无法导入 MP3。
 */

import { createOggEncoder, type WasmMediaEncoder } from "wasm-media-encoders";

/** 压缩预设。estKbps 仅用于编码前估算展示，编码后展示真实大小。 */
export interface BgmPreset {
  key: string;
  label: string;
  format: "wav" | "ogg";
  /** 预设分组（下拉框 optgroup）：ogg 压缩 / wav 无压缩。 */
  group: "ogg" | "wav";
  /** 目标采样率（Hz）。 */
  sampleRate: number;
  mono: boolean;
  /** Vorbis VBR 质量（-1..10，仅 ogg）。 */
  vbrQuality?: number;
  /** 估算码率（kbps），估算用。 */
  estKbps: number;
  /** 是否下拉框默认选中项（仅一个）。 */
  recommended?: boolean;
}

export const BGM_PRESETS: BgmPreset[] = [
  // ---- OGG Vorbis（压缩，推荐用于 BGM） ----
  {
    key: "ogg_max",
    label: "极致 ~256kbps 立体声 44.1kHz（约 1.9MB/分钟，听感近无损）",
    format: "ogg",
    group: "ogg",
    sampleRate: 44100,
    mono: false,
    vbrQuality: 8,
    estKbps: 256,
  },
  {
    key: "ogg_hi",
    label: "高音质 ~192kbps 立体声 44.1kHz（约 1.4MB/分钟）",
    format: "ogg",
    group: "ogg",
    sampleRate: 44100,
    mono: false,
    vbrQuality: 6,
    estKbps: 192,
  },
  {
    key: "ogg_std",
    label: "标准 ~128kbps 立体声 44.1kHz（约 1.0MB/分钟）",
    format: "ogg",
    group: "ogg",
    sampleRate: 44100,
    mono: false,
    vbrQuality: 4,
    estKbps: 128,
    recommended: true,
  },
  {
    key: "ogg_small",
    label: "紧凑 ~96kbps 立体声 32kHz（约 0.7MB/分钟）",
    format: "ogg",
    group: "ogg",
    sampleRate: 32000,
    mono: false,
    vbrQuality: 2,
    estKbps: 96,
  },
  {
    key: "ogg_mini",
    label: "迷你 ~64kbps 立体声 32kHz（约 0.5MB/分钟）",
    format: "ogg",
    group: "ogg",
    sampleRate: 32000,
    mono: false,
    vbrQuality: 0,
    estKbps: 64,
  },
  {
    key: "ogg_tiny",
    label: "超小 ~48kbps 单声道 32kHz（约 0.36MB/分钟）",
    format: "ogg",
    group: "ogg",
    sampleRate: 32000,
    mono: true,
    vbrQuality: 1,
    estKbps: 48,
  },
  {
    key: "ogg_micro",
    label: "极小 ~40kbps 单声道 22.05kHz（约 0.3MB/分钟，语音/8bit 风格可用）",
    format: "ogg",
    group: "ogg",
    sampleRate: 22050,
    mono: true,
    vbrQuality: -1,
    estKbps: 40,
  },
  // ---- WAV（PCM 无压缩，最兼容 / 适合短音效） ----
  {
    key: "wav",
    label: "WAV 16bit 立体声 44.1kHz（约 10MB/分钟）",
    format: "wav",
    group: "wav",
    sampleRate: 44100,
    mono: false,
    estKbps: 1411,
  },
  {
    key: "wav_mono",
    label: "WAV 16bit 单声道 44.1kHz（约 5MB/分钟）",
    format: "wav",
    group: "wav",
    sampleRate: 44100,
    mono: true,
    estKbps: 706,
  },
  {
    key: "wav_22k",
    label: "WAV 16bit 单声道 22.05kHz（约 2.5MB/分钟，复古/语音）",
    format: "wav",
    group: "wav",
    sampleRate: 22050,
    mono: true,
    estKbps: 353,
  },
];

/** 上传/编码限制，与后端 CustomBgmMaxBytes 对齐。 */
export const BGM_MAX_BYTES = 20 * 1024 * 1024;
/** 超过该时长仅警告（BGM 通常 1-3 分钟）。 */
export const BGM_WARN_SECONDS = 8 * 60;

/** 解码上传的音频文件为 AudioBuffer（统一重采样到 44.1kHz）。 */
export async function decodeAudioFile(file: File): Promise<AudioBuffer> {
  const data = await file.arrayBuffer();
  const ctx = new OfflineAudioContext(1, 1, 44100);
  return await ctx.decodeAudioData(data);
}

/** 估算编码后大小（字节）。 */
export function estimateSizeBytes(preset: BgmPreset, durationSec: number): number {
  return Math.round((preset.estKbps * 1000 * durationSec) / 8);
}

/** 按预设重采样/降混后编码，返回 Blob。 */
export async function encodeBgm(
  buffer: AudioBuffer,
  preset: BgmPreset
): Promise<Blob> {
  const channels: 1 | 2 = preset.mono ? 1 : Math.min(2, buffer.numberOfChannels) === 2 ? 2 : 1;
  // 输入已是 44.1k（decodeAudioFile 保证）；采样率相同且声道数一致时跳过重渲染
  const needResample =
    buffer.sampleRate !== preset.sampleRate || buffer.numberOfChannels !== channels;
  let src: AudioBuffer = buffer;
  if (needResample) {
    const frames = Math.ceil((buffer.duration * preset.sampleRate) | 0) || 1;
    const off = new OfflineAudioContext(channels, frames, preset.sampleRate);
    const srcNode = off.createBufferSource();
    srcNode.buffer = buffer;
    srcNode.connect(off.destination);
    srcNode.start();
    src = await off.startRendering();
  }

  if (preset.format === "wav") return encodeWav16(src);
  return encodeOggVorbis(src, preset.vbrQuality ?? 4);
}

// ---------------- WAV (PCM 16-bit) ----------------

function encodeWav16(buffer: AudioBuffer): Blob {
  const numCh = buffer.numberOfChannels;
  const frames = buffer.length;
  const bytesPerSample = 2;
  const dataBytes = frames * numCh * bytesPerSample;
  const out = new ArrayBuffer(44 + dataBytes);
  const view = new DataView(out);

  const writeStr = (off: number, s: string): void => {
    for (let i = 0; i < s.length; i++) view.setUint8(off + i, s.charCodeAt(i));
  };

  writeStr(0, "RIFF");
  view.setUint32(4, 36 + dataBytes, true);
  writeStr(8, "WAVE");
  writeStr(12, "fmt ");
  view.setUint32(16, 16, true); // fmt chunk size
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, numCh, true);
  view.setUint32(24, buffer.sampleRate, true);
  view.setUint32(28, buffer.sampleRate * numCh * bytesPerSample, true); // byte rate
  view.setUint16(32, numCh * bytesPerSample, true); // block align
  view.setUint16(34, 16, true); // bits per sample
  writeStr(36, "data");
  view.setUint32(40, dataBytes, true);

  const chans: Float32Array[] = [];
  for (let c = 0; c < numCh; c++) chans.push(buffer.getChannelData(c));
  let off = 44;
  for (let i = 0; i < frames; i++) {
    for (let c = 0; c < numCh; c++) {
      let s = chans[c][i];
      s = s < -1 ? -1 : s > 1 ? 1 : s;
      view.setInt16(off, s < 0 ? s * 0x8000 : s * 0x7fff, true);
      off += 2;
    }
  }
  return new Blob([out], { type: "audio/wav" });
}

// ---------------- OGG Vorbis ----------------

let _oggEncoder: Promise<WasmMediaEncoder<"audio/ogg">> | null = null;

function getOggEncoder(): Promise<WasmMediaEncoder<"audio/ogg">> {
  if (!_oggEncoder) _oggEncoder = createOggEncoder();
  return _oggEncoder;
}

async function encodeOggVorbis(buffer: AudioBuffer, vbrQuality: number): Promise<Blob> {
  const enc = await getOggEncoder();
  const channels: 1 | 2 = buffer.numberOfChannels === 1 ? 1 : 2;
  enc.configure({
    channels,
    sampleRate: buffer.sampleRate,
    vbrQuality,
  });

  const chans: Float32Array[] = [];
  for (let c = 0; c < channels; c++) chans.push(buffer.getChannelData(c));

  const parts: Uint8Array[] = [];
  const CHUNK = 65536; // 帧数；分块喂入避免单次内存峰值
  for (let start = 0; start < buffer.length; start += CHUNK) {
    const end = Math.min(start + CHUNK, buffer.length);
    const slice = chans.map((ch) => ch.subarray(start, end));
    const outBuf = enc.encode(slice);
    if (outBuf && outBuf.length) parts.push(new Uint8Array(outBuf));
  }
  const tail = enc.finalize();
  if (tail && tail.length) parts.push(new Uint8Array(tail));

  const size = parts.reduce((n, p) => n + p.byteLength, 0);
  const merged = new Uint8Array(size);
  let off = 0;
  for (const p of parts) {
    merged.set(p, off);
    off += p.byteLength;
  }
  return new Blob([merged], { type: "audio/ogg" });
}

// ---------------- 展示辅助 ----------------

export function formatBytes(bytes: number): string {
  if (!isFinite(bytes) || bytes < 0) return "?";
  if (bytes < 1024) return bytes + " B";
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
  return (bytes / (1024 * 1024)).toFixed(2) + " MB";
}

export function formatDuration(sec: number): string {
  if (!isFinite(sec) || sec < 0) return "--:--";
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${String(s).padStart(2, "0")}`;
}
