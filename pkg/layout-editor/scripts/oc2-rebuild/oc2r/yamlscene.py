"""Unity YAML（.unity / .prefab / .asset）轻量解析器。

只覆盖 Unity 序列化实际用到的 YAML 子集：
  - 文档分隔 `--- !u!<classId> &<anchor>[ stripped]`
  - 块映射 / 块序列 / 序列里的映射
  - 行内流式映射 `{x: 1, y: 2}`、流式序列 `[]`
  - 块标量 `|`（按原文保留）

刻意不使用 PyYAML：Unity 的 `!u!` 标签与重复 key 会让通用解析器报错，
而且这里只需要只读取值，自研版本快且可控。
"""

import re

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)


class Doc(object):
    __slots__ = ("cls", "anchor", "stripped", "body", "_data", "_type")

    def __init__(self, cls, anchor, stripped, body):
        self.cls = cls
        self.anchor = anchor
        self.stripped = stripped
        self.body = body
        self._data = None
        self._type = None

    def _ensure(self):
        if self._data is not None:
            return
        raw = parse_block(self.body)
        # 文档体形如 `GameObject:\n  m_Name: ...`，剥掉唯一的类型外壳
        if isinstance(raw, dict) and len(raw) == 1:
            k = next(iter(raw))
            v = raw[k]
            if isinstance(v, dict):
                self._type = k
                self._data = v
                return
        self._type = ""
        self._data = raw if isinstance(raw, dict) else {}

    @property
    def data(self):
        self._ensure()
        return self._data

    @property
    def type_name(self):
        self._ensure()
        return self._type

    def __repr__(self):
        return "<Doc cls=%d anchor=%d>" % (self.cls, self.anchor)


def split_docs(text):
    """切分成 Doc 列表。第一段（文件头 %YAML 等）被丢弃。"""
    out = []
    ms = list(DOC_RE.finditer(text))
    for i, m in enumerate(ms):
        start = m.end()
        end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
        out.append(Doc(int(m.group(1)), int(m.group(2)), bool(m.group(3)), text[start:end]))
    return out


# --------------------------------------------------------------------------
# 标量 / 流式容器
# --------------------------------------------------------------------------

_NUM_RE = re.compile(r"^-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?$")


def _scalar(s):
    s = s.strip()
    if not s:
        return ""
    if s[0] == "'" and s[-1] == "'" and len(s) >= 2:
        return s[1:-1].replace("''", "'")
    if s[0] == '"' and s[-1] == '"' and len(s) >= 2:
        return s[1:-1]
    return s


def num(v, default=0.0):
    """把解析出来的标量转成 float；无法转换时返回 default。"""
    if isinstance(v, (int, float)):
        return float(v)
    if isinstance(v, str) and _NUM_RE.match(v.strip()):
        return float(v)
    return default


def _parse_flow(s, i):
    """从 s[i] 开始解析流式容器，返回 (值, 新下标)。s[i] 必须是 '{' 或 '['。"""
    if s[i] == "{":
        i += 1
        d = {}
        while i < len(s):
            while i < len(s) and s[i] in " ,":
                i += 1
            if i < len(s) and s[i] == "}":
                return d, i + 1
            j = s.find(":", i)
            if j < 0:
                return d, len(s)
            key = s[i:j].strip()
            i = j + 1
            while i < len(s) and s[i] == " ":
                i += 1
            if i < len(s) and s[i] in "{[":
                val, i = _parse_flow(s, i)
            else:
                j = i
                depth = 0
                while j < len(s):
                    c = s[j]
                    if c in "{[":
                        depth += 1
                    elif c in "}]":
                        if depth == 0:
                            break
                        depth -= 1
                    elif c == "," and depth == 0:
                        break
                    j += 1
                val = _scalar(s[i:j])
                i = j
            d[key] = val
        return d, i
    # '['
    i += 1
    arr = []
    while i < len(s):
        while i < len(s) and s[i] in " ,":
            i += 1
        if i < len(s) and s[i] == "]":
            return arr, i + 1
        if s[i] in "{[":
            val, i = _parse_flow(s, i)
            arr.append(val)
            continue
        j = i
        while j < len(s) and s[j] not in ",]":
            j += 1
        arr.append(_scalar(s[i:j]))
        i = j
    return arr, i


def _value(s):
    s = s.strip()
    if not s:
        return None
    if s[0] in "{[":
        v, _ = _parse_flow(s, 0)
        return v
    return _scalar(s)


# --------------------------------------------------------------------------
# 块解析
# --------------------------------------------------------------------------


def _indent(line):
    n = 0
    for c in line:
        if c == " ":
            n += 1
        else:
            break
    return n


def parse_block(text):
    """把一个文档体解析成嵌套 dict/list。

    Unity 的块序列与其所属 key **同缩进**（`m_Component:` 在 2 空格，`- component:` 也在 2 空格），
    这与常见 YAML 写法不同，是本解析器的关键点。
    """
    lines = text.split("\n")
    n = len(lines)
    pos = [0]

    def skip_blank():
        while pos[0] < n and not lines[pos[0]].strip():
            pos[0] += 1

    def block_scalar(min_ind):
        buf = []
        while pos[0] < n:
            raw = lines[pos[0]]
            if raw.strip() and _indent(raw) < min_ind:
                break
            buf.append(raw)
            pos[0] += 1
        return "\n".join(buf)

    def parse_nested(parent_ind):
        """key 后面值为空时，决定后续是块序列、块映射还是空串。"""
        skip_blank()
        if pos[0] >= n:
            return ""
        raw = lines[pos[0]]
        ind = _indent(raw)
        s = raw.strip()
        if (s.startswith("- ") or s == "-") and ind >= parent_ind:
            return parse_seq(ind)
        if ind > parent_ind:
            return parse_map(ind)
        return ""

    def parse_map(base):
        d = {}
        while True:
            skip_blank()
            if pos[0] >= n:
                break
            raw = lines[pos[0]]
            ind = _indent(raw)
            if ind < base:
                break
            s = raw.strip()
            if s.startswith("- ") or s == "-":
                break
            m = _KV_RE.match(s)
            if not m:
                pos[0] += 1
                continue
            key, val = m.group(1), m.group(2)
            pos[0] += 1
            v = val.strip()
            if v in ("|", ">", "|-", ">-"):
                d[key] = block_scalar(ind + 1)
            elif v == "":
                d[key] = parse_nested(ind)
            else:
                d[key] = _value(val)
        return d

    def parse_seq(base):
        arr = []
        while True:
            skip_blank()
            if pos[0] >= n:
                break
            raw = lines[pos[0]]
            if _indent(raw) != base:
                break
            s = raw.strip()
            if not (s.startswith("- ") or s == "-"):
                break
            pos[0] += 1
            if s == "-":
                arr.append(parse_nested(base))
                continue
            item = s[2:]
            t = item.strip()
            if t[:1] in "{[":
                arr.append(_value(item))
                continue
            m = _KV_RE.match(t)
            if m:
                elem_base = base + 2
                key, val = m.group(1), m.group(2)
                d = {}
                vv = val.strip()
                if vv in ("|", ">", "|-", ">-"):
                    d[key] = block_scalar(elem_base + 1)
                elif vv == "":
                    d[key] = parse_nested(elem_base)
                else:
                    d[key] = _value(val)
                rest = parse_map(elem_base)
                for k, v2 in rest.items():
                    d.setdefault(k, v2)
                arr.append(d)
            else:
                arr.append(_scalar(item))
        return arr

    skip_blank()
    if pos[0] >= n:
        return {}
    first = lines[pos[0]]
    if first.strip().startswith("- "):
        return parse_seq(_indent(first))
    return parse_map(_indent(first))


_KV_RE = re.compile(r"^([A-Za-z_][\w\s\-\.]*?|\"[^\"]*\"|'[^']*'):(.*)$")


def is_ref(v):
    return isinstance(v, dict) and "fileID" in v


def ref_fid(v):
    if isinstance(v, dict) and "fileID" in v:
        try:
            return int(v["fileID"])
        except (TypeError, ValueError):
            return 0
    return 0


def ref_guid(v):
    if isinstance(v, dict):
        return v.get("guid") or ""
    return ""
