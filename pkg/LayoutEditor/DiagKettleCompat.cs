using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 热水器 prefab 粒子组件 2017.4 兼容修复（一次性工具，C#4 兼容）。
///
/// 背景：web_kettle_hotwater.prefab 的 ParticleSystem(198)×2 / ParticleSystemRenderer(199)×2
/// 是按 Unity 2019+ 格式生成的（ringBufferMode / m_RayTracingMode / m_RenderingLayerMask /
/// m_RendererPriority / m_ReceiveGI / useUnscaledTime 等字段），而本工程是 Unity 2017.4.8f1，
/// 反序列化时报 "Unexpected node type."，导致粒子组件与场景实例降级、热水器不可见。
///
/// 做法：本脚本让 Unity 2017.4 自己生成一份规范的粒子序列化模板（临时 prefab），
/// 再把模板文档替换进热水器 prefab 的 4 个粒子 anchor（保留原 anchor/fileID、
/// m_GameObject 引用、PSR 材质列表），最后强制重导入并实例化验证渲染器状态。
/// 结果写 logs/kettle_compat_result.txt；原 prefab 备份在 logs/backup/。
/// </summary>
public static class DiagKettleCompat
{
    private const string KettlePath = "Assets/commonW5/prefabs/kettle/web_kettle_hotwater.prefab";
    private const string TempPrefabPath = "Assets/commonW5/prefabs/kettle/_psref_tmp.prefab";
    private const string ResultPath = "logs/kettle_compat_result.txt";

    private static StringBuilder sb;

    [MenuItem("Layout Editor/诊断/应用热水器粒子 2017 兼容修复")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static void Run()
    {
        sb = new StringBuilder();
        try
        {
            DoRun();
        }
        catch (Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
            Debug.LogError("[COMPAT] exception: " + e);
        }
        try
        {
            Directory.CreateDirectory("logs");
            File.WriteAllText(ResultPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("[COMPAT] write result failed: " + e.Message);
        }
        Debug.Log("[COMPAT] done -> " + ResultPath);
    }

    private static void DoRun()
    {
        // ---------- 1. 生成 2017.4 规范粒子模板 ----------
        GameObject tmp = new GameObject("_psref_tmp");
        tmp.AddComponent<ParticleSystem>();
        if (File.Exists(TempPrefabPath))
            AssetDatabase.DeleteAsset(TempPrefabPath);
#pragma warning disable 618
        PrefabUtility.CreatePrefab(TempPrefabPath, tmp);
#pragma warning restore 618
        UnityEngine.Object.DestroyImmediate(tmp);
        AssetDatabase.ImportAsset(TempPrefabPath, ImportAssetOptions.ForceUpdate);
        string refText = File.ReadAllText(TempPrefabPath).Replace("\r\n", "\n");
        File.WriteAllText("logs/ps_ref_2017_full.prefab.txt", refText);
        string ref198 = ExtractBody(refText, "198");
        string ref199 = ExtractBody(refText, "199");
        sb.AppendLine("template: ref198=" + (ref198 == null ? 0 : ref198.Length)
            + "B ref199=" + (ref199 == null ? 0 : ref199.Length) + "B");
        AssetDatabase.DeleteAsset(TempPrefabPath);
        if (string.IsNullOrEmpty(ref198) || string.IsNullOrEmpty(ref199))
        {
            sb.AppendLine("ERROR: 2017 粒子模板生成失败（198/199 文档缺失）");
            return;
        }

        // ---------- 2. 替换热水壶 prefab 的 4 个粒子文档 ----------
        string text = File.ReadAllText(KettlePath).Replace("\r\n", "\n");
        Directory.CreateDirectory("logs/backup");
        File.WriteAllText("logs/backup/web_kettle_hotwater.prefab.before-compat.txt", text);

        string[][] targets = new string[][]
        {
            new string[] { "198", "1910000029" },   // PFX_Steam ParticleSystem
            new string[] { "199", "1910000030" },   // PFX_Steam ParticleSystemRenderer
            new string[] { "198", "1910000037" },   // PFX_Drip  ParticleSystem
            new string[] { "199", "1910000038" },   // PFX_Drip  ParticleSystemRenderer
        };

        foreach (string[] target in targets)
        {
            string cls = target[0];
            string anchor = target[1];
            string oldBody = ExtractBodyByAnchor(text, cls, anchor);
            if (oldBody == null)
            {
                sb.AppendLine("ERROR: 旧文档缺失 !u!" + cls + " &" + anchor);
                return;
            }
            string refBody = cls == "198" ? ref198 : ref199;
            string newBody = BuildPatchedBody(refBody, oldBody, cls);
            string newBlock = "--- !u!" + cls + " &" + anchor + "\n" + newBody;
            text = ReplaceBlockByAnchor(text, cls, anchor, newBlock);
            sb.AppendLine("replaced !u!" + cls + " &" + anchor
                + " old=" + oldBody.Length + "B new=" + newBody.Length + "B");
        }

        File.WriteAllText(KettlePath, text);
        AssetDatabase.ImportAsset(KettlePath, ImportAssetOptions.ForceUpdate);
        sb.AppendLine("prefab written & reimported");

        // ---------- 3. 验证：实例化并输出渲染器状态 ----------
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePath);
        if (prefab == null)
        {
            sb.AppendLine("ERROR: prefab load == null");
            return;
        }
        GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (inst == null)
        {
            sb.AppendLine("ERROR: instantiate == null");
            return;
        }
        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
        sb.AppendLine("renderers=" + renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            string meshInfo = "-";
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                meshInfo = "verts=" + mf.sharedMesh.vertexCount + " sub=" + mf.sharedMesh.subMeshCount;
            Material mat = r.sharedMaterial;
            string matInfo = mat == null
                ? "<null>"
                : (mat.name + " shader=" + (mat.shader == null ? "<null>" : mat.shader.name));
            sb.AppendLine("  " + r.GetType().Name + " go=" + r.gameObject.name
                + " enabled=" + r.enabled
                + " activeInHierarchy=" + r.gameObject.activeInHierarchy
                + " mat=" + matInfo + " " + meshInfo);
        }
        UnityEngine.Object.DestroyImmediate(inst);
    }

    // ---------- 文本工具 ----------

    /// <summary>从完整文本按 class 提取首个文档体（分隔行之后到下一个 --- 之前）。</summary>
    private static string ExtractBody(string text, string cls)
    {
        string marker = "--- !u!" + cls + " &";
        int s = text.IndexOf(marker, StringComparison.Ordinal);
        if (s < 0) return null;
        int lineEnd = text.IndexOf('\n', s);
        if (lineEnd < 0) return null;
        int next = text.IndexOf("\n--- ", lineEnd, StringComparison.Ordinal);
        int end = next < 0 ? text.Length : next;
        return text.Substring(lineEnd + 1, end - lineEnd - 1);
    }

    private static string ExtractBodyByAnchor(string text, string cls, string anchor)
    {
        string marker = "--- !u!" + cls + " &" + anchor + "\n";
        int s = text.IndexOf(marker, StringComparison.Ordinal);
        if (s < 0) return null;
        int bodyStart = s + marker.Length;
        int next = text.IndexOf("\n--- ", bodyStart, StringComparison.Ordinal);
        int end = next < 0 ? text.Length : next;
        return text.Substring(bodyStart, end - bodyStart);
    }

    private static string ReplaceBlockByAnchor(string text, string cls, string anchor, string newBlock)
    {
        string marker = "--- !u!" + cls + " &" + anchor + "\n";
        int s = text.IndexOf(marker, StringComparison.Ordinal);
        if (s < 0) return text;
        int next = text.IndexOf("\n--- ", s + marker.Length, StringComparison.Ordinal);
        int end = next < 0 ? text.Length : next;
        return text.Substring(0, s) + newBlock + text.Substring(end);
    }

    /// <summary>取指定 key 的整行（含缩进），找不到返回 null。</summary>
    private static string ExtractLine(string body, string key)
    {
        string[] lines = body.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith(key + ":", StringComparison.Ordinal))
                return lines[i];
        }
        return null;
    }

    /// <summary>替换 body 中指定 key 行；不存在则追加。</summary>
    private static string PatchLine(string body, string key, string newLine)
    {
        if (newLine == null) return body;
        string[] lines = body.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith(key + ":", StringComparison.Ordinal))
            {
                lines[i] = newLine;
                return string.Join("\n", lines);
            }
        }
        return body + "\n" + newLine;
    }

    /// <summary>提取段落（key 行 + 连续的 "  - " 列表行）。</summary>
    private static string ExtractSection(string body, string key)
    {
        string[] lines = body.Split('\n');
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(key + ":", StringComparison.Ordinal))
            {
                start = i;
                break;
            }
        }
        if (start < 0) return null;
        List<string> section = new List<string>();
        section.Add(lines[start]);
        for (int i = start + 1; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("- ", StringComparison.Ordinal))
            {
                section.Add(lines[i]);
                continue;
            }
            break;
        }
        return string.Join("\n", section.ToArray());
    }

    /// <summary>将模板段落替换为指定段落；模板无该段则追加到末尾。</summary>
    private static string PatchSection(string body, string key, string section)
    {
        if (section == null) return body;
        string[] lines = body.Split('\n');
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(key + ":", StringComparison.Ordinal))
            {
                start = i;
                break;
            }
        }
        if (start < 0)
            return body + "\n" + section;
        int end = start + 1;
        while (end < lines.Length && lines[end].TrimStart().StartsWith("- ", StringComparison.Ordinal))
            end++;
        StringBuilder result = new StringBuilder();
        for (int i = 0; i < start; i++)
            result.Append(lines[i]).Append("\n");
        result.Append(section).Append("\n");
        for (int i = end; i < lines.Length; i++)
        {
            result.Append(lines[i]);
            if (i < lines.Length - 1) result.Append("\n");
        }
        return result.ToString();
    }

    /// <summary>用 2017 模板段落构建修复后的文档体：保留旧文档的引用字段与材质。</summary>
    private static string BuildPatchedBody(string refBody, string oldBody, string cls)
    {
        string body = refBody;

        // 引用字段沿用旧文档（GO / PrefabInternal / ObjectHideFlags）
        body = PatchLine(body, "m_ObjectHideFlags", ExtractLine(oldBody, "m_ObjectHideFlags") ?? "  m_ObjectHideFlags: 1");
        body = PatchLine(body, "m_GameObject", ExtractLine(oldBody, "m_GameObject"));
        body = PatchLine(body, "m_PrefabInternal", ExtractLine(oldBody, "m_PrefabInternal"));

        if (cls == "199")
        {
            // ParticleSystemRenderer：保留旧材质列表（蒸汽/水滴自定义材质）
            body = PatchSection(body, "m_Materials", ExtractSection(oldBody, "m_Materials"));
        }
        return body.TrimEnd('\n');
    }
}
