/**
 * 把全透明的桌台俯视图（Sprites/Default × 缺顶点色）用已导出的 OBJ/MTL 重栅格化。
 * 只覆盖平均 alpha 接近 0 的 *_top.png，并刷新 manifest.generatedAt 以击穿缓存。
 *
 *   node layout-editor/scripts/rebake-blank-counter-tops.mjs
 */
import fs from "fs";
import path from "path";
import zlib from "zlib";
import { fileURLToPath } from "url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const dir = path.join(repoRoot, "exports/counter-skins");

function readPng(file) {
  const data = fs.readFileSync(file);
  if (data.slice(0, 8).toString("hex") !== "89504e470d0a1a0a") throw new Error("not png " + file);
  let i = 8;
  let ihdr = null;
  const idat = [];
  while (i < data.length) {
    const len = data.readUInt32BE(i);
    const typ = data.slice(i + 4, i + 8).toString("ascii");
    const chunk = data.slice(i + 8, i + 8 + len);
    if (typ === "IHDR") ihdr = chunk;
    else if (typ === "IDAT") idat.push(chunk);
    else if (typ === "IEND") break;
    i += 12 + len;
  }
  const w = ihdr.readUInt32BE(0);
  const h = ihdr.readUInt32BE(4);
  const color = ihdr[9];
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const bpp = color === 6 ? 4 : color === 2 ? 3 : 0;
  if (!bpp) return null;
  const stride = w * bpp;
  const rows = [];
  let prev = Buffer.alloc(stride);
  let p = 0;
  for (let y = 0; y < h; y++) {
    const filter = raw[p++];
    const row = Buffer.from(raw.slice(p, p + stride));
    p += stride;
    if (filter === 1 || filter === 3 || filter === 4) {
      for (let x = 0; x < stride; x++) {
        const left = x >= bpp ? row[x - bpp] : 0;
        const up = prev[x];
        const ul = x >= bpp ? prev[x - bpp] : 0;
        let add = 0;
        if (filter === 1) add = left;
        else if (filter === 2) add = up;
        else if (filter === 3) add = (left + up) >> 1;
        else {
          const pp = left + up - ul;
          const pa = Math.abs(pp - left);
          const pb = Math.abs(pp - up);
          const pc = Math.abs(pp - ul);
          add = pa <= pb && pa <= pc ? left : pb <= pc ? up : ul;
        }
        row[x] = (row[x] + add) & 255;
      }
    } else if (filter === 2) {
      for (let x = 0; x < stride; x++) row[x] = (row[x] + prev[x]) & 255;
    } else if (filter !== 0) {
      return null;
    }
    rows.push(row);
    prev = row;
  }
  return { w, h, bpp, rows };
}

function avgAlpha(png) {
  if (!png) return 255;
  let a = 0;
  let n = 0;
  const step = Math.max(1, Math.floor(png.w / 32));
  for (let y = 0; y < png.h; y += step) {
    const row = png.rows[y];
    for (let x = 0; x < png.w; x += step) {
      a += png.bpp === 4 ? row[x * 4 + 3] : 255;
      n++;
    }
  }
  return a / n;
}

function sample(png, u, v) {
  let x = Math.floor(((u % 1) + 1) % 1 * png.w);
  let y = Math.floor(((v % 1) + 1) % 1 * png.h);
  if (x >= png.w) x = png.w - 1;
  if (y >= png.h) y = png.h - 1;
  const o = x * png.bpp;
  const row = png.rows[y];
  const r = row[o];
  const g = row[o + 1];
  const b = row[o + 2];
  // 部分导出贴图 RGB 有内容但 alpha 全是 0（俯视图因此全透明）。这种当不透明贴图用。
  const a = png.forceOpaque || png.bpp !== 4 ? 255 : row[o + 3];
  return [r, g, b, a];
}

function parseObj(file) {
  const verts = [];
  const uvs = [];
  const faces = [];
  let mat = "";
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    if (line.startsWith("v ")) {
      const p = line.split(/\s+/);
      verts.push([+p[1], +p[2], +p[3]]);
    } else if (line.startsWith("vt ")) {
      const p = line.split(/\s+/);
      uvs.push([+p[1], +p[2]]);
    } else if (line.startsWith("usemtl ")) {
      mat = line.slice(7).trim();
    } else if (line.startsWith("f ")) {
      const parts = line.trim().split(/\s+/).slice(1);
      if (parts.length < 3) continue;
      const idx = parts.map((tok) => {
        const [vi, ti] = tok.split("/");
        return [(+vi || 1) - 1, ti ? (+ti || 1) - 1 : -1];
      });
      for (let i = 1; i < idx.length - 1; i++) faces.push({ mat, tri: [idx[0], idx[i], idx[i + 1]] });
    }
  }
  return { verts, uvs, faces };
}

function parseMtl(file) {
  const mats = {};
  let cur = null;
  for (const line of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    if (line.startsWith("newmtl ")) {
      cur = line.slice(7).trim();
      mats[cur] = { kd: [1, 1, 1], map: "" };
    } else if (!cur) continue;
    else if (line.startsWith("Kd ")) {
      const p = line.split(/\s+/);
      mats[cur].kd = [+p[1], +p[2], +p[3]];
    } else if (line.startsWith("map_Kd ")) {
      mats[cur].map = line.slice(7).trim();
    }
  }
  return mats;
}

function raster(obj, mats, texCache, w, h, flipV) {
  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
  for (const v of obj.verts) {
    if (v[0] < minX) minX = v[0];
    if (v[0] > maxX) maxX = v[0];
    if (v[2] < minZ) minZ = v[2];
    if (v[2] > maxZ) maxZ = v[2];
  }
  const spanX = Math.max(1e-4, maxX - minX);
  const spanZ = Math.max(1e-4, maxZ - minZ);
  const color = new Uint8ClampedArray(w * h * 4);
  const depth = new Float32Array(w * h);
  depth.fill(-1e9);
  const project = (v) => {
    const px = ((v[0] - minX) / spanX) * (w - 1);
    const py = ((v[2] - minZ) / spanZ) * (h - 1);
    return [px, py, v[1]];
  };
  for (const face of obj.faces) {
    const mat = mats[face.mat] || { kd: [1, 1, 1], map: "" };
    const tex = mat.map ? texCache.get(mat.map) : null;
    const pts = face.tri.map(([vi, ti]) => {
      const p = project(obj.verts[vi]);
      const uv = ti >= 0 && obj.uvs[ti] ? obj.uvs[ti] : [0, 0];
      return { x: p[0], y: p[1], z: p[2], u: uv[0], v: flipV ? 1 - uv[1] : uv[1] };
    });
    const [a, b, c] = pts;
    const minPX = Math.max(0, Math.floor(Math.min(a.x, b.x, c.x)));
    const maxPX = Math.min(w - 1, Math.ceil(Math.max(a.x, b.x, c.x)));
    const minPY = Math.max(0, Math.floor(Math.min(a.y, b.y, c.y)));
    const maxPY = Math.min(h - 1, Math.ceil(Math.max(a.y, b.y, c.y)));
    const area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
    if (Math.abs(area) < 1e-8) continue;
    for (let y = minPY; y <= maxPY; y++) {
      for (let x = minPX; x <= maxPX; x++) {
        const w0 = ((b.x - x) * (c.y - y) - (c.x - x) * (b.y - y)) / area;
        const w1 = ((c.x - x) * (a.y - y) - (a.x - x) * (c.y - y)) / area;
        const w2 = 1 - w0 - w1;
        if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
        const z = w0 * a.z + w1 * b.z + w2 * c.z;
        const idx = y * w + x;
        if (z < depth[idx]) continue;
        depth[idx] = z;
        const u = w0 * a.u + w1 * b.u + w2 * c.u;
        const v = w0 * a.v + w1 * b.v + w2 * c.v;
        let r = 200, g = 200, bch = 200, al = 255;
        if (tex) {
          const s = sample(tex, u, v);
          r = s[0]; g = s[1]; bch = s[2]; al = s[3];
        }
        r = Math.min(255, Math.round(r * mat.kd[0]));
        g = Math.min(255, Math.round(g * mat.kd[1]));
        bch = Math.min(255, Math.round(bch * mat.kd[2]));
        const o = idx * 4;
        color[o] = r; color[o + 1] = g; color[o + 2] = bch; color[o + 3] = al;
      }
    }
  }
  return color;
}

function writePng(file, w, h, rgba) {
  const raw = Buffer.alloc((strideOf(w) ) * h);
  const stride = w * 4;
  for (let y = 0; y < h; y++) {
    raw[y * (stride + 1)] = 0;
    Buffer.from(rgba.buffer, rgba.byteOffset + y * stride, stride).copy(raw, y * (stride + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6;
  const chunks = [
    chunk("IHDR", ihdr),
    chunk("IDAT", zlib.deflateSync(raw)),
    chunk("IEND", Buffer.alloc(0)),
  ];
  const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  fs.writeFileSync(file, Buffer.concat([sig, ...chunks]));
}

function strideOf(w) { return w * 4 + 1; }

function chunk(type, data) {
  const t = Buffer.from(type);
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(zlib.crc32(Buffer.concat([t, data])) >>> 0);
  return Buffer.concat([len, t, data, crc]);
}

function coverage(rgba) {
  let n = 0;
  for (let i = 3; i < rgba.length; i += 4) if (rgba[i] > 16) n++;
  return n / (rgba.length / 4);
}

function main() {
  if (!fs.existsSync(dir)) {
    console.error("missing " + dir);
    process.exit(1);
  }
  const tops = fs.readdirSync(dir).filter((n) => n.endsWith("_top.png"));
  const blanks = [];
  for (const name of tops) {
    const png = readPng(path.join(dir, name));
    if (png && avgAlpha(png) < 2) blanks.push(name);
  }
  if (!blanks.length) {
    console.log("no blank tops");
    return;
  }
  // 用一张已经有内容的俯视图校准 UV 的 V 方向。
  const refName = tops.find((n) => {
    const png = readPng(path.join(dir, n));
    return png && avgAlpha(png) > 40 && fs.existsSync(path.join(dir, n.replace(/_top\.png$/, ".obj")));
  });
  let flipV = true;
  if (refName) {
    const refPng = readPng(path.join(dir, refName));
    const key = refName.replace(/_top\.png$/, "");
    const obj = parseObj(path.join(dir, key + ".obj"));
    const mats = parseMtl(path.join(dir, key + ".mtl"));
    const texCache = loadTextures(mats);
    const a = coverage(raster(obj, mats, texCache, 64, 64, false));
    const b = coverage(raster(obj, mats, texCache, 64, 64, true));
    flipV = b >= a;
    console.log(`uv flipV=${flipV} (calibrated on ${key}, coverage ${a.toFixed(2)} vs ${b.toFixed(2)})`);
  }
  let written = 0;
  for (const name of blanks) {
    const key = name.replace(/_top\.png$/, "");
    const objPath = path.join(dir, key + ".obj");
    const mtlPath = path.join(dir, key + ".mtl");
    if (!fs.existsSync(objPath) || !fs.existsSync(mtlPath)) {
      console.log("skip no mesh " + key);
      continue;
    }
    const existing = readPng(path.join(dir, name));
    const w = existing?.w || 256;
    const h = existing?.h || 256;
    const obj = parseObj(objPath);
    const mats = parseMtl(mtlPath);
    const texCache = loadTextures(mats);
    const rgba = raster(obj, mats, texCache, w, h, flipV);
    const cov = coverage(rgba);
    if (cov < 0.02) {
      console.log("still empty " + key);
      continue;
    }
    writePng(path.join(dir, name), w, h, rgba);
    written++;
    console.log(`rewrote ${name} coverage ${(cov * 100).toFixed(0)}%`);
  }
  const manifestPath = path.join(dir, "manifest.json");
  if (fs.existsSync(manifestPath) && written) {
    const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
    manifest.generatedAt = new Date().toISOString();
    fs.writeFileSync(manifestPath, JSON.stringify(manifest));
  }
  console.log(`blank ${blanks.length}, rewritten ${written}`);
}

function loadTextures(mats) {
  const cache = new Map();
  for (const mat of Object.values(mats)) {
    if (!mat.map || cache.has(mat.map)) continue;
    const file = path.join(dir, mat.map);
    if (!fs.existsSync(file)) continue;
    const png = readPng(file);
    if (png) {
      if (avgAlpha(png) < 8) png.forceOpaque = true;
      cache.set(mat.map, png);
    }
  }
  return cache;
}

main();
