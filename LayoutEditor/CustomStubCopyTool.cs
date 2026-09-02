using System;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 母本 → 关卡集 stub 目录拷贝工具。
///
/// 母本：Assets/Editor/LayoutEditor/CustomStub/（编辑器平台程序集，仅作模板+语法校验）。
/// 拷贝目标：Assets/LevelSets/&lt;set&gt;/stub/，内含关卡集专属程序集
/// Stub_&lt;set&gt;.asmdef（references: LevelEditorStub，全平台编译）。
///
/// GUID 约定：拷贝只复制 .cs 内容、不复制 .meta —— 首次拷贝由 Unity 生成新 GUID
/// （每集独立脚本身份）；重复拷贝做内容同步，绝不改动已存在的 .meta
/// （保证场景里已烘焙的脚本引用稳定）。
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

    /// <summary>检测各关卡集 stub 副本与母本的内容漂移，有漂移则自动同步
    /// （仅内容，不动 .meta/GUID）。返回发生同步的关卡集数。母本改动后由
    /// CustomStubAutoBake 在域重载后调用，保证副本自动跟进、无需手动同步。</summary>
    public static int SyncAllDrifted()
    {
        if (!Directory.Exists(MasterDir))
            return 0;
        var synced = 0;
        var masterFiles = Directory.GetFiles(MasterDir, "*.cs");
        foreach (var setDir in Directory.GetDirectories(LevelSetsRoot))
        {
            var stubDir = Path.Combine(setDir, "stub");
            if (!Directory.Exists(stubDir))
                continue;
            var drifted = false;
            foreach (var src in masterFiles)
            {
                var dst = Path.Combine(stubDir, Path.GetFileName(src));
                if (!File.Exists(dst) || File.ReadAllText(src) != File.ReadAllText(dst))
                {
                    drifted = true;
                    break;
                }
            }
            var setName = Path.GetFileName(setDir);
            var asmdef = Path.Combine(stubDir, StubAssemblyName(setName) + ".asmdef");
            if (File.Exists(asmdef))
            {
                // 程序集名/引用漂移也视为需要同步（CopyToSet 会重写 asmdef）
                if (!asmdef.Contains(StubAssemblyName(setName) + ".asmdef"))
                    drifted = true;
            }
            if (!drifted)
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
        if (string.IsNullOrEmpty(setName) || !Directory.Exists(MasterDir))
            return "母本目录不存在: " + MasterDir;

        var dstDir = LevelSetsRoot + "/" + setName + "/stub";
        if (!Directory.Exists(dstDir))
            Directory.CreateDirectory(dstDir);

        var copied = 0;
        var updated = 0;
        var masterNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        foreach (var srcFile in Directory.GetFiles(MasterDir, "*.cs"))
        {
            var fileName = Path.GetFileName(srcFile);
            masterNames.Add(fileName);
            var dstFile = Path.Combine(dstDir, fileName);
            var content = File.ReadAllText(srcFile);
            if (File.Exists(dstFile) && File.ReadAllText(dstFile) == content)
                continue;
            File.WriteAllText(dstFile, content);
            if (File.Exists(dstFile + ".meta"))
                updated++;
            else
                copied++;
        }

        // asmdef：关卡集专属程序集（与母本不同名，全平台编译，引用 LevelEditorStub）
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
        foreach (var dstFile in Directory.GetFiles(dstDir, "*.cs"))
        {
            if (masterNames.Contains(Path.GetFileName(dstFile)))
                continue;
            var assetPath = dstFile.Replace('\\', '/');
            AssetDatabase.DeleteAsset(assetPath);
            removed++;
        }

        AssetDatabase.Refresh();
        return "新增 " + copied + " / 更新 " + updated + " / 清理 " + removed
            + " → " + dstDir + "（程序集 " + asmdefName + "）";
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
            "把 CustomStub 母本（Assets/Editor/LayoutEditor/CustomStub）拷贝到关卡集的 stub/ 目录，" +
            "生成关卡集专属程序集 Stub_<set>。重复拷贝为内容同步：保留已有关卡集脚本的 GUID" +
            "（场景引用稳定），只更新代码内容。", MessageType.Info);

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
