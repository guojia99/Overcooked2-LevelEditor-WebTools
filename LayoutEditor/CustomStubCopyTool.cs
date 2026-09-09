using System;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 母本 → 关卡集 stub 目录拷贝工具。
///
/// 母本：Assets/Editor/LayoutEditor/CustomStub/（编辑器平台程序集，仅作模板+语法校验），
/// 按功能分子文件夹：core/（StubLog/GameApi/EntryPoint/HarmonyPatches）、RandomCrate/、
/// HotPot/（含 PushablePot/PushableVoidFall/Target）、UtensilTiming/、Switch/（定时开关
/// +按钮复位）、Terminal/、WorldMap/。
/// 拷贝目标：Assets/LevelSets/&lt;set&gt;/stub/（目录结构与母本镜像），内含关卡集专属
/// 程序集 Stub_&lt;set&gt;.asmdef（references: LevelEditorStub，全平台编译，asmdef 递归
/// 覆盖子文件夹）。
///
/// GUID 约定：拷贝只复制 .cs 内容、不复制 .meta —— 首次拷贝由 Unity 生成新 GUID
/// （每集独立脚本身份）；重复拷贝做内容同步，绝不改动已存在的 .meta
/// （保证场景里已烘焙的脚本引用稳定）。
/// 旧扁平布局迁移：stub/&lt;name&gt;.cs → stub/&lt;sub&gt;/&lt;name&gt;.cs 用
/// File.Move 连 .meta 成对移动（GUID 保留，场景引用不断链）。
/// </summary>
public static class CustomStubCopyTool
{
    private const string MasterDir = "Assets/Editor/LayoutEditor/CustomStub";
    private const string LevelSetsRoot = "Assets/LevelSets";

    [MenuItem("Layout Editor/CustomStub/拷贝到关卡集…")]
    public static void OpenWindow()
    {
        var window = EditorWindow.GetWindow<CustomStubCopyToolWindow>(false, "CustomStub 拷贝");
        window.Repaint();
    }

    /// <summary>关卡集名 → stub 程序集名（仅保留字母/数字/下划线）。</summary>
    public static string StubAssemblyName(string setName)
    {
        var sb = new StringBuilder();
        foreach (var ch in (setName ?? ""))
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return "Stub_" + sb;
    }

    /// <summary>是否已配置 stub（stub/ 目录 + Stub_&lt;set&gt;.asmdef 存在）。web 状态端点用。</summary>
    public static bool IsConfigured(string setName)
    {
        var stubDir = LevelSetsRoot + "/" + setName + "/stub";
        return AssetDatabase.IsValidFolder(stubDir)
            && File.Exists(stubDir + "/" + StubAssemblyName(setName) + ".asmdef");
    }

    /// <summary>递归收集母本 .cs，返回相对路径列表（如 "core/StubLog.cs"，正斜杠）。</summary>
    private static System.Collections.Generic.List<string> CollectMasterFiles()
    {
        var list = new System.Collections.Generic.List<string>();
        CollectInto(MasterDir, list);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static void CollectInto(string dir, System.Collections.Generic.List<string> list)
    {
        foreach (var f in Directory.GetFiles(dir, "*.cs"))
            list.Add(f.Substring(MasterDir.Length).TrimStart('/', '\\').Replace('\\', '/'));
        foreach (var sub in Directory.GetDirectories(dir))
            CollectInto(sub, list);
    }

    /// <summary>检测单个关卡集 stub 副本与母本是否漂移（内容、相对路径或程序集名）。
    /// 未拷贝的关卡集返回 false（用 IsConfigured 区分）。web 状态端点用。</summary>
    public static bool IsDrifted(string setName)
    {
        if (!Directory.Exists(MasterDir))
            return false;
        var stubDir = LevelSetsRoot + "/" + setName + "/stub";
        if (!Directory.Exists(stubDir))
            return false;
        var masterRels = CollectMasterFiles();
        var masterSet = new System.Collections.Generic.HashSet<string>(masterRels, StringComparer.Ordinal);
        foreach (var rel in masterRels)
        {
            var dst = Path.Combine(stubDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dst))
                return true; // 含旧扁平布局（文件还在 stub/ 根部未迁入子文件夹）
            if (File.ReadAllText(Path.Combine(MasterDir, rel.Replace('/', Path.DirectorySeparatorChar))) != File.ReadAllText(dst))
                return true;
        }
        // 副本侧多余/残留文件（母本已删除或旧扁平位置）也视为漂移
        foreach (var dstFile in Directory.GetFiles(stubDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = dstFile.Substring(stubDir.Length).TrimStart('/', '\\').Replace('\\', '/');
            if (!masterSet.Contains(rel))
                return true;
        }
        var asmdef = Path.Combine(stubDir, StubAssemblyName(setName) + ".asmdef");
        if (File.Exists(asmdef))
        {
            // 程序集名/引用漂移也视为需要同步（CopyToSet 会重写 asmdef）
            if (!asmdef.Contains(StubAssemblyName(setName) + ".asmdef"))
                return true;
        }
        return false;
    }

    /// <summary>检测各关卡集 stub 副本与母本的内容漂移，有漂移则自动同步
    /// （仅内容，不动 .meta/GUID）。返回发生同步的关卡集数。母本改动后由
    /// CustomStubAutoBake 在域重载后调用，保证副本自动跟进、无需手动同步。</summary>
    public static int SyncAllDrifted()
    {
        if (!Directory.Exists(MasterDir))
            return 0;
        var synced = 0;
        foreach (var setDir in Directory.GetDirectories(LevelSetsRoot))
        {
            var setName = Path.GetFileName(setDir);
            if (!IsDrifted(setName))
                continue;
            try
            {
                var result = CopyToSet(setName);
                Debug.Log("[CustomStub] 检测到母本更新，已自动同步 " + setName + ": " + result);
                synced++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CustomStub] 自动同步 " + setName + " 失败: " + ex.Message);
            }
        }
        return synced;
    }

    /// <summary>RandomDispenser 包装 prefab（commonW1）：随机食材箱的专属道具类型。
    /// 基于 Dispenser 包装复制，自带数据载体空壳（tag "RandomCrate|" + 空 soArray）——
    /// web 目录/调色板按独立道具展示，apply 按实例填充随机配置。幂等。</summary>
    public const string RandomDispenserPrefabPath = "Assets/commonW1/prefabs/core/counters/RandomDispenser.prefab";
    private const string BaseDispenserPrefabPath = "Assets/common01/prefabs/counters/Dispenser.prefab";

    public static void EnsureRandomDispenserPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RandomDispenserPrefabPath);
        if (existing != null)
            return;
        try
        {
            EnsureFolder("Assets/commonW1/prefabs/core/counters");
            if (!AssetDatabase.CopyAsset(BaseDispenserPrefabPath, RandomDispenserPrefabPath))
            {
                Debug.LogWarning("[CustomStub] RandomDispenser 复制失败: " + BaseDispenserPrefabPath);
                return;
            }
            AssetDatabase.SaveAssets();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(RandomDispenserPrefabPath);
            if (go == null)
                return;
            go.name = "RandomDispenser";
            var tag = go.GetComponent<SpecificPseudoPrefabTag>();
            if (tag == null)
                tag = go.AddComponent<SpecificPseudoPrefabTag>();
            tag.prefabTag = "RandomCrate|";
            var soArray = go.GetComponent<PseudoPrefabSOArray>();
            if (soArray == null)
                soArray = go.AddComponent<PseudoPrefabSOArray>();
            soArray.pseudoPrefabSOs = new PseudoPrefabSO[0];
            EditorUtility.SetDirty(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[CustomStub] 已生成 RandomDispenser 包装 prefab: " + RandomDispenserPrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] RandomDispenser 生成异常: " + ex.Message);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        var segments = folderPath.Split('/');
        var current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            var next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    /// <summary>执行拷贝（内容同步 + 生成 asmdef + 清理母本中已删除的脚本）。返回日志行数。</summary>
    public static string CopyToSet(string setName)
    {
        return CopyToSet(setName, true);
    }

    /// <summary>refresh=false：只写文件不 Refresh —— web API 用（先应答 HTTP，
    /// 再由调用方 delayCall 触发 Refresh，避免域重载把响应截断）。</summary>
    public static string CopyToSet(string setName, bool refresh)
    {
        if (string.IsNullOrEmpty(setName) || !Directory.Exists(MasterDir))
            return "母本目录不存在: " + MasterDir;

        var dstDir = LevelSetsRoot + "/" + setName + "/stub";
        if (!Directory.Exists(dstDir))
            Directory.CreateDirectory(dstDir);

        var migrated = 0;
        var copied = 0;
        var updated = 0;
        var masterRels = CollectMasterFiles();
        var masterSet = new System.Collections.Generic.HashSet<string>(masterRels, StringComparer.Ordinal);

        foreach (var rel in masterRels)
        {
            var srcFile = Path.Combine(MasterDir, rel.Replace('/', Path.DirectorySeparatorChar));
            var dstFile = Path.Combine(dstDir, rel.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(srcFile);

            // 旧扁平布局迁移：目标在子文件夹且不存在，而 stub/ 根部旧位置存在 →
            // .cs 与 .cs.meta 成对 File.Move（GUID 随 .meta 保留，场景引用不断链）
            var legacyFile = Path.Combine(dstDir, Path.GetFileName(rel));
            var isSubfolder = rel.IndexOf('/') >= 0;
            if (isSubfolder && !string.Equals(legacyFile, dstFile, StringComparison.Ordinal))
            {
                if (!File.Exists(dstFile) && File.Exists(legacyFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
                    File.Move(legacyFile, dstFile);
                    var legacyMeta = legacyFile + ".meta";
                    if (File.Exists(legacyMeta))
                        File.Move(legacyMeta, dstFile + ".meta");
                    migrated++;
                }
                else if (File.Exists(dstFile) && File.Exists(legacyFile))
                {
                    // 新旧并存的中间态（上次迁移中断）：以新为准，删旧（其 GUID 废弃）
                    Debug.LogWarning("[CustomStub] " + setName + ": 新旧位置并存，删除旧扁平残留 "
                        + Path.GetFileName(rel) + "（旧 GUID 废弃，若场景断链请从 git 恢复检查）");
                    AssetDatabase.DeleteAsset(legacyFile.Replace('\\', '/'));
                }
            }

            if (File.Exists(dstFile) && File.ReadAllText(dstFile) == content)
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
            var hadMeta = File.Exists(dstFile + ".meta");
            File.WriteAllText(dstFile, content);
            if (hadMeta)
                updated++;
            else
                copied++;
        }

        // asmdef：关卡集专属程序集（与母本不同名，全平台编译，引用 LevelEditorStub）。
        // 注意：0Harmony（Assets/Plugins 预编译 DLL）不写入 references——Unity 2017.4
        // 的 asmdef 只能引用其他 asmdef，预编译 DLL 走全局自动引用（Auto Reference 默认开）。
        var asmdefName = StubAssemblyName(setName);
        var asmdefFile = Path.Combine(dstDir, asmdefName + ".asmdef");
        var asmdefContent = "{\n"
            + "    \"name\": \"" + asmdefName + "\",\n"
            + "    \"references\": [\n"
            + "        \"LevelEditorStub\"\n"
            + "    ],\n"
            + "    \"includePlatforms\": [],\n"
            + "    \"excludePlatforms\": []\n"
            + "}\n";
        if (!File.Exists(asmdefFile) || File.ReadAllText(asmdefFile) != asmdefContent)
        {
            File.WriteAllText(asmdefFile, asmdefContent);
            updated++;
        }

        // 清理母本中已删除、但关卡集还残留的脚本（连同 .meta，Guid 随之废弃）
        var removed = 0;
        foreach (var dstFile in Directory.GetFiles(dstDir, "*.cs", SearchOption.AllDirectories))
        {
            var rel = dstFile.Substring(dstDir.Length).TrimStart('/', '\\').Replace('\\', '/');
            if (masterSet.Contains(rel))
                continue;
            AssetDatabase.DeleteAsset(dstFile.Replace('\\', '/'));
            removed++;
        }
        // 清理空子目录（连同 folder .meta；自底向上）
        RemoveEmptyDirs(dstDir);

        if (refresh)
            AssetDatabase.Refresh();
        return "新增 " + copied + " / 更新 " + updated + " / 迁移 " + migrated + " / 清理 " + removed
            + " → " + dstDir + "（程序集 " + asmdefName + "）";
    }

    /// <summary>自底向上删除空子目录（AssetDatabase.DeleteAsset 连同 folder .meta；
    /// 根目录本身不动）。</summary>
    private static void RemoveEmptyDirs(string root)
    {
        foreach (var dir in Directory.GetDirectories(root))
        {
            RemoveEmptyDirs(dir);
            if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));
        }
    }
}

/// <summary>关卡集列表窗口。</summary>
public class CustomStubCopyToolWindow : EditorWindow
{
    private Vector2 _scroll;

    private void OnFocus()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "把 CustomStub 母本（Assets/Editor/LayoutEditor/CustomStub，按 core/RandomCrate/" +
            "HotPot/UtensilTiming/Switch/Terminal/WorldMap 子文件夹组织）拷贝到关卡集的 stub/ 目录" +
            "（目录结构镜像母本），生成关卡集专属程序集 Stub_<set>。重复拷贝为内容同步：保留已有" +
            "关卡集脚本的 GUID（场景引用稳定），只更新代码内容；旧扁平布局自动迁移进子文件夹" +
            "（.meta 随文件移动，GUID 不变）。", MessageType.Info);

        if (!Directory.Exists("Assets/Editor/LayoutEditor/CustomStub"))
        {
            EditorGUILayout.HelpBox("母本目录不存在。", MessageType.Error);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var folder in AssetDatabase.GetSubFolders("Assets/LevelSets"))
        {
            var setName = folder.Substring(folder.LastIndexOf('/') + 1);
            var stubDir = folder + "/stub";
            var hasStub = AssetDatabase.IsValidFolder(stubDir);
            var asmdef = CustomStubCopyTool.StubAssemblyName(setName) + ".asmdef";
            var asmdefOk = hasStub && File.Exists(stubDir + "/" + asmdef);
            var state = hasStub
                ? (asmdefOk ? "已配置" : "stub/ 存在但缺 " + asmdef)
                : "未拷贝";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(setName, GUILayout.Width(160));
            EditorGUILayout.LabelField(state, GUILayout.Width(200));
            if (GUILayout.Button(hasStub ? "同步更新" : "拷贝", GUILayout.Width(80)))
            {
                try
                {
                    var result = CustomStubCopyTool.CopyToSet(setName);
                    Debug.Log("[CustomStub] " + setName + ": " + result);
                    EditorUtility.DisplayDialog("CustomStub", setName + "\n" + result, "确定");
                    AssetDatabase.Refresh();
                    Repaint();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("CustomStub", "拷贝失败:\n" + ex.Message, "确定");
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
