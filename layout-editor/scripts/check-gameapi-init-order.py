#!/usr/bin/env python3
"""GameApi 静态字段初始化顺序检查。

背景（2026-09-15 实机事故）：C# 静态字段严格按【文本顺序】初始化。GameApi 里
大量字段的初始化器会调用 Find/Field/Prop 或直接引用其它静态字段——只要在初始化
期读到【声明在自己后面】的静态字段，拿到的就是 null/默认值。轻则该反射目标永久
落空，重则 NRE 冒泡成 TypeInitializationException 把整个 GameApi 永久毒化
（全部 stub 功能连同可移动火锅模型一起消失，日志只剩一句看不出真因的类型初始化失败）。

事故实例：`NamespaceCandidates` 写成带初始化器的静态字段、声明在文件底部，而
文件顶部的 `= Find("GameObjectUtils")` 在初始化期就会走到 FindUncached 里读它
→ null → NRE。注意这是【间接】引用（字段初始化器 → Find → FindUncached → 字段），
所以本脚本必须做调用图闭包，只查直接引用是抓不到的。

两类检查：
  A. 字段初始化器【直接】引用了声明在其后的静态字段；
  B. 初始化期可达的方法（从字段初始化器出发做调用图闭包）引用了任何
     【带初始化器的】静态字段——这类字段的值取决于文本顺序，初始化期不可信。
     修法：改成懒初始化（`s_x == null` 时再建）或常量。
"""
import re
import sys
from pathlib import Path

SRC = Path(__file__).resolve().parents[2] / "Assets/WebCustomStubRuntime/core/GameApi.cs"

DECL = re.compile(
    r'^\s*(?:public|private|internal)\s+static\s+(?:readonly\s+)?[\w\.<>\[\]]+\s+(\w+)\s*='
)
METHOD = re.compile(
    r'^\s*(?:public|private|internal)\s+static\s+[\w\.<>\[\],\s]+?\s'
    r'(\w+)\s*(?:<[^>()]*>)?\s*\([^;]*\)\s*(?:where\s+[^{;]+)?$'
)


def strip_noise(s: str) -> str:
    s = re.sub(r'//[^\n]*', '', s)
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'"(?:[^"\\]|\\.)*"', '""', s)
    return s


def take_block(lines, start):
    """从 start 行起按大括号配平取出方法体，返回 (文本, 结束行号)。"""
    buf = []
    depth = 0
    seen = False
    i = start
    while i < len(lines):
        buf.append(lines[i])
        depth += lines[i].count("{") - lines[i].count("}")
        if "{" in lines[i]:
            seen = True
        if seen and depth <= 0:
            break
        i += 1
    return "\n".join(buf), i


def main() -> int:
    lines = SRC.read_text(encoding="utf-8").split("\n")

    field_order: dict[str, int] = {}     # 带初始化器的静态字段 -> 声明序号
    inits: list[tuple[str, int, str]] = []
    methods: dict[str, str] = {}         # 方法名 -> 方法体

    idx = 0
    i = 0
    while i < len(lines):
        m = DECL.match(lines[i])
        if m:
            name = m.group(1)
            buf = lines[i].split("=", 1)[1]
            depth = buf.count("(") - buf.count(")") + buf.count("{") - buf.count("}")
            j = i
            while (depth > 0 or not buf.rstrip().endswith(";")) and j + 1 < len(lines):
                j += 1
                buf += "\n" + lines[j]
                depth += lines[j].count("(") - lines[j].count(")")
                depth += lines[j].count("{") - lines[j].count("}")
            field_order[name] = idx
            inits.append((name, idx, buf))
            idx += 1
            i = j + 1
            continue
        mm = METHOD.match(lines[i])
        if mm and i + 1 < len(lines) and lines[i + 1].strip().startswith("{"):
            body, end = take_block(lines, i + 1)
            methods[mm.group(1)] = body
            i = end + 1
            continue
        i += 1

    problems: list[str] = []

    # ---- A. 字段初始化器直接引用后置字段 ----
    seed_methods: set[str] = set()
    for name, my_idx, expr in inits:
        body = strip_noise(expr)
        for ref in set(re.findall(r'\b(\w+)\b', body)):
            if ref in methods:
                seed_methods.add(ref)
            if ref == name or ref not in field_order:
                continue
            if field_order[ref] >= my_idx:
                problems.append(
                    "[A] {} 的初始化器引用了声明在其后的 {}（初始化期必为 null）".format(name, ref)
                )

    # ---- B. 初始化期可达方法引用带初始化器的静态字段 ----
    reachable = set(seed_methods)
    frontier = list(seed_methods)
    while frontier:
        cur = frontier.pop()
        for ref in set(re.findall(r'\b(\w+)\b', strip_noise(methods.get(cur, "")))):
            if ref in methods and ref not in reachable:
                reachable.add(ref)
                frontier.append(ref)

    for mname in sorted(reachable):
        for ref in sorted(set(re.findall(r'\b(\w+)\b', strip_noise(methods[mname])))):
            if ref in field_order:
                problems.append(
                    "[B] 初始化期可达方法 {} 引用了带初始化器的静态字段 {}"
                    "（值依赖文本顺序，初始化期不可信；改成懒初始化）".format(mname, ref)
                )

    if problems:
        print("静态字段初始化顺序问题 {} 处:".format(len(set(problems))))
        for p in sorted(set(problems)):
            print("  " + p)
        return 1
    print("静态字段初始化顺序 OK（静态字段 {} 个，初始化期可达方法 {} 个: {}）".format(
        len(inits), len(reachable), ", ".join(sorted(reachable))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
