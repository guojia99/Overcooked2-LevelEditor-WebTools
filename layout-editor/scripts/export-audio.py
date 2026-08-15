#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
export-audio.py — 从游戏 AssetBundle 提取音频为浏览器可播放文件（Vorbis -> .ogg，ADPCM -> .wav），
供 web 编辑器「音频配置」中的试听使用。

用法:
    python3 export-audio.py <request.json>

request.json 由 Unity 侧 LayoutEditorAudioExporter 生成，结构:
{
  "exportRoot":  "<abs>",                    # 输出目录（bgm/ sfx/ 子目录）
  "bundlesDir":  "<abs>",                    # StreamingAssets/Windows
  "music":  [ {"bundle","container","guid","id","nameZh","filename"} ],
  "dirs":   [ {"bundle","container","dirId","nameZh",
               "clips":[{"kind":"oneshot|looping","entry":N,"part":"base|start|end",
                         "tag","type","filename"}]} ],
  "ambiences": [ {"tag","filename","dirId"} ]
}

输出:
    按 filename 写到 exportRoot 下（Vorbis 写 .ogg，ADPCM 写 .wav）；
    exportRoot/audio-export-result.json 记录每个请求文件的 ok/失败原因与实际文件名。

依赖（首次运行自动在当前目录创建 .venv-audio 并安装）:
    UnityPy  fsb5   （fsb5 需要系统 libvorbis/libogg：macOS 执行 brew install libvorbis）
"""
import ctypes.util
import json
import os
import re
import struct
import subprocess
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
VENV_DIR = os.path.join(SCRIPT_DIR, ".venv-audio")


def ensure_deps():
    """自动引导 venv 并安装依赖；缺失系统库时给出提示。"""
    try:
        import UnityPy  # noqa: F401
        import fsb5  # noqa: F401
    except ImportError:
        if sys.prefix and VENV_DIR.startswith(sys.prefix):
            print("ERROR: deps missing inside venv; run: %s -m pip install UnityPy fsb5" % sys.executable, file=sys.stderr)
            sys.exit(2)
        print("bootstrap: creating venv + installing UnityPy, fsb5 ...", file=sys.stderr)
        try:
            subprocess.check_call([sys.executable, "-m", "venv", VENV_DIR])
            py = os.path.join(VENV_DIR, "bin", "python")
            subprocess.check_call([py, "-m", "pip", "install", "--quiet", "--upgrade", "pip"])
            subprocess.check_call([py, "-m", "pip", "install", "--quiet", "UnityPy", "fsb5"])
        except Exception as e:
            print("ERROR: bootstrap failed: %s" % e, file=sys.stderr)
            sys.exit(2)
        os.execv(py, [py, os.path.abspath(__file__)] + sys.argv[1:])
    if ctypes.util.find_library("vorbis") is None:
        print("ERROR: 缺少 libvorbis（fsb5 解码依赖）。请先安装：brew install libvorbis", file=sys.stderr)
        sys.exit(3)


ensure_deps()

import UnityPy  # noqa: E402
from UnityPy.helpers.ResourceReader import get_resource_data  # noqa: E402
from fsb5 import FSB5, SoundFormat  # noqa: E402

# ---------------------------------------------------------------------------
# IMA ADPCM decode (FSB5 mode 7)
# ---------------------------------------------------------------------------
IMA_STEP = [7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130,
            143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449,
            494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
            1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026,
            4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442,
            11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
            27086, 29794, 32767]
IMA_INDEX = [-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8]


def decode_ima_adpcm(data, channels):
    """FSB5 IMAADPCM：每声道 4 字节头 [int16 predictor][int16 stepIndex]，之后
    半字节流按声道交错（byte 低半字节 = ch0）。返回交织的 16bit PCM 列表。"""
    preds, steps = [], []
    pos = 0
    for _ in range(channels):
        pred, step = struct.unpack_from("<hh", data, pos)
        pos += 4
        preds.append(pred)
        steps.append(step)
    out = []
    for f in range((len(data) - pos) * 2):
        byte = data[pos + (f // 2)]
        nib = (byte >> ((f % 2) * 4)) & 0xF
        c = f % channels
        pred = preds[c]
        step_i = steps[c]
        step = IMA_STEP[step_i]
        diff = step >> 3
        if nib & 4:
            diff += step
        if nib & 2:
            diff += step >> 1
        if nib & 1:
            diff += step >> 2
        pred = pred - diff if (nib & 8) else pred + diff
        if pred > 32767:
            pred = 32767
        elif pred < -32768:
            pred = -32768
        preds[c] = pred
        steps[c] = max(0, min(88, step_i + IMA_INDEX[nib & 7]))
        out.append(pred)
    return out


def build_wav(pcm, channels, rate):
    frames = len(pcm) // channels
    data = struct.pack("<%dh" % len(pcm), *pcm)
    byterate = rate * channels * 2
    hdr = struct.pack("<4sI4s4sIHHIIHH4sI",
                      b"RIFF", 36 + len(data), b"WAVE", b"fmt ", 16, 1,
                      channels, rate, byterate, channels * 2, 16,
                      b"data", len(data))
    return hdr + data


# ---------------------------------------------------------------------------
# Bundle / cab resolution
# ---------------------------------------------------------------------------
_bundle_paths = []          # all bundle file paths (sorted)
_envs = {}                  # bundle path -> Environment
_cab_map = {}               # cab name (lower) -> bundle path


def list_bundles(bundles_dir):
    global _bundle_paths
    _bundle_paths = sorted(
        os.path.join(bundles_dir, n)
        for n in os.listdir(bundles_dir)
        if not n.startswith(".") and not n.endswith(".meta")
    )


def load_bundle(path):
    env = _envs.get(path)
    if env is not None:
        return env
    env = UnityPy.load(path)
    _envs[path] = env
    for f in env.files.values():
        for name in f.files:
            _cab_map.setdefault(name.lower(), path)
    return env


def scan_for_cab(cab):
    """按需加载 bundle 直到 cab 被找到。"""
    cab = cab.lower()
    if cab in _cab_map:
        return _cab_map[cab]
    for p in _bundle_paths:
        if p in _envs:
            continue
        load_bundle(p)
        if cab in _cab_map:
            return _cab_map[cab]
    return None


def get_serialized_file(bundle_path, cab):
    env = _envs[bundle_path]
    for f in env.files.values():
        for name, sf in f.files.items():
            if name.lower() == cab.lower():
                return sf
    return None


def resolve_pptr(assets_file, m_file_id, m_path_id):
    """Unity PPtr -> ObjectReader（支持跨 bundle 的外部引用）。"""
    if m_file_id == 0:
        return assets_file.files.get(m_path_id)
    idx = m_file_id - 1
    if idx < 0 or idx >= len(assets_file.externals):
        return None
    ext = str(assets_file.externals[idx])
    m = re.search(r"archive:/([^/]+)/", ext)
    if not m:
        return None
    cab = m.group(1)
    bundle_path = scan_for_cab(cab)
    if bundle_path is None:
        return None
    sf = get_serialized_file(bundle_path, cab)
    if sf is None:
        return None
    return sf.files.get(m_path_id)


def find_clip_by_container(bundles_dir, bundle, container):
    """按 container 路径找 AudioClip（先查指定 bundle，再查已加载的其它 bundle）。"""
    target = (container or "").lower().replace("\\", "/")
    env = load_bundle(os.path.join(bundles_dir, bundle))
    for obj in env.objects:
        if obj.type.name == "AudioClip" and (obj.container or "").lower() == target:
            return obj
    for p, env in _envs.items():
        for obj in env.objects:
            if obj.type.name == "AudioClip" and (obj.container or "").lower() == target:
                return obj
    return None


# ---------------------------------------------------------------------------
# Extraction
# ---------------------------------------------------------------------------
def extract_clip(clip_obj, key):
    clip = clip_obj.read()
    res = clip.m_Resource
    if res is None or res.m_Size == 0:
        raise ValueError("clip 无音频资源: %s" % key)
    data = get_resource_data(res.m_Source, clip_obj.assets_file, res.m_Offset, res.m_Size)
    if not data.startswith(b"FSB5"):
        raise ValueError("非 FSB5 音频资源: %s" % key)
    fsb = FSB5(data)
    idx = getattr(clip, "m_SubsoundIndex", 0)
    if not 0 <= idx < len(fsb.samples):
        idx = 0
    sample = fsb.samples[idx]
    mode = fsb.header.mode
    if mode == SoundFormat.VORBIS:
        return "ogg", fsb.rebuild_sample(sample)
    if mode == SoundFormat.IMAADPCM:
        pcm = decode_ima_adpcm(sample.data, sample.channels)
        frames = min(sample.samples, len(pcm) // sample.channels)
        pcm = pcm[: frames * sample.channels]
        return "wav", build_wav(pcm, sample.channels, sample.frequency)
    raise NotImplementedError("unsupported FSB format: %s" % mode)


def resolve_dir_clip(bundles_dir, item):
    """从 AudioDirectoryData 的 typetree 解析 entry 指向的 AudioClip 对象。"""
    env = load_bundle(os.path.join(bundles_dir, item["bundle"]))
    dir_obj = None
    target = (item["container"] or "").lower().replace("\\", "/")
    for obj in env.objects:
        if obj.type.name == "MonoBehaviour" and (obj.container or "").lower() == target:
            dir_obj = obj
            break
    if dir_obj is None:
        raise FileNotFoundError("AudioDirectoryData 未找到: %s" % item["container"])
    tree = dir_obj.read_typetree()
    kind = item["kind"]
    entry_idx = item["entry"]
    arrays = tree.get("OneShotAudio") if kind == "oneshot" else tree.get("LoopingAudio")
    if not arrays or entry_idx >= len(arrays):
        raise ValueError("entry 越界: %s[%d]" % (kind, entry_idx))
    entry = arrays[entry_idx]
    part = item.get("part") or ""
    field = "AudioFile"
    if kind == "looping" and part == "start":
        field = "StartClip"
    elif kind == "looping" and part == "end":
        field = "EndClip"
    ref = entry.get(field) or {}
    if "m_FileID" in ref:
        m_file_id = ref["m_FileID"]
        m_path_id = ref["m_PathID"]
    elif "fileID" in ref:
        m_file_id = 0
        m_path_id = ref["fileID"]
    else:
        raise ValueError("%s 无 %s 引用" % (kind, field))
    obj = resolve_pptr(dir_obj.assets_file, m_file_id, m_path_id)
    if obj is None:
        raise FileNotFoundError("clip 对象未找到: %s[%d].%s" % (kind, entry_idx, field))
    return obj


def main():
    if len(sys.argv) < 2:
        print("usage: python3 export-audio.py <request.json>", file=sys.stderr)
        sys.exit(1)
    with open(sys.argv[1], "r", encoding="utf-8") as f:
        req = json.load(f)
    export_root = req["exportRoot"]
    bundles_dir = req["bundlesDir"]
    list_bundles(bundles_dir)
    os.makedirs(os.path.join(export_root, "bgm"), exist_ok=True)
    os.makedirs(os.path.join(export_root, "sfx"), exist_ok=True)

    entries = []
    for m in req.get("music", []):
        entries.append((m["filename"], ("music", m)))
    for d in req.get("dirs", []):
        for c in d.get("clips", []):
            item = dict(d)
            item.update(c)
            entries.append((c["filename"], ("dir", item)))

    results = {}
    ok = 0
    fail = 0
    total = len(entries)
    for i, (key, loader) in enumerate(entries, 1):
        try:
            kind, item = loader
            if kind == "music":
                clip_obj = find_clip_by_container(bundles_dir, item["bundle"], item["container"])
                if clip_obj is None:
                    raise FileNotFoundError("AudioClip 未找到: %s" % item["container"])
            else:
                clip_obj = resolve_dir_clip(bundles_dir, item)
            ext, payload = extract_clip(clip_obj, key)
            actual = os.path.splitext(key)[0] + "." + ext
            out_path = os.path.join(export_root, actual)
            os.makedirs(os.path.dirname(out_path), exist_ok=True)
            with open(out_path, "wb") as f:
                f.write(payload)
            results[key] = {"ok": True, "actual": actual}
            ok += 1
            print("[%d/%d] OK  %s" % (i, total, actual))
        except Exception as e:
            results[key] = {"ok": False, "error": str(e)}
            fail += 1
            print("[%d/%d] FAIL %s : %s" % (i, total, key, e), file=sys.stderr)

    result_path = os.path.join(export_root, "audio-export-result.json")
    file_list = [
        {"key": k, "ok": v.get("ok", False), "actual": v.get("actual", ""), "error": v.get("error", "")}
        for k, v in results.items()
    ]
    with open(result_path, "w", encoding="utf-8") as f:
        json.dump({"ok": ok, "fail": fail, "files": file_list}, f, ensure_ascii=False)
    print("done: ok=%d fail=%d" % (ok, fail))
    sys.exit(0 if fail == 0 else 1)


if __name__ == "__main__":
    main()
