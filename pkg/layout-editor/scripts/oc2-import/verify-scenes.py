#!/usr/bin/env python3
"""
verify-scenes.py — 生成的 .unity 场景结构校验：
  1. 文档头格式 / fileID 唯一
  2. 本地引用闭环（m_Father / m_Children / m_GameObject / component / m_TransformParent / m_PrefabInternal）
  3. 外部 guid 引用存在（.meta 全库扫描；内置 guid=0000...f000/e000 豁免）
  4. 根节点 m_RootOrder 连续、父子互指一致
用法：python3 verify-scenes.py [scene.unity ...]（默认校验 oc2_dlc_story/scenes 全部）
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)
REF_RE = re.compile(r"\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}), type: \d+)?\}")
GUID_META_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
BUILTIN = {"0000000000000000f000000000000000", "0000000000000000e000000000000000"}


def build_guid_db():
    db = set()
    for base in (os.path.join(ROOT, "Assets"),):
        for dirpath, _d, files in os.walk(base):
            for n in files:
                if not n.endswith(".meta"):
                    continue
                try:
                    m = GUID_META_RE.search(open(os.path.join(dirpath, n),
                                                 "r", encoding="utf-8").read(200))
                except Exception:
                    continue
                if m:
                    db.add(m.group(1))
    return db


def verify(path, guid_db):
    txt = open(path, "r", encoding="utf-8").read()
    errors = []
    docs = {}
    matches = list(DOC_RE.finditer(txt))
    for i, m in enumerate(matches):
        fid = int(m.group(2))
        if fid in docs:
            errors.append(f"fileID 重复: {fid}")
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(txt)
        docs[fid] = (int(m.group(1)), bool(m.group(3)), txt[start:end])
    # 本地引用 + guid
    for fid, (cls, stripped, body) in docs.items():
        for m in REF_RE.finditer(body):
            rid, g = int(m.group(1)), m.group(2)
            if g:
                if g not in BUILTIN and guid_db is not None and g not in guid_db:
                    errors.append(f"doc {fid}: guid 不存在 {g}")
            elif rid > 0 and rid not in docs and cls != 114 or (rid > 0 and rid not in docs):
                # fileID>0 无 guid = 本地引用
                if rid in (100100000,) or rid >= 10 ** 13:
                    continue  # prefab 内部 id（不应出现在无 guid 处，但容错）
                errors.append(f"doc {fid}: 本地引用缺失 {rid}")
    # 父子一致性
    trs = {f: b for f, (c, s, b) in docs.items() if c == 4 and not s}
    for f, body in trs.items():
        fa = re.search(r"m_Father: \{fileID: (\d+)\}", body)
        fa = int(fa.group(1)) if fa else 0
        if fa and fa not in trs:
            errors.append(f"Transform {f}: 父 {fa} 不存在")
        elif fa:
            parent_body = trs[fa]
            ch_section = (parent_body.split("m_Children:")[1].split("m_Father")[0]
                          if "m_Children:" in parent_body else "")
            if f not in [int(x) for x in re.findall(r"- \{fileID: (\d+)\}", ch_section)]:
                errors.append(f"Transform {f}: 父 {fa} 的 m_Children 未包含它")
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        if go and int(go.group(1)) not in docs:
            errors.append(f"Transform {f}: m_GameObject {go.group(1)} 缺失")
    # stripped 引用的 Prefab 实例存在
    for f, (cls, s, body) in docs.items():
        if s:
            pi = re.search(r"m_PrefabInternal: \{fileID: (\d+)\}", body)
            if pi and int(pi.group(1)) not in docs:
                errors.append(f"stripped {f}: PrefabInternal {pi.group(1)} 缺失")
    # 根 RootOrder（含 prefab 实例的 m_RootOrder mod）
    roots = [(int(re.search(r"m_RootOrder: (\d+)", b).group(1)), f)
             for f, b in trs.items()
             if re.search(r"m_Father: \{fileID: 0\}", b)]
    for f, (cls, s, body) in docs.items():
        if cls == 1001 and re.search(r"m_TransformParent: \{fileID: 0\}", body):
            m = re.search(r"propertyPath: m_RootOrder\n\s+value: (\d+)", body)
            if m:
                roots.append((int(m.group(1)), f))
    orders = sorted(o for o, _ in roots)
    if orders != list(range(len(orders))):
        errors.append(f"根 RootOrder 不连续: {orders}")
    return errors, len(docs)


def main():
    guid_db = build_guid_db()
    print(f"guid 库: {len(guid_db)}")
    if len(sys.argv) > 1:
        paths = sys.argv[1:]
    else:
        d = os.path.join(ROOT, "Assets", "LevelSets", "oc2_dlc_story", "scenes")
        paths = sorted(os.path.join(d, n) for n in os.listdir(d) if n.endswith(".unity"))
    bad = 0
    for p in paths:
        errors, ndocs = verify(p, guid_db)
        tag = "OK " if not errors else "ERR"
        print(f"[{tag}] {os.path.basename(p)}: {ndocs} docs, {len(errors)} 问题")
        for e in errors[:10]:
            print("   -", e)
        bad += bool(errors)
    print(f"\n{len(paths) - bad}/{len(paths)} 通过")


if __name__ == "__main__":
    main()
