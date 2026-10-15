using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-time checks for RandomTriggerBakery tag codec（编码/解码对称、
/// 参数 spec 往返、BuildTag 结构）。菜单触发，输出到 Console。</summary>
public static class RandomTriggerBakeryTests
{
    [MenuItem("Layout Editor/测试 (Tests)/运行随机触发器烘焙器测试 (Run RandomTriggerBakery Tests)", false, 600)]
    public static void RunAll()
    {
        var failed = 0;
        failed += TestEscRoundTrip();
        failed += TestParamSpecRoundTrip();
        failed += TestBuildTagStructure();
        Debug.Log(string.Format("[RandomTriggerTests] 完成，失败 {0} 项", failed));
        if (failed > 0)
            throw new Exception(string.Format("[RandomTriggerTests] {0} 项断言失败", failed));
    }

    private static int TestEscRoundTrip()
    {
        var failed = 0;
        var samples = new[]
        {
            "plain", "带中文信号", "a,b;c|d>e/f@g%h", "逗,号分;号竖|线", "100%", "", "rt:RT_ab12",
        };
        foreach (var s in samples)
        {
            var esc = EscRt(s);
            if (esc.IndexOf(',') >= 0 || esc.IndexOf(';') >= 0 || esc.IndexOf('|') >= 0 ||
                esc.IndexOf('/') >= 0 || esc.IndexOf('@') >= 0)
            {
                Debug.LogError("[RandomTriggerTests] EscRt 后仍含结构字符: " + esc);
                failed++;
            }
            var back = UnescRt(esc);
            if (back != s)
            {
                Debug.LogError(string.Format("[RandomTriggerTests] Esc 往返不一致: \"{0}\" → \"{1}\" → \"{2}\"", s, esc, back));
                failed++;
            }
        }
        return failed;
    }

    private static int TestParamSpecRoundTrip()
    {
        var failed = 0;
        var cases = new[]
        {
            new LayoutRandomTriggerParamDto { mode = 1, fixedValue = "Open,分;号|" },
            new LayoutRandomTriggerParamDto { mode = 2, candidates = new[] { "红", "绿/草", "蓝" } },
            new LayoutRandomTriggerParamDto { mode = 3, stringLength = 8, charset = "AB01" },
            new LayoutRandomTriggerParamDto { mode = 3, stringLength = 4, charset = "" },
            new LayoutRandomTriggerParamDto { mode = 4, digits = 3 },
            new LayoutRandomTriggerParamDto { mode = 5, minValue = -2.5f, maxValue = 7.25f, decimals = 2 },
            new LayoutRandomTriggerParamDto { mode = 5, minValue = 1f, maxValue = 99f, decimals = 0 },
        };
        foreach (var p in cases)
        {
            var spec = EncodeParamSpec(p);
            var back = ParseParamSpec(spec);
            if (back == null)
            {
                Debug.LogError("[RandomTriggerTests] spec 解析为 null: " + spec);
                failed++;
                continue;
            }
            if (back.mode != p.mode ||
                (back.fixedValue ?? "") != (p.fixedValue ?? "") ||
                (back.charset ?? "") != (p.charset ?? "") ||
                back.stringLength != (p.stringLength == 0 ? 4 : p.stringLength) ||
                back.digits != Math.Max(1, p.digits) ||
                Mathf.Abs(back.minValue - p.minValue) > 0.0001f ||
                Mathf.Abs(back.maxValue - p.maxValue) > 0.0001f ||
                back.decimals != p.decimals)
            {
                Debug.LogError(string.Format("[RandomTriggerTests] spec 往返不一致 mode={0}: \"{1}\"", p.mode, spec));
                failed++;
                continue;
            }
            if (p.candidates != null)
            {
                if (back.candidates == null || back.candidates.Length != p.candidates.Length)
                {
                    Debug.LogError("[RandomTriggerTests] 候选数量往返不一致: " + spec);
                    failed++;
                }
                else
                {
                    for (int i = 0; i < p.candidates.Length; i++)
                    {
                        if (back.candidates[i] != p.candidates[i])
                        {
                            Debug.LogError("[RandomTriggerTests] 候选值往返不一致: " + spec);
                            failed++;
                            break;
                        }
                    }
                }
            }
        }
        return failed;
    }

    private static int TestBuildTagStructure()
    {
        var failed = 0;
        var entries = new List<object>();
        var entryType = EntryBakedType;
        var paramType = typeof(LayoutRandomTriggerParamDto);
        var e1 = Activator.CreateInstance(entryType);
        entryType.GetField("trigger").SetValue(e1, "Chop");
        entryType.GetField("weight").SetValue(e1, 3.5f);
        entryType.GetField("targetName").SetValue(e1, "断头台,1号");
        entryType.GetField("param").SetValue(e1, new LayoutRandomTriggerParamDto { mode = 1, fixedValue = "快/速" });
        entries.Add(e1);
        var e2 = Activator.CreateInstance(entryType);
        entryType.GetField("trigger").SetValue(e2, "Launch");
        entryType.GetField("weight").SetValue(e2, 1f);
        entryType.GetField("targetName").SetValue(e2, "");
        entryType.GetField("param").SetValue(e2, null);
        entries.Add(e2);

        var tag = (string)BuildTagMethod.Invoke(null, new object[]
        {
            "rt:go|输入", 1.5f, ":", new[] { "Btn_1", "Btn;2" }, CastEntries(entries),
        });
        if (string.IsNullOrEmpty(tag) || !tag.StartsWith("RandomTrigger|", StringComparison.Ordinal))
        {
            Debug.LogError("[RandomTriggerTests] BuildTag 前缀非法: " + tag);
            return failed + 1;
        }
        // 结构切分（与 EntryPoint.ParseRandomTrigger 同款）：'|' → 段，E: → ';' → ','
        var parts = tag.Split('|');
        var iSeg = ""; var eSeg = ""; var nSeg = "";
        foreach (var part in parts)
        {
            if (part.StartsWith("I:")) iSeg = part.Substring(2);
            else if (part.StartsWith("N:")) nSeg = part.Substring(2);
            else if (part.StartsWith("E:")) eSeg = part.Substring(2);
        }
        if (UnescRt(iSeg) != "rt:go|输入")
        {
            Debug.LogError("[RandomTriggerTests] I: 段往返不一致: " + iSeg);
            failed++;
        }
        var nParts = nSeg.Split(',');
        if (nParts.Length != 2 || UnescRt(nParts[1]) != "Btn;2")
        {
            Debug.LogError("[RandomTriggerTests] N: 段往返不一致: " + nSeg);
            failed++;
        }
        var eParts = eSeg.Split(';');
        if (eParts.Length != 2)
        {
            Debug.LogError("[RandomTriggerTests] E: 候选数不符: " + eSeg);
            return failed + 1;
        }
        var f1 = eParts[0].Split(',');
        if (f1.Length != 4 || UnescRt(f1[0]) != "Chop" || f1[1] != "3.5" ||
            UnescRt(f1[2]) != "断头台,1号" || f1[3] != "f" + EscRt("快/速"))
        {
            Debug.LogError("[RandomTriggerTests] E: 候选 1 字段不符: " + eParts[0]);
            failed++;
        }
        var f2 = eParts[1].Split(',');
        if (f2.Length != 3 || UnescRt(f2[0]) != "Launch" || UnescRt(f2[2]) != "")
        {
            Debug.LogError("[RandomTriggerTests] E: 候选 2（无参数应省略第 4 段）不符: " + eParts[1]);
            failed++;
        }
        return failed;
    }

    // ---------------------------------------------------------------- 反射辅助

    private static Type BakeryType
    {
        get { return typeof(RandomTriggerBakery); }
    }

    private static Type EntryBakedType
    {
        get { return BakeryType.GetNestedType("EntryBaked", BindingFlags.NonPublic); }
    }

    private static MethodInfo BuildTagMethod
    {
        get { return BakeryType.GetMethod("BuildTag", BindingFlags.NonPublic | BindingFlags.Static); }
    }

    private static object CastEntries(List<object> list)
    {
        var arr = Array.CreateInstance(EntryBakedType, list.Count);
        for (int i = 0; i < list.Count; i++)
            arr.SetValue(list[i], i);
        var typed = Activator.CreateInstance(typeof(List<>).MakeGenericType(EntryBakedType));
        var add = typed.GetType().GetMethod("Add");
        foreach (var item in list)
            add.Invoke(typed, new[] { item });
        return typed;
    }

    private static string EscRt(string s)
    {
        return (string)BakeryType.GetMethod("EscRt", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { s });
    }

    private static string UnescRt(string s)
    {
        return (string)BakeryType.GetMethod("UnescRt", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { s });
    }

    private static string EncodeParamSpec(LayoutRandomTriggerParamDto p)
    {
        return (string)BakeryType.GetMethod("EncodeParamSpec", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { p });
    }

    private static LayoutRandomTriggerParamDto ParseParamSpec(string spec)
    {
        return (LayoutRandomTriggerParamDto)BakeryType.GetMethod("ParseParamSpec", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { spec });
    }
}
