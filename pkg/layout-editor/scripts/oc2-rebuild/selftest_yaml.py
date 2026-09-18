"""YAML 解析器自检：对真实场景/预制体做抽样解析并断言关键字段。"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from oc2r import paths, yamlscene as Y


def check(name, cond, detail=""):
    flag = "ok  " if cond else "FAIL"
    print("  [%s] %s %s" % (flag, name, detail))
    return cond


def main():
    ok = True
    p = paths.ar(
        "downloadablecontent/dlc02/dlc_assets/Scenes/01_Beach/s_beach_1_1.unity"
    )
    txt = open(p, encoding="utf-8", errors="replace").read()
    docs = Y.split_docs(txt)
    print("scene docs:", len(docs))
    by_cls = {}
    for d in docs:
        by_cls.setdefault(d.cls, []).append(d)
    ok &= check("有 GameObject", len(by_cls.get(1, [])) > 100, str(len(by_cls.get(1, []))))
    ok &= check("有 Transform", len(by_cls.get(4, [])) > 100, str(len(by_cls.get(4, []))))

    # GameObject: m_Name / m_Component
    go = by_cls[1][0]
    d = go.data
    ok &= check("GameObject.m_Name", isinstance(d.get("m_Name"), str), repr(d.get("m_Name")))
    comp = d.get("m_Component")
    ok &= check("GameObject.m_Component 是 list", isinstance(comp, list), str(type(comp)))
    if isinstance(comp, list) and comp:
        c0 = comp[0]
        ok &= check(
            "component 元素形如 {component: {fileID}}",
            isinstance(c0, dict) and "component" in c0 and Y.ref_fid(c0["component"]) != 0,
            repr(c0),
        )

    # Transform: 位置/旋转/缩放/父子
    tr = by_cls[4][0]
    td = tr.data
    for key in ("m_LocalPosition", "m_LocalRotation", "m_LocalScale", "m_GameObject", "m_Father"):
        ok &= check("Transform.%s" % key, key in td, repr(td.get(key))[:70])
    pos = td.get("m_LocalPosition")
    ok &= check(
        "位置是三元 dict",
        isinstance(pos, dict) and all(k in pos for k in "xyz"),
        repr(pos),
    )
    children = td.get("m_Children")
    ok &= check("m_Children 是 list 或缺省", children is None or isinstance(children, list), repr(children)[:60])

    # MeshRenderer 的 m_Materials
    mr = by_cls.get(23, [])
    if mr:
        md = mr[0].data
        mats = md.get("m_Materials")
        ok &= check("MeshRenderer.m_Materials 是 list", isinstance(mats, list), repr(mats)[:80])
        if isinstance(mats, list) and mats:
            ok &= check("材质引用带 guid", "guid" in mats[0], repr(mats[0]))

    # MonoBehaviour 的 m_Script
    mb = by_cls.get(114, [])
    if mb:
        bd = mb[0].data
        ok &= check("MonoBehaviour.m_Script guid", len(Y.ref_guid(bd.get("m_Script", {}))) == 32, repr(bd.get("m_Script")))

    # RenderSettings
    rs = by_cls.get(104, [])
    if rs:
        rd = rs[0].data
        ok &= check("RenderSettings.m_AmbientSkyColor", isinstance(rd.get("m_AmbientSkyColor"), dict), repr(rd.get("m_AmbientSkyColor")))

    # 预制体
    pp = paths.ar("downloadablecontent/dlc02/dlc_assets/prefabs/shared kitchen/countertop_choppingboard_01.prefab")
    ptxt = open(pp, encoding="utf-8", errors="replace").read()
    pdocs = Y.split_docs(ptxt)
    names = [d.data.get("m_Name") for d in pdocs if d.cls == 1]
    ok &= check("预制体根名", "countertop_choppingboard_01" in names, str(names[:4]))

    print("RESULT:", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
