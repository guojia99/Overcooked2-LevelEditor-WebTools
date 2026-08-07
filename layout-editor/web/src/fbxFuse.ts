/** 浏览器端 FBX + PNG 材质合并：把 PNG 内嵌进 FBX（Video 节点 Content，base64）。
 *  无 Video 节点时创建 Video 节点并建立 Texture → Video 连接（three 与 Unity 均支持）。
 *  算法经 node 原型 + 字节级回读验证（PNG 数据完全一致）。 */

function decode(buf: Uint8Array, p: number, n: number): string {
  let out = "";
  for (let i = 0; i < n; i++) out += String.fromCharCode(buf[p + i]);
  return out;
}

interface Insert {
  at: number;
  bytes: Uint8Array;
}

function bytesToBase64(bytes: Uint8Array): string {
  let binary = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}

export function fuseTextureIntoFbx(fbxBytes: Uint8Array, pngBytes: Uint8Array): Uint8Array {
  if (fbxBytes.length < 64 || pngBytes.length === 0) return fbxBytes;
  const version = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + 23, 4).getInt32(0, true);
  const wide = version >= 7500;
  const HDR = wide ? 24 : 12;
  const readEnd = (p: number): number =>
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
      case "R": {
        const len = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p, 4).getUint32(0, true);
        return p + 4 + len;
      }
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

  // 解析节点树：收集 endOffset 字段位置、Objects/Connections、Video/Texture 节点
  interface NodeInfo {
    field: number;
    origEnd: number;
    propsEnd: number;
    name: string;
    id?: number;
    children: NodeInfo[];
  }
  const allNodes: NodeInfo[] = [];
  const objects: NodeInfo[] = [];
  const videoNodes: NodeInfo[] = [];
  const textureNodes: NodeInfo[] = [];

  function parseNode(p: number, depth: number): { node: NodeInfo | null } {
    if (p + HDR + 1 > fbxBytes.length) return { node: null };
    let endOffset: number;
    let numProps: number;
    let nameLen = 0;
    let propsEnd: number;
    try {
      endOffset = readEnd(p);
      if (endOffset === 0 || endOffset > fbxBytes.length) return { node: null };
      numProps = readEnd(p + (wide ? 8 : 4));
      let q = p + HDR;
      nameLen = fbxBytes[q];
      q += 1 + nameLen;
      for (let i = 0; i < numProps; i++) q = skipProp(q);
      propsEnd = q;
    } catch {
      return { node: null };
    }

    const name = decode(fbxBytes, p + HDR + 1, nameLen);
    const node: NodeInfo = { field: p, origEnd: endOffset, propsEnd, name, children: [] };
    // id：首属性（'L'/'I'）
    try {
      const t = String.fromCharCode(fbxBytes[p + HDR + 1 + nameLen]);
      if (t === "L") node.id = Number(new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p + HDR + 2 + nameLen, 8).getBigInt64(0, true));
      else if (t === "I") node.id = new DataView(fbxBytes.buffer, fbxBytes.byteOffset + p + HDR + 2 + nameLen, 4).getInt32(0, true);
    } catch {
      /* ignore */
    }
    allNodes.push(node);

    let c = propsEnd;
    let guard = 0;
    while (c < endOffset && guard++ < 4000) {
      const sub = parseNode(c, depth + 1);
      if (!sub.node) {
        c = endOffset;
        break;
      }
      node.children.push(sub.node);
      c = sub.node.origEnd;
    }
    return { node };
  }

  // 顶层循环
  let p = 27;
  let guard = 0;
  while (p + HDR + 1 < fbxBytes.length && guard++ < 200) {
    const top = parseNode(p, 0);
    if (!top.node) break;
    if (top.node.name === "Objects") objects.push(top.node);
    p = top.node.origEnd;
  }
  for (const n of allNodes) {
    if (n.name === "Video" && objects.length > 0 && n.field > objects[0].field) videoNodes.push(n);
    if (n.name === "Texture" && objects.length > 0 && n.field > objects[0].field) textureNodes.push(n);
  }
  const connections = allNodes.find((n) => n.name === "Connections");
  const objectsNode = objects[0];

  const base64 = bytesToBase64(pngBytes);
  const enc = (s: string): Uint8Array => {
    const out = new Uint8Array(s.length);
    for (let i = 0; i < s.length; i++) out[i] = s.charCodeAt(i);
    return out;
  };
  const join = (arr: Uint8Array[]): Uint8Array => {
    const total = arr.reduce((s, a) => s + a.length, 0);
    const out = new Uint8Array(total);
    let w = 0;
    for (const a of arr) {
      out.set(a, w);
      w += a.length;
    }
    return out;
  };
  const sProp = (s: string): Uint8Array => {
    const data = enc(s);
    const out = new Uint8Array(5 + data.length);
    out[0] = 0x53;
    new DataView(out.buffer).setUint32(1, data.length, true);
    out.set(data, 5);
    return out;
  };
  const lProp = (v: number): Uint8Array => {
    const out = new Uint8Array(9);
    out[0] = 0x4c;
    new DataView(out.buffer).setBigInt64(1, BigInt(v), true);
    return out;
  };
  const nodeHead = (name: string, numProps: number, propListLen: number): Uint8Array => {
    const nameB = enc(name);
    const head = new Uint8Array(HDR + 1 + nameB.length);
    const dv = new DataView(head.buffer);
    if (wide) {
      dv.setBigInt64(0, 0n, true);
      dv.setBigInt64(8, BigInt(numProps), true);
      dv.setBigInt64(16, BigInt(propListLen), true);
    } else {
      dv.setUint32(0, 0, true);
      dv.setUint32(4, numProps, true);
      dv.setUint32(8, propListLen, true);
    }
    head[HDR] = nameB.length;
    head.set(nameB, HDR + 1);
    return head;
  };

  const contentChild = join([nodeHead("Content", 1, 5 + base64.length), sProp(base64)]);
  const inserts: Insert[] = [];

  if (videoNodes.length > 0) {
    // 只给第一个 Video 内嵌（three 取 connections.children[0]）；
    // 插入到 Video 的 children 区开头（propsEnd 处）—— 否则会变成 Video 的兄弟节点，
    // 且 endOffset 更新会出现 == 歧义（three 解析错位 → "Unknown property type"）。
    inserts.push({ at: videoNodes[0].propsEnd, bytes: contentChild });
  } else if (textureNodes.length > 0 && objectsNode) {
    // 无 Video：创建 Video 节点 + Texture → Video 连接
    const maxId = allNodes.reduce((m, n) => (n.id !== undefined && n.id > m ? n.id : m), 0);
    const videoId = maxId + 1;
    const fileName = "embedded_texture.png";
    const videoProps = join([lProp(videoId), sProp("Video"), sProp("Clip")]);
    const videoChildren = join([
      join([nodeHead("RelativeFilename", 1, 5 + fileName.length), sProp(fileName)]),
      contentChild,
    ]);
    const videoNode = join([nodeHead("Video", 3, videoProps.length), videoProps, videoChildren]);
    inserts.push({ at: objectsNode.propsEnd, bytes: videoNode });

    if (connections) {
      const connChildren = textureNodes
        .filter((t) => t.id !== undefined)
        .map((t) => join([nodeHead("C", 4, 0), sProp("OO"), lProp(t.id!), lProp(videoId), lProp(0)]));
      if (connChildren.length > 0) {
        inserts.push({ at: connections.propsEnd, bytes: join(connChildren) });
      }
    }
  }

  if (inserts.length === 0) return fbxBytes;
  inserts.sort((a, b) => a.at - b.at);

  // 重建文件
  const total = fbxBytes.length + inserts.reduce((s, i) => s + i.bytes.length, 0);
  const result = new Uint8Array(total);
  let w = 0;
  let cursor = 0;
  for (let i = 0; i <= inserts.length; i++) {
    const at = i < inserts.length ? inserts[i].at : fbxBytes.length;
    result.set(fbxBytes.subarray(cursor, at), w);
    w += at - cursor;
    cursor = at;
    if (i < inserts.length) {
      result.set(inserts[i].bytes, w);
      w += inserts[i].bytes.length;
    }
  }

  // 更新 endOffset：字段新位置 = 原位置 + Σ(插入点 < 原位置的量)；新值 = 原值 + Σ(插入点 < 原值的量)
  // （严格小于：插入点在 children 区开头，原节点 endOffset 均 > 插入点；== 的情况不存在歧义）
  const dv = new DataView(result.buffer, result.byteOffset);
  const shiftAt = (pos: number): number => {
    let d = 0;
    for (const ins of inserts) if (ins.at < pos) d += ins.bytes.length;
    return d;
  };
  const writeEnd = (pos: number, val: number): void => {
    if (wide) dv.setBigInt64(pos, BigInt(val), true);
    else dv.setUint32(pos, val >>> 0, true);
  };
  for (const n of allNodes) {
    const newField = n.field + shiftAt(n.field);
    const newEnd = n.origEnd + shiftAt(n.origEnd);
    writeEnd(newField, newEnd);
  }
  for (const ins of inserts) {
    const newStart = ins.at + shiftAt(ins.at);
    writeEnd(newStart, newStart + ins.bytes.length);
  }

  return result;
}
