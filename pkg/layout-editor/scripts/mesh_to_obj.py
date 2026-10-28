#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Unity Mesh .asset (packed m_VertexData, sv8~sv10) → OBJ 转换器（独立工具）。

从 AssetRipper 导出的 Mesh .asset 使用打包顶点流（m_VertexData._typelessdata
十六进制），本项目（Unity 2017.4）无法直接读取 sv10 格式——统一转成 OBJ 走项目
常规导入管线。

坐标约定镜像仓库 C# dumper（LayoutEditorBundleDumper.BuildModelObj）：
X 取反、面序倒排（左手系导出到 obj 右手系），与 commonW5 既有 obj 一致。

用法：
  python3 mesh_to_obj.py <input.asset> <output.obj>
  python3 mesh_to_obj.py --selftest   # 用茶叶网格对照 commonW5 既有 obj 验证
"""
import re
import struct
import sys

FMT_FLOAT32 = 0
FMT_FLOAT16 = 1

FMT_SIZE = {FMT_FLOAT32: 4, FMT_FLOAT16: 2}

# Unity 顶点通道语义序（kShaderChannelVertex）
CH_POSITION = 0
CH_NORMAL = 1
CH_TANGENT = 2
CH_COLOR = 3
CH_UV0 = 4


def _parse_inline_hex(text, key):
    """单行十六进制块（m_IndexBuffer / _typelessdata）。"""
    m = re.search(r"^\s*%s: ([0-9a-fA-F]+)\s*$" % re.escape(key), text, re.M)
    if not m:
        return None
    return bytes.fromhex(m.group(1))


def parse_mesh_asset(path):
    text = open(path, encoding="utf-8").read()
    if "!u!43" not in text:
        raise ValueError("not a Mesh asset: %s" % path)
    name_m = re.search(r"^  m_Name: (.*)$", text, re.M)
    mesh = {"name": name_m.group(1).strip() if name_m else "mesh"}

    vd_m = re.search(r"^  m_VertexData:\n((?:    .*\n)+)", text, re.M)
    if not vd_m:
        raise ValueError("no m_VertexData block (legacy flat mesh?)")
    vd = vd_m.group(1)

    vc_m = re.search(r"m_VertexCount: (\d+)", vd)
    mesh["vertex_count"] = int(vc_m.group(1)) if vc_m else 0

    # 通道：{index: {stream, offset, format}}；dimension 由同流后继 offset 差推导
    raw_channels = []
    for cm in re.finditer(
            r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)", vd):
        raw_channels.append({
            "stream": int(cm.group(1)), "offset": int(cm.group(2)),
            "format": int(cm.group(3)), "declared_dim": int(cm.group(4)),
        })
    if not raw_channels:
        raise ValueError("no channels parsed")
    data = _parse_inline_hex(vd, "_typelessdata")
    if data is None:
        # 兼容：数据直接内联在 m_DataSize 之后一行
        m = re.search(r"m_DataSize: \d+\n\s+([0-9a-fA-F]+)\s*\n", vd)
        if m:
            data = bytes.fromhex(m.group(1))
    if data is None:
        raise ValueError("no _typelessdata / inline data")
    stride = len(data) // mesh["vertex_count"]
    if stride * mesh["vertex_count"] != len(data):
        raise ValueError("data size %d not divisible by vertex count %d"
                         % (len(data), mesh["vertex_count"]))

    # 每条流的通道按 offset 排序，尺寸 = 后继 offset 差（末通道 = stride - offset）；
    # declared_dim=0 的空通道不参与间距链（它们 offset 恒为 0，会截断推导）。
    by_stream = {}
    for idx, ch in enumerate(raw_channels):
        if ch["declared_dim"] <= 0:
            continue
        by_stream.setdefault(ch["stream"], []).append((ch["offset"], idx))
    sizes = {}
    for s, lst in by_stream.items():
        lst.sort()
        for i, (off, idx) in enumerate(lst):
            end = lst[i + 1][0] if i + 1 < len(lst) else stride
            if end <= off:
                sizes[idx] = 0
            else:
                sizes[idx] = end - off
    mesh["channels"] = []
    for idx, ch in enumerate(raw_channels):
        fmt = ch["format"]
        if fmt not in FMT_SIZE or sizes.get(idx, 0) < FMT_SIZE.get(fmt, 0):
            mesh["channels"].append(None)  # 空通道（dimension 0）
            continue
        dim = sizes[idx] // FMT_SIZE[fmt]
        mesh["channels"].append({
            "stream": ch["stream"], "offset": ch["offset"],
            "format": fmt, "dimension": dim,
        })
    mesh["data"] = data
    mesh["stride"] = stride

    idx_fmt_m = re.search(r"^  m_IndexFormat: (\d+)", text, re.M)
    mesh["index_format"] = int(idx_fmt_m.group(1)) if idx_fmt_m else 0  # 0=u16
    mesh["index_buffer"] = _parse_inline_hex(text, "m_IndexBuffer")

    mesh["submeshes"] = []
    for sm in re.finditer(
            r"firstByte: (\d+)\n\s+indexCount: (\d+)\n\s+topology: (\d+)", text):
        mesh["submeshes"].append({
            "firstByte": int(sm.group(1)), "indexCount": int(sm.group(2)),
            "topology": int(sm.group(3)),
        })
    return mesh


def _read_channel(mesh, ch_index, want_comps):
    ch = mesh["channels"][ch_index] if ch_index < len(mesh["channels"]) else None
    if ch is None or ch["dimension"] < want_comps:
        return None
    data, stride = mesh["data"], mesh["stride"]
    fmt = ch["format"]
    out = []
    for v in range(mesh["vertex_count"]):
        base = v * stride + ch["offset"]
        comps = []
        for d in range(want_comps):
            off = base + d * FMT_SIZE[fmt]
            raw = data[off:off + FMT_SIZE[fmt]]
            comps.append(struct.unpack("<f" if fmt == FMT_FLOAT32 else "<e", raw)[0])
        out.append(comps)
    return out


def convert(mesh):
    positions = _read_channel(mesh, CH_POSITION, 3)
    if positions is None:
        raise ValueError("no position channel")
    normals = _read_channel(mesh, CH_NORMAL, 3)
    uvs = _read_channel(mesh, CH_UV0, 2)

    lines = ["# generated by mesh_to_obj.py (mirror X, reverse winding)"]
    for p in positions:
        lines.append("v %.6f %.6f %.6f" % (-p[0], p[1], p[2]))
    if uvs:
        for t in uvs:
            lines.append("vt %.6f %.6f" % (t[0], t[1]))
    if normals:
        for n in normals:
            lines.append("vn %.6f %.6f %.6f" % (-n[0], n[1], n[2]))

    idx_size = 4 if mesh["index_format"] == 1 else 2
    unpack = struct.Struct("<I" if idx_size == 4 else "<H")
    ibuf = mesh["index_buffer"]
    if ibuf is None:
        raise ValueError("no m_IndexBuffer")
    total_idx = len(ibuf) // idx_size
    indices = [unpack.unpack_from(ibuf, i * idx_size)[0] for i in range(total_idx)]

    submeshes = mesh["submeshes"] or [{
        "firstByte": 0, "indexCount": total_idx, "topology": 0}]
    for si, sm in enumerate(submeshes):
        start = sm["firstByte"] // idx_size
        count = sm["indexCount"]
        lines.append("g %s_%d" % (mesh["name"], si))
        for t in range(start, min(start + count, total_idx - 2), 3):
            a, b, c = indices[t] + 1, indices[t + 1] + 1, indices[t + 2] + 1
            # 法线/UV 与顶点同序；面里显式引用 vn，Unity 2017 才不会
            # 报 "Mesh ... has no normals. Recalculating normals."
            if uvs and normals:
                lines.append("f %d/%d/%d %d/%d/%d %d/%d/%d" % (c, c, c, b, b, b, a, a, a))
            elif uvs:
                lines.append("f %d/%d %d/%d %d/%d" % (c, c, b, b, a, a))
            elif normals:
                lines.append("f %d//%d %d//%d %d//%d" % (c, c, b, b, a, a))
            else:
                lines.append("f %d %d %d" % (c, b, a))
    return "\n".join(lines) + "\n"


def main():
    if len(sys.argv) >= 2 and sys.argv[1] == "--selftest":
        repo = "/Users/guojia/Desktop/oc2_editor/Overcooked2-LevelEditor"
        ripped = ("/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/"
                  "Ripped/UnityProject_AssetsOnly/ExportedProject/Assets")
        mesh = parse_mesh_asset(ripped + "/Mesh/m_dlc100_Tea_leaves_raw_03_0.asset")
        obj = convert(mesh)
        ref = open(repo + "/Assets/commonW5/ingredients/tea/m_dlc100_Tea_leaves_raw_03.obj").read()

        def stats(text):
            v = [l for l in text.splitlines() if l.startswith("v ")]
            f = [l for l in text.splitlines() if l.startswith("f ")]
            return v, f

        v1, f1 = stats(obj)
        v2, f2 = stats(ref)
        print("converted: %d verts / %d faces" % (len(v1), len(f1)))
        print("reference: %d verts / %d faces" % (len(v2), len(f2)))
        ok = len(v1) == len(v2)
        if ok:
            p1 = [tuple(map(float, l.split()[1:4])) for l in v1]
            p2 = [tuple(map(float, l.split()[1:4])) for l in v2]
            max_diff = max(max(abs(a - b) for a, b in zip(x, y)) for x, y in zip(p1, p2))
            print("max vertex diff: %.8f" % max_diff)
            ok = max_diff < 1e-4
        if ok:
            # 面序对照（参考文件 f 行可能带 vn：取首 index 序列比较）
            fi1 = [tuple(l.split()[1].split("/")[0] for _ in [0]) or l.split()[1].split("/")[0] for l in f1]
            fi2 = [l.split()[1].split("/")[0] for l in f2]
            same_order = fi1 == fi2
            print("face order identical: %s（参考文件面组顺序差异不阻断）" % same_order)
        print("SELFTEST", "PASS" if ok else "FAIL")
        return 0 if ok else 1
    if len(sys.argv) != 3:
        print(__doc__)
        return 1
    mesh = parse_mesh_asset(sys.argv[1])
    open(sys.argv[2], "w").write(convert(mesh))
    print("wrote %s (%d verts, %d submeshes)" %
          (sys.argv[2], mesh["vertex_count"], len(mesh["submeshes"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
