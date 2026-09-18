"""场景图：把 AssetRipper 导出的扁平 .unity 解析成带世界变换的节点树。

上一版导入器最大的问题是递归下钻时只取叶子的 m_LocalPosition，
把中间分组节点的 T/R/S 整条丢掉（实测 42.7% 物件错位）。
本模块一次性把每个节点的世界 TRS 算出来，后续所有环节只用世界变换。
"""

import math
import os

from . import yamlscene as Y

CLS_GAMEOBJECT = 1
CLS_TRANSFORM = 4
CLS_MESHFILTER = 33
CLS_MESHRENDERER = 23
CLS_SKINNEDMESHRENDERER = 137
CLS_BOXCOLLIDER = 65
CLS_MONOBEHAVIOUR = 114
CLS_LIGHT = 108
CLS_CAMERA = 20
CLS_RENDERSETTINGS = 104
CLS_PARTICLESYSTEM = 198
CLS_RECTTRANSFORM = 224
CLS_ANIMATOR = 95
CLS_ANIMATION = 111
CLS_MESHCOLLIDER = 64
CLS_SPHERECOLLIDER = 135
CLS_CAPSULECOLLIDER = 136

IDENT_Q = (0.0, 0.0, 0.0, 1.0)
ONE3 = (1.0, 1.0, 1.0)
ZERO3 = (0.0, 0.0, 0.0)


# --------------------------------------------------------------------------
# 向量 / 四元数
# --------------------------------------------------------------------------


def v3(d, default=ZERO3):
    if not isinstance(d, dict):
        return default
    return (Y.num(d.get("x"), default[0]), Y.num(d.get("y"), default[1]), Y.num(d.get("z"), default[2]))


def quat(d):
    if not isinstance(d, dict):
        return IDENT_Q
    return (Y.num(d.get("x")), Y.num(d.get("y")), Y.num(d.get("z")), Y.num(d.get("w"), 1.0))


def q_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def q_rot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    # t = 2 * cross(q.xyz, v)
    tx = 2.0 * (y * vz - z * vy)
    ty = 2.0 * (z * vx - x * vz)
    tz = 2.0 * (x * vy - y * vx)
    return (
        vx + w * tx + (y * tz - z * ty),
        vy + w * ty + (z * tx - x * tz),
        vz + w * tz + (x * ty - y * tx),
    )


def q_norm(q):
    x, y, z, w = q
    n = math.sqrt(x * x + y * y + z * z + w * w)
    if n < 1e-12:
        return IDENT_Q
    return (x / n, y / n, z / n, w / n)


def q_to_euler_unity(q):
    """四元数 -> Unity 欧拉角（度，ZXY 应用顺序），与 Inspector 显示一致。"""
    x, y, z, w = q_norm(q)
    # Unity: rotation = Y * X * Z
    sinx = 2.0 * (w * x - y * z)
    sinx = max(-1.0, min(1.0, sinx))
    ex = math.asin(sinx)
    if abs(sinx) > 0.9999999:
        ey = math.atan2(2.0 * (w * y + x * z), 1.0 - 2.0 * (x * x + y * y))
        ez = 0.0
    else:
        ey = math.atan2(2.0 * (w * y + z * x), 1.0 - 2.0 * (x * x + y * y))
        ez = math.atan2(2.0 * (w * z + x * y), 1.0 - 2.0 * (x * x + z * z))
    d = 180.0 / math.pi
    return (_wrap360(ex * d), _wrap360(ey * d), _wrap360(ez * d))


def _wrap360(a):
    a = math.fmod(a, 360.0)
    if a < 0:
        a += 360.0
    if abs(a) < 1e-5 or abs(a - 360.0) < 1e-5:
        return 0.0
    return a


def is_uniform(s, eps=1e-4):
    return abs(s[0] - s[1]) < eps and abs(s[1] - s[2]) < eps


def is_identity_q(q, eps=1e-5):
    return abs(abs(q[3]) - 1.0) < eps


# --------------------------------------------------------------------------
# 3x4 仿射矩阵（行主序，12 个浮点）
# --------------------------------------------------------------------------


def m_from_trs(pos, rot, scale):
    x, y, z, w = rot
    sx, sy, sz = scale
    r00 = 1 - 2 * (y * y + z * z)
    r01 = 2 * (x * y - w * z)
    r02 = 2 * (x * z + w * y)
    r10 = 2 * (x * y + w * z)
    r11 = 1 - 2 * (x * x + z * z)
    r12 = 2 * (y * z - w * x)
    r20 = 2 * (x * z - w * y)
    r21 = 2 * (y * z + w * x)
    r22 = 1 - 2 * (x * x + y * y)
    return (r00 * sx, r01 * sy, r02 * sz, pos[0],
            r10 * sx, r11 * sy, r12 * sz, pos[1],
            r20 * sx, r21 * sy, r22 * sz, pos[2])


M_IDENT = (1.0, 0.0, 0.0, 0.0,
           0.0, 1.0, 0.0, 0.0,
           0.0, 0.0, 1.0, 0.0)


def m_mul(a, b):
    a00, a01, a02, a03, a10, a11, a12, a13, a20, a21, a22, a23 = a
    b00, b01, b02, b03, b10, b11, b12, b13, b20, b21, b22, b23 = b
    return (
        a00 * b00 + a01 * b10 + a02 * b20,
        a00 * b01 + a01 * b11 + a02 * b21,
        a00 * b02 + a01 * b12 + a02 * b22,
        a00 * b03 + a01 * b13 + a02 * b23 + a03,
        a10 * b00 + a11 * b10 + a12 * b20,
        a10 * b01 + a11 * b11 + a12 * b21,
        a10 * b02 + a11 * b12 + a12 * b22,
        a10 * b03 + a11 * b13 + a12 * b23 + a13,
        a20 * b00 + a21 * b10 + a22 * b20,
        a20 * b01 + a21 * b11 + a22 * b21,
        a20 * b02 + a21 * b12 + a22 * b22,
        a20 * b03 + a21 * b13 + a22 * b23 + a23,
    )


def m_decompose(m):
    """4x4 -> (位置, 旋转四元数, 缩放, 是否有剪切)。

    位置永远精确；旋转/缩放在存在剪切时是最佳近似。
    """
    pos = (m[3], m[7], m[11])
    c0 = (m[0], m[4], m[8])
    c1 = (m[1], m[5], m[9])
    c2 = (m[2], m[6], m[10])
    l0 = math.sqrt(c0[0] ** 2 + c0[1] ** 2 + c0[2] ** 2)
    l1 = math.sqrt(c1[0] ** 2 + c1[1] ** 2 + c1[2] ** 2)
    l2 = math.sqrt(c2[0] ** 2 + c2[1] ** 2 + c2[2] ** 2)
    det = (c0[0] * (c1[1] * c2[2] - c1[2] * c2[1])
           - c1[0] * (c0[1] * c2[2] - c0[2] * c2[1])
           + c2[0] * (c0[1] * c1[2] - c0[2] * c1[1]))
    sign = -1.0 if det < 0 else 1.0
    scale = (l0 * sign, l1, l2)
    if l0 < 1e-9 or l1 < 1e-9 or l2 < 1e-9:
        return pos, IDENT_Q, (l0, l1, l2), False
    n0 = (c0[0] / l0 * sign, c0[1] / l0 * sign, c0[2] / l0 * sign)
    n1 = (c1[0] / l1, c1[1] / l1, c1[2] / l1)
    n2 = (c2[0] / l2, c2[1] / l2, c2[2] / l2)
    d01 = n0[0] * n1[0] + n0[1] * n1[1] + n0[2] * n1[2]
    d02 = n0[0] * n2[0] + n0[1] * n2[1] + n0[2] * n2[2]
    d12 = n1[0] * n2[0] + n1[1] * n2[1] + n1[2] * n2[2]
    sheared = max(abs(d01), abs(d02), abs(d12)) > 1e-4
    q = _quat_from_basis(n0, n1, n2)
    return pos, q, scale, sheared


def _quat_from_basis(c0, c1, c2):
    m00, m10, m20 = c0
    m01, m11, m21 = c1
    m02, m12, m22 = c2
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        w = 0.25 * s
        x = (m21 - m12) / s
        y = (m02 - m20) / s
        z = (m10 - m01) / s
    elif m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        w = (m21 - m12) / s
        x = 0.25 * s
        y = (m01 + m10) / s
        z = (m02 + m20) / s
    elif m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        w = (m02 - m20) / s
        x = (m01 + m10) / s
        y = 0.25 * s
        z = (m12 + m21) / s
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2
        w = (m10 - m01) / s
        x = (m02 + m20) / s
        y = (m12 + m21) / s
        z = 0.25 * s
    return q_norm((x, y, z, w))


# --------------------------------------------------------------------------
# 节点
# --------------------------------------------------------------------------


class Node(object):
    __slots__ = (
        "go", "tr", "name", "active", "layer", "tag", "static_flags",
        "parent", "children", "comps",
        "lpos", "lrot", "lscale",
        "wpos", "wrot", "wscale", "skewed", "mat",
        "depth", "sibling_index",
    )

    def __init__(self):
        self.go = 0
        self.tr = 0
        self.name = ""
        self.active = True
        self.layer = 0
        self.tag = ""
        self.static_flags = 0
        self.parent = None
        self.children = []
        self.comps = []          # [(cls, Doc)]
        self.lpos = ZERO3
        self.lrot = IDENT_Q
        self.lscale = ONE3
        self.wpos = ZERO3
        self.wrot = IDENT_Q
        self.wscale = ONE3
        self.skewed = False
        self.mat = M_IDENT
        self.depth = 0
        self.sibling_index = 0

    # -- 便捷访问 ---------------------------------------------------------
    def comp(self, cls):
        for c, d in self.comps:
            if c == cls:
                return d
        return None

    def comps_of(self, cls):
        return [d for c, d in self.comps if c == cls]

    def has(self, cls):
        return any(c == cls for c, _ in self.comps)

    @property
    def path(self):
        parts = []
        n = self
        while n is not None:
            parts.append(n.name)
            n = n.parent
        return "/".join(reversed(parts))

    def walk(self):
        yield self
        for c in self.children:
            for x in c.walk():
                yield x

    def active_in_hierarchy(self):
        n = self
        while n is not None:
            if not n.active:
                return False
            n = n.parent
        return True

    def __repr__(self):
        return "<Node %s>" % self.path


class Scene(object):
    def __init__(self, path):
        self.path = path
        self.name = os.path.basename(path)
        txt = open(path, encoding="utf-8", errors="replace").read()
        self.docs = Y.split_docs(txt)
        self.by_anchor = {}
        for d in self.docs:
            self.by_anchor[d.anchor] = d
        self.nodes = {}       # go fileID -> Node
        self.by_tr = {}       # transform fileID -> Node
        self.roots = []
        self._build()

    # ----------------------------------------------------------------
    def _build(self):
        go_docs = {}
        tr_docs = {}
        for d in self.docs:
            if d.cls == CLS_GAMEOBJECT:
                go_docs[d.anchor] = d
            elif d.cls in (CLS_TRANSFORM, CLS_RECTTRANSFORM):
                tr_docs[d.anchor] = d

        for fid, d in go_docs.items():
            data = d.data
            n = Node()
            n.go = fid
            nm = data.get("m_Name")
            n.name = nm if isinstance(nm, str) else ""
            n.active = str(data.get("m_IsActive", "1")).strip() not in ("0", "False", "false")
            n.layer = int(Y.num(data.get("m_Layer"), 0))
            tg = data.get("m_TagString")
            n.tag = tg if isinstance(tg, str) else ""
            n.static_flags = int(Y.num(data.get("m_StaticEditorFlags"), 0))
            comps = data.get("m_Component")
            if isinstance(comps, list):
                for c in comps:
                    cf = Y.ref_fid(c.get("component")) if isinstance(c, dict) else 0
                    cd = self.by_anchor.get(cf)
                    if cd is not None:
                        n.comps.append((cd.cls, cd))
            self.nodes[fid] = n

        for fid, d in tr_docs.items():
            data = d.data
            gof = Y.ref_fid(data.get("m_GameObject"))
            n = self.nodes.get(gof)
            if n is None:
                continue
            n.tr = fid
            n.lpos = v3(data.get("m_LocalPosition"))
            n.lrot = quat(data.get("m_LocalRotation"))
            n.lscale = v3(data.get("m_LocalScale"), ONE3)
            self.by_tr[fid] = n

        # 父子关系
        for fid, d in tr_docs.items():
            n = self.by_tr.get(fid)
            if n is None:
                continue
            data = d.data
            pf = Y.ref_fid(data.get("m_Father"))
            p = self.by_tr.get(pf)
            if p is not None:
                n.parent = p
            kids = data.get("m_Children")
            if isinstance(kids, list):
                order = []
                for k in kids:
                    kn = self.by_tr.get(Y.ref_fid(k))
                    if kn is not None:
                        order.append(kn)
                n.children = order

        for n in self.by_tr.values():
            if n.parent is None:
                self.roots.append(n)
        # 没被父节点 m_Children 收录的孤儿（AssetRipper 偶发），补进父节点
        for n in self.by_tr.values():
            if n.parent is not None and n not in n.parent.children:
                n.parent.children.append(n)
        self.roots.sort(key=lambda x: x.name)
        self._compute_world()

    def _compute_world(self):
        """世界变换一律走完整 4x4 矩阵链，再分解成 TRS。

        直接用 "父缩放 ⊙ 子位置" 的快捷算法在"父级有旋转+非均匀缩放"时
        会算出错误的位置（实测最大偏 95 米），必须走矩阵。
        """
        stack = [(r, None) for r in self.roots]
        while stack:
            n, p = stack.pop()
            local = m_from_trs(n.lpos, n.lrot, n.lscale)
            if p is None:
                n.mat = local
                n.depth = 0
            else:
                n.mat = m_mul(p.mat, local)
                n.depth = p.depth + 1
            n.wpos, n.wrot, n.wscale, n.skewed = m_decompose(n.mat)
            for i, c in enumerate(n.children):
                c.sibling_index = i
                stack.append((c, n))

    # ----------------------------------------------------------------
    def find_root(self, name):
        for r in self.roots:
            if r.name == name:
                return r
        return None

    def find_path(self, path):
        parts = path.split("/")
        cur = None
        pool = self.roots
        for seg in parts:
            nxt = None
            for c in pool:
                if c.name == seg:
                    nxt = c
                    break
            if nxt is None:
                return None
            cur = nxt
            pool = cur.children
        return cur

    def iter_nodes(self):
        for r in self.roots:
            for n in r.walk():
                yield n

    def __repr__(self):
        return "<Scene %s roots=%d nodes=%d>" % (self.name, len(self.roots), len(self.nodes))
