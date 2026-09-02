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

    /// <summary>域重载后的静默 staging：对每个有 stub 源码的关卡集，当 Library DLL
    /// 比 .dll.bytes 新（或 .bytes 缺失/未赋 bundle 名）时自动重新打包。返回 staged 数。
    /// 与 CustomStubAutoBake 的域重载钩子配合，形成「拷贝→编译→自动打包」闭环，
    /// 杜绝导出时打进过期 DLL。</summary>
    public static int StageAllSetsQuiet()
    {
        var staged = 0;
        try
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            foreach (var folder in AssetDatabase.GetSubFolders(LevelSetsRoot))
            {
                var setName = folder.Substring(folder.LastIndexOf('/') + 1);
                var stubDir = folder + "/stub";
                if (!AssetDatabase.IsValidFolder(stubDir) || Directory.GetFiles(stubDir, "*.cs").Length == 0)
                    continue;
                var asmName = CustomStubCopyTool.StubAssemblyName(setName);
                var dllAbs = projectRoot + "/Library/ScriptAssemblies/" + asmName + ".dll";
                var bytesAbs = Application.dataPath.Replace("Assets", "") + stubDir + "/" + asmName + ".dll.bytes";
                var importer = AssetImporter.GetAtPath(stubDir + "/" + asmName + ".dll.bytes");
                var needsStage = !File.Exists(dllAbs)
                    ? false // DLL 未编译（异常态，交给导出时的显式报错）
                    : !File.Exists(bytesAbs)
                      || File.GetLastWriteTime(dllAbs) > File.GetLastWriteTime(bytesAbs).AddSeconds(2)
                      || (importer != null && importer.assetBundleName != setName + "/runtime");
                if (!needsStage)
                    continue;
                StageSet(setName, false);
                staged++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 静默 staging 异常: " + ex.Message);
        }
        return staged;
    }

    [MenuItem("Layout Editor/CustomStub/编译打包 Stub DLL（全部关卡集）")]
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

    // ---- 手动 Build AssetBundles 的 staging 加固 ----
    // 原 Tools/Build AssetBundles（CreateAssetBundles.cs，LayoutEditor 外，不改）不经过
    // LayoutEditorSetExporter.BeforeBuild 钩子，曾导致真机 runtime 缺失/过期。
    // 这里提供含 staging 的替代入口（同款序列：StageSet → BuildAssetBundles）。

    [MenuItem("Layout Editor/CustomStub/Toggle Prepare For Building", false, 11)]
    public static void TogglePrepareForBuilding()
    {
        // 与 Tools/Toggle Prepare For Building 同款逻辑（LayoutEditor 外的原菜单不改）：
        // 打包前清除临时加载的物体，打包后切回恢复预览。
        LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding =
            !LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding;
        if (LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding)
        {
            LevelEditor.PseudoPrefabManager.Instance.DeInit();
        }
        else
        {
            LevelEditor.PseudoPrefabManager.Instance.Init();
        }
        Debug.Log("[CustomStub] prepareForBuilding = "
            + LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding);
    }

    [MenuItem("Layout Editor/CustomStub/Build AssetBundles（含 Stub staging）", false, 100)]
    public static void BuildAssetBundlesWithStaging()
    {
        BuildAssetBundlesWithStaging(BuildAssetBundleOptions.None);
    }

    [MenuItem("Layout Editor/CustomStub/Build AssetBundles（含 Stub staging，ForceRebuild）", false, 101)]
    public static void BuildAssetBundlesWithStagingForceRebuild()
    {
        BuildAssetBundlesWithStaging(BuildAssetBundleOptions.ForceRebuildAssetBundle);
    }

    private static void BuildAssetBundlesWithStaging(BuildAssetBundleOptions options)
    {
        // 编译/导入进行中调用 BuildAssetBundles 会被直接取消
        // （"Building AssetBundles was canceled"）——显式拦截并提示。
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog("Build AssetBundles",
                "Unity 正在编译/导入脚本，请等待完成后再构建。", "确定");
            return;
        }
        var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!TargetSceneSaveValidator.CheckPrepareForBuilding(activeScene))
            return;

        foreach (var folder in AssetDatabase.GetSubFolders(LevelSetsRoot))
        {
            var setName = folder.Substring(folder.LastIndexOf('/') + 1);
            var stubDir = folder + "/stub";
            if (!AssetDatabase.IsValidFolder(stubDir) || Directory.GetFiles(stubDir, "*.cs").Length == 0)
                continue;
            // throwOnStale=false：DLL 缺失/过期打警告但不中断 build。
            // 切勿在此追加 AssetDatabase.Refresh()（触发脚本重编译 → build 被取消）。
            StageSet(setName, false);
            Debug.Log("[CustomStub] 手动 build 提醒：关卡集 " + setName + " 使用了随机食材箱，"
                + "分发时除 info_" + setName + " / s_* 外还必须拷贝 Assets/AssetBundles/" + setName
                + "/runtime（无扩展名，剔除 .manifest/.meta），否则真机随机箱退化为固定箱。"
                + "推荐改用 web 导出（自动打包含 runtime 的 zip）。");
        }

        var assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
            Directory.CreateDirectory(assetBundleDirectory);
        BuildPipeline.BuildAssetBundles(assetBundleDirectory, options, BuildTarget.StandaloneWindows);
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
            error = "未找到 " + asmName + ".dll（Library/ScriptAssemblies）。请先 Layout Editor/CustomStub/拷贝到关卡集… 并等待 Unity 编译完成。";
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
