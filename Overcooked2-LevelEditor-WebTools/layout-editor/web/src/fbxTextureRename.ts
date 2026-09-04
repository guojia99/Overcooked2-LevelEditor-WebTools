/** 浏览器端 FBX 贴图引用改名：上传后 FBX 会被改名为 <recipeId>.fbx、贴图按后端
 *  SanitizeUploadFileName 规则落盘，FBX 内部的 RelativeFilename/FileName 引用名对不上，
 *  Unity 会丢失贴图索引。这里只把引用名改写为贴图落盘后的最终文件名（纯字符串替换 +
 *  endOffset/propListLen 修正），不插入任何节点、不内嵌任何数据。 */

function decode(buf: Uint8Array, p: number, n: number): string {
  let out = "";
  for (let i = 0; i < n; i++) out += String.fromCharCode(buf[p + i]);
  return out;
}

const utf8Decoder = new TextDecoder("utf-8");
const utf8Encoder = new TextEncoder();

/** 与后端 SanitizeUploadFileName 一致的规则：取 basename、扩展名白名单小写、
 *  base 名只保留 Unicode 字母/数字/下划线。 */
export function sanitizeUploadFileName(fileName: string): string {
  if (!fileName) return "";
  const parts = fileName.replace(/\\/g, "/").split("/");
  const name = parts[parts.length - 1].trim();
  if (!name) return "";
  const dot = name.lastIndexOf(".");
  const ext = dot >= 0 ? name.slice(dot).toLowerCase() : "";
  const allowed = [".fbx", ".obj", ".png", ".jpg", ".jpeg", ".tga"];
  if (!allowed.includes(ext)) return "";
  const base = (dot >= 0 ? name.slice(0, dot) : name).replace(/[^\p{L}\p{N}_]/gu, "");
  return base + ext;
}

/** 贴图用途分类（base_color/roughness/metallic/normal/emissive/ao），无关键字时按 base_color。 */
export type TextureClass = "base_color" | "roughness" | "metallic" | "normal" | "emissive" | "ao";

export function classifyTextureName(name: string): TextureClass {
  const n = name.toLowerCase();
  if (n.includes("normal")) return "normal";
  if (n.includes("rough")) return "roughness";
  if (n.includes("metal")) return "metallic";
  if (n.includes("emiss")) return "emissive";
  if (n.includes("occlusion") || /(^|[^a-z])ao([^a-z]|$)/.test(n)) return "ao";
  return "base_color";
}

/** 材质通道（OP 连接的属性名）→ 贴图用途。 */
function channelToClass(prop: string): TextureClass | null {
  const n = prop.toLowerCase();
  if (n === "diffusecolor" || n === "diffusefactor" || n === "basecolor") return "base_color";
  if (n === "normalmap" || n === "bumpmap" || n === "bump") return "normal";
  if (n.includes("shininess") || n.includes("roughness") || n.includes("glossiness")) return "roughness";
  if (n.includes("reflection") || n.includes("specular") || n.includes("metal")) return "metallic";
  if (n.includes("emissive")) return "emissive";
  if (n.includes("ambient")) return "ao";
  return null;
}

/** 类别 → 落盘文件名（如 { base_color: "Egg_base_color.png" }）。 */
export type TextureDiskNames = Partial<Record<TextureClass, string>>;

interface RefEntry {
  /** 字符串 payload 起始位置（'S' 标记 +5）。 */
  dataStart: number;
  oldLen: number;
  value: string;
  /** 文件名之后的保留后缀（Media/Video 名属性为 "\x00\x01Video"，普通路径字段为 ""）。 */
  suffix: string;
  /** "\x00\x01" 前的文件名部分（分类用）。 */
  filePart: string;
  /** 文件名是否带图片扩展名（无扩展名时按所属 Texture/Video 对象的其他字段定类别）。 */
  hasExt: boolean;
  /** 所属 Texture/Video 对象的节点位置（同对象多字段共享类别）。 */
  owner: number;
}

interface NodeInfo {
  /** endOffset 字段位置。 */
  field: number;
  origEnd: number;
  /** propListLen 字段位置。 */
  propField: number;
  origPropListLen: number;
  propsStart: number;
  propsEnd: number;
}

const IMAGE_EXT_RE = /\.(png|jpg|jpeg|tga)$/i;

function basenameOf(p: string): string {
  const parts = p.replace(/\\/g, "/").split("/");
  return parts[parts.length - 1];
}

/** 把 FBX 内贴图引用名按类别改写为落盘文件名。返回改写后的字节与改写处数。 */
export function renameFbxTextureRefs(fbxBytes: Uint8Array, diskNames: TextureDiskNames): { bytes: Uint8Array; renamed: number } {
  const noChange = { bytes: fbxBytes, renamed: 0 };
  const wanted = Object.entries(diskNames).filter(([, v]) => !!v) as [TextureClass, string][];
  if (fbxBytes.length < 64 || wanted.length === 0) return noChange;
  const version = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + 23, 4).getInt32(0, true);
  const wide = version >= 7500;
  const HDR = wide ? 24 : 12;
  const readNum = (p: number): number =>
    wide
      ? Number(new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p, 8).getBigInt64(0, true))
      : new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p, 4).getUint32(0, true);

  function skipProp(p: number): number {
    const type = String.fromCharCode(fbxBytes[p]);
    p += 1;
    switch (type) {
      case "C": return p + 1;
      case "D": return p + 8;
      case "F": return p + 4;
      case "I": return p + 4;
      case "L": return p + 8;
      case "Y": return p + 2;
      case "R":
      case "S": {
        const len = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p, 4).getUint32(0, true);
        return p + 4 + len;
      }
      case "b": case "c": case "d": case "f": case "i": case "l": {
        const count = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p, 4).getUint32(0, true);
        const enc = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p + 4, 4).getUint32(0, true);
        const compLen = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p + 8, 4).getUint32(0, true);
        if (enc === 0) {
          const esz = type === "d" || type === "l" ? 8 : type === "b" || type === "c" ? 1 : 4;
          return p + 12 + count * esz;
        }
        return p + 12 + compLen;
      }
      default:
        throw new Error("fbx prop " + type);
    }
  }

  const allNodes: NodeInfo[] = [];
  const refs: RefEntry[] = [];
  /** Objects 下 Texture/Video 节点：field → 对象 id（Connections 按 id 连接）。 */
  const idByField = new Map<number, number>();
  const videoFields = new Set<number>();
  const textureFields = new Set<number>();
  /** Texture field → TextureName（如 "normalmap_texture"，分类兜底）。 */
  const texNameByField = new Map<number, string>();
  /** Connections 的 C 条目。 */
  const connList: { kind: string; ids: number[]; prop?: string }[] = [];

  function parseNode(p: number, parentName: string, parentField: number, grandName: string, grandField: number): { end: number } | null {
    if (p + HDR + 1 > fbxBytes.length) return null;
    let endOffset: number;
    let numProps: number;
    let nameLen: number;
    let propsStart: number;
    let propsEnd: number;
    try {
      endOffset = readNum(p);
      if (endOffset === 0 || endOffset > fbxBytes.length) return null;
      numProps = readNum(p + (wide ? 8 : 4));
      let q = p + HDR;
      nameLen = fbxBytes[q];
      q += 1 + nameLen;
      propsStart = q;
      for (let i = 0; i < numProps; i++) q = skipProp(q);
      propsEnd = q;
    } catch {
      return null;
    }
    const name = decode(fbxBytes, p + HDR + 1, nameLen);
    allNodes.push({
      field: p,
      origEnd: endOffset,
      propField: p + (wide ? 16 : 8),
      origPropListLen: readNum(p + (wide ? 16 : 8)),
      propsStart,
      propsEnd,
    });

    // 贴图路径/名字字段改写（与标准 FBX 逐字段核对）：
    // 1) Texture>FileName / RelativeFilename（Unity 按 basename 搜贴图）
    // 2) Video>Filename / RelativeFilename（注意有导出器写小写 "Filename"，标准 FBX 亦是如此）
    // 3) Texture>Media：值 = "<文件名>\x00\x01Video"，按名字引用 Video，需与 Video 名同步
    // 4) Objects>Video 节点自身 name 属性（首个 'S' 属性）：值 = "<文件名>\x00\x01Video"
    // 5) Video>Properties70>P["Path"] 的值（第 5 个 'S' 属性，FBX SDK 读取的贴图路径）
    // 改写时保留 "\x00\x01…" 后缀。Connections 按 id 连接，结构不动，仅用于分类。
    const readSProp = (st: number): { len: number; value: string } | null => {
      if (st + 5 > fbxBytes.length || fbxBytes[st] !== 0x53) return null;
      const len = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + st + 1, 4).getUint32(0, true);
      if (len <= 0 || len > 1024 || st + 5 + len > fbxBytes.length) return null;
      return { len, value: utf8Decoder.decode(fbxBytes.subarray(st + 5, st + 5 + len)) };
    };
    const readLProp = (st: number): number | null => {
      if (st + 9 > fbxBytes.length) return null;
      const t = String.fromCharCode(fbxBytes[st]);
      if (t === "L") return Number(new DataView(fbxBytes.buffer, fbxBytes.byteOffset + st + 1, 8).getBigInt64(0, true));
      if (t === "I" && st + 5 <= fbxBytes.length) return new DataView(fbxBytes.buffer, fbxBytes.byteOffset + st + 1, 4).getInt32(0, true);
      return null;
    };
    const pushRef = (st: number, propEnd: number, owner: number, allowNoExt: boolean): void => {
      const sp = readSProp(st);
      if (!sp || st + 5 + sp.len !== propEnd) return;
      const nul = sp.value.indexOf("\x00");
      const filePart = nul >= 0 ? sp.value.slice(0, nul) : sp.value;
      const hasExt = IMAGE_EXT_RE.test(filePart);
      if (!hasExt && !allowNoExt) return;
      refs.push({
        dataStart: st + 5,
        oldLen: sp.len,
        value: sp.value,
        suffix: nul >= 0 ? sp.value.slice(nul) : "",
        filePart,
        hasExt,
        owner,
      });
    };

    const lowerName = name.toLowerCase();
    if ((lowerName === "relativefilename" || lowerName === "filename") &&
        (parentName === "Video" || parentName === "Texture") &&
        numProps >= 1 && propsEnd > propsStart) {
      pushRef(propsStart, propsEnd, parentField, false);
    }
    if (lowerName === "media" && parentName === "Texture" &&
        numProps >= 1 && propsEnd > propsStart) {
      pushRef(propsStart, propsEnd, parentField, true);
    }
    if ((name === "Video" || name === "Texture") && parentName === "Objects") {
      const id = readLProp(propsStart);
      if (id !== null) idByField.set(p, id);
      if (name === "Video") videoFields.add(p);
      else textureFields.add(p);
    }
    if (name === "TextureName" && parentName === "Texture" && numProps >= 1) {
      const sp = readSProp(propsStart);
      if (sp) texNameByField.set(parentField, sp.value.split("\x00")[0]);
    }
    if (name === "C" && parentName === "Connections") {
      let q = propsStart;
      let kind = "";
      let prop: string | undefined;
      const ids: number[] = [];
      for (let i = 0; i < numProps; i++) {
        const st = q;
        const t = String.fromCharCode(fbxBytes[st]);
        q = skipProp(q);
        if (t === "S") {
          const sp = readSProp(st);
          if (sp) {
            if (!kind) kind = sp.value;
            else prop = sp.value;
          }
        } else if (t === "L" || t === "I") {
          const id = readLProp(st);
          if (id !== null) ids.push(id);
        }
      }
      if (kind) connList.push({ kind, ids, prop });
    }
    // Video>Properties70>P["Path"]：FBX SDK 读取的贴图路径（第 5 个 'S' 属性为值）
    if (name === "P" && parentName === "Properties70" && grandName === "Video") {
      let q = propsStart;
      const sProps: { st: number; end: number; value: string }[] = [];
      for (let i = 0; i < numProps; i++) {
        const st = q;
        const t = String.fromCharCode(fbxBytes[st]);
        q = skipProp(q);
        if (t === "S") {
          const sp = readSProp(st);
          if (sp) sProps.push({ st, end: q, value: sp.value });
        }
      }
      if (sProps.length >= 2 && sProps[0].value === "Path") {
        const last = sProps[sProps.length - 1];
        pushRef(last.st, last.end, grandField, false);
      }
    }
    if (name === "Video" && parentName === "Objects") {
      let q = propsStart;
      for (let i = 0; i < numProps; i++) {
        const st = q;
        const t = String.fromCharCode(fbxBytes[st]);
        q = skipProp(q);
        if (t === "S") {
          pushRef(st, q, p, true);
          break;
        }
      }
    }

    let c = propsEnd;
    let guard = 0;
    while (c < endOffset && guard++ < 4000) {
      const sub = parseNode(c, name, p, parentName, parentField);
      if (!sub) break;
      c = sub.end;
    }
    return { end: endOffset };
  }

  let p = 27;
  let guard = 0;
  while (p + HDR + 1 < fbxBytes.length && guard++ < 200) {
    const top = parseNode(p, "", -1, "", -1);
    if (!top) break;
    p = top.end;
  }
  if (refs.length === 0) return noChange;

  // 分类依据（优先级从高到低）：
  // 1) 材质通道：OP Texture->Material 的属性名（DiffuseColor/NormalMap/…），Video 经 OO 连接到 Texture
  // 2) TextureName（如 "normalmap_texture"）
  // 3) 文件名关键字；4) 同对象其他带扩展名字段
  const idToField = new Map<number, number>();
  for (const [f, id] of idByField) idToField.set(id, f);
  const texChannelClass = new Map<number, TextureClass>();
  const videoToTex = new Map<number, number>();
  for (const c of connList) {
    const [a, b] = c.ids;
    const fa = a !== undefined ? idToField.get(a) : undefined;
    const fb = b !== undefined ? idToField.get(b) : undefined;
    if (c.kind === "OP" && c.prop && fa !== undefined && textureFields.has(fa)) {
      const cls = channelToClass(c.prop);
      if (cls && !texChannelClass.has(a)) texChannelClass.set(a, cls);
    }
    if (c.kind === "OO" && fa !== undefined && fb !== undefined &&
        videoFields.has(fa) && textureFields.has(fb)) {
      if (!videoToTex.has(a)) videoToTex.set(a, b);
    }
  }
  const ownerClass = new Map<number, TextureClass>();
  for (const ref of refs) {
    if (ref.hasExt && !ownerClass.has(ref.owner)) {
      ownerClass.set(ref.owner, classifyTextureName(basenameOf(ref.filePart)));
    }
  }
  const classOfRef = (ref: RefEntry): TextureClass | null => {
    const isVideo = videoFields.has(ref.owner);
    const id = idByField.get(ref.owner);
    if (id !== undefined) {
      const texId = isVideo ? videoToTex.get(id) : id;
      if (texId !== undefined) {
        const cls = texChannelClass.get(texId);
        if (cls) return cls;
      }
    }
    if (!isVideo) {
      const tn = texNameByField.get(ref.owner);
      if (tn) return classifyTextureName(tn);
    }
    if (ref.hasExt) return classifyTextureName(basenameOf(ref.filePart));
    return ownerClass.get(ref.owner) ?? null;
  };

  // 每个引用按用途分类 → 该类别的落盘文件名；该类别未上传则保持原样
  interface Edit { dataStart: number; oldLen: number; newBytes: Uint8Array }
  const edits: Edit[] = [];
  for (const ref of refs) {
    const cls = classOfRef(ref);
    if (!cls) continue;
    const diskName = diskNames[cls];
    if (!diskName) continue;
    const newStr = diskName + ref.suffix;
    if (newStr === ref.value) continue;
    const newBytes = utf8Encoder.encode(newStr);
    if (newBytes.length > 0) edits.push({ dataStart: ref.dataStart, oldLen: ref.oldLen, newBytes });
  }
  if (edits.length === 0) return noChange;
  edits.sort((a, b) => a.dataStart - b.dataStart);

  // 重建文件：替换区间为 [dataStart-4, dataStart+oldLen)（int32 长度前缀 + payload）
  const delta = edits.reduce((s, e) => s + e.newBytes.length - e.oldLen, 0);
  const result = new Uint8Array(fbxBytes.length + delta);
  const rdv = new DataView(result.buffer, result.byteOffset);
  let w = 0;
  let cursor = 0;
  for (const e of edits) {
    const lenField = e.dataStart - 4;
    result.set(fbxBytes.subarray(cursor, lenField), w);
    w += lenField - cursor;
    rdv.setUint32(w, e.newBytes.length, true);
    w += 4;
    result.set(e.newBytes, w);
    w += e.newBytes.length;
    cursor = e.dataStart + e.oldLen;
  }
  result.set(fbxBytes.subarray(cursor), w);

  // 修正所有节点的 endOffset 与受影响节点的 propListLen
  const shiftAt = (pos: number): number => {
    let d = 0;
    for (const e of edits) {
      if (e.dataStart + e.oldLen <= pos) d += e.newBytes.length - e.oldLen;
    }
    return d;
  };
  const writeNum = (pos: number, val: number): void => {
    if (wide) rdv.setBigInt64(pos, BigInt(val), true);
    else rdv.setUint32(pos, val >>> 0, true);
  };
  for (const n of allNodes) {
    writeNum(n.field + shiftAt(n.field), n.origEnd + shiftAt(n.origEnd));
    let propDelta = 0;
    for (const e of edits) {
      if (e.dataStart >= n.propsStart && e.dataStart < n.propsEnd) {
        propDelta += e.newBytes.length - e.oldLen;
      }
    }
    if (propDelta !== 0) {
      writeNum(n.propField + shiftAt(n.propField), n.origPropListLen + propDelta);
    }
  }

  return { bytes: result, renamed: edits.length };
}
