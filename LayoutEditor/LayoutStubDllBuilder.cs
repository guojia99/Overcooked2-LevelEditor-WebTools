using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 关卡集 stub 程序集 → 随关卡分发的 .dll.bytes 打包工具。
///
/// Unity 会把关卡集程序集（Stub_&lt;set&gt;，见 CustomStubCopyTool）编译到
/// Library/ScriptAssemblies/Stub_&lt;set&gt;.dll。本工具把它复制为
/// Assets/LevelSets/&lt;set&gt;/stub/Stub_&lt;set&gt;.dll.bytes（TextAsset），
/// 并赋 assetBundleName = &lt;set&gt;/runtime —— 导出关卡集 zip 时与
/// info_&lt;set&gt; / s_* 同层打包（普通 bundle，加载器插件可 LoadAsset 提取）。
///
/// 解耦：本类经 LayoutEditorSetExporter.BeforeBuild 钩子接入（[InitializeOnLoad]
/// 订阅），没有本类时 SetExporter 行为不变；反之本类也不依赖 LayoutEditor 其他类型。
/// </summary>
[InitializeOnLoad]
public static class LayoutStubDllBuilder
{
    private const string LevelSetsRoot = "Assets/LevelSets";

    static LayoutStubDllBuilder()
    {
        LayoutEditorSetExporter.BeforeBuild += OnBeforeBuild;
    }

    private static void OnBeforeBuild(string setName)
    {
        StageSet(setName, true);
    }

    [MenuItem("Tools/CustomStub/编译打包 Stub DLL（全部关卡集）")]
    public static void StageAllManual()
    {
        // 先 Refresh 触发潜在重编译，等一帧再取产物，避免取到旧 DLL
        AssetDatabase.Refresh();
        EditorApplication.delayCall += delegate
        {
            var ok = 0;
            var skipped = 0;
            try
            {
                foreach (var folder in AssetDatabase.GetSubFolders(LevelSetsRoot))
                {
                    var setName = folder.Substring(folder.LastIndexOf('/') + 1);
                    var stubDir = folder + "/stub";
                    if (!AssetDatabase.IsValidFolder(stubDir) || Directory.GetFiles(stubDir, "*.cs").Length == 0)
                    {
                        skipped++;
                        continue;
                    }
                    StageSet(setName, false);
                    ok++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("CustomStub DLL", "打包失败:\n" + ex.Message, "确定");
                return;
            }
            EditorUtility.DisplayDialog("CustomStub DLL",
                "已打包 " + ok + " 个关卡集（跳过 " + skipped + " 个未配置 stub 的关卡集）。", "确定");
        };
    }

    /// <summary>把 Library/ScriptAssemblies/Stub_&lt;set&gt;.dll staging 为 .dll.bytes 并赋 bundle 名。
    /// throwOnStale=true（导出流程）：DLL 缺失/过期直接抛错中断导出；false（手动）：弹窗提示。</summary>
    public static void StageSet(string setName, bool throwOnStale)
    {
        var stubDir = LevelSetsRoot + "/" + setName + "/stub";
        if (!Directory.Exists(stubDir))
            return;

        var asmName = CustomStubCopyTool.StubAssemblyName(setName);
        var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
        var dllAbs = projectRoot + "/Library/ScriptAssemblies/" + asmName + ".dll";

        var error = "";
        if (!File.Exists(dllAbs))
        {
            error = "未找到 " + asmName + ".dll（Library/ScriptAssemblies）。请先 Tools/CustomStub/拷贝到关卡集… 并等待 Unity 编译完成。";
        }
        else
        {
            // 新鲜度：DLL 必须不早于 stub 源码（.cs / .asmdef）
            var dllTime = File.GetLastWriteTime(dllAbs);
            foreach (var f in Directory.GetFiles(stubDir))
            {
                var lower = f.ToLower();
                if (!lower.EndsWith(".cs") && !lower.EndsWith(".asmdef"))
                    continue;
                if (File.GetLastWriteTime(f) > dllTime.AddSeconds(2))
                {
                    error = "stub 源码比 " + asmName + ".dll 新（编辑后尚未编译完成）。请等 Unity 编译结束后重试。";
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            if (throwOnStale)
                throw new Exception("[CustomStub] " + setName + ": " + error);
            Debug.LogWarning("[CustomStub] " + setName + ": " + error);
            return;
        }

        var bytesPath = stubDir + "/" + asmName + ".dll.bytes";
        File.Copy(dllAbs, Application.dataPath.Replace("Assets", "") + bytesPath, true);
        AssetDatabase.ImportAsset(bytesPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(bytesPath);
        if (importer != null)
        {
            var want = setName + "/runtime";
            if (importer.assetBundleName != want)
            {
                importer.assetBundleName = want;
                importer.SaveAndReimport();
            }
        }
        Debug.Log("[CustomStub] " + setName + ": 已打包 " + asmName + ".dll → " + bytesPath
            + "（bundle " + setName + "/runtime）");
    }
}
