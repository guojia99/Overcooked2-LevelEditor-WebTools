using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 统一运行时程序集 → 随依赖包分发的 .dll.bytes 打包工具（单程序集模型）。
///
/// 统一单程序集重构后（母本 Assets/WebCustomStubRuntime/，全平台 asmdef，
/// 程序集名 WebCustomStubRuntime）：Unity 编译到
/// Library/ScriptAssemblies/WebCustomStubRuntime.dll。本工具把它复制为
/// Assets/WebCustomStubRuntime/RuntimeDll/WebCustomStubRuntime.dll.bytes（TextAsset），
/// 赋 assetBundleName = webcustomstub_runtime —— 导出「依赖包」zip 时打进
/// OC2DIYLevelRuntimeWLoader/ 文件夹，由 Loader v2.0 从自身目录 LoadFromFile 提取注入。
/// 不再按关卡集编译/打包（旧 Stub_&lt;set&gt; 每集 DLL 体系已废除）。
///
/// 解耦：本类经 LayoutEditorSetExporter.BeforeBuild 钩子接入（[InitializeOnLoad]
/// 订阅），没有本类时 SetExporter 行为不变。
/// </summary>
[InitializeOnLoad]
public static class LayoutStubDllBuilder
{
    /// <summary>统一运行时程序集名（母本 asmdef name）。</summary>
    public const string RuntimeAsmName = "WebCustomStubRuntime";

    /// <summary>统一运行时 bundle 名（依赖包内文件名，Loader 固定按此加载）。</summary>
    public const string RuntimeBundleName = "webcustomstub_runtime";

    /// <summary>母本根目录（源码 + 打包产物 .dll.bytes 落位处）。</summary>
    public const string RuntimeRoot = "Assets/WebCustomStubRuntime";

    private const string RuntimeDllDir = RuntimeRoot + "/RuntimeDll";
    private const string BytesAssetPath = RuntimeDllDir + "/" + RuntimeAsmName + ".dll.bytes";

    static LayoutStubDllBuilder()
    {
        LayoutEditorSetExporter.BeforeBuild += OnBeforeBuild;
    }

    private static void OnBeforeBuild(string setName)
    {
        // 导出任意关卡集/依赖包前，保证统一 runtime 新鲜（过期直接抛错中断导出）。
        StageRuntime(true);
    }

    private static string ProjectRoot()
    {
        return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
    }

    private static string RuntimeDllAbs()
    {
        return ProjectRoot() + "/Library/ScriptAssemblies/" + RuntimeAsmName + ".dll";
    }

    private static string BytesAbs()
    {
        return ProjectRoot() + "/" + BytesAssetPath;
    }

    /// <summary>域重载后的静默 staging：当 Library DLL 比 .dll.bytes 新（或缺失/未赋
    /// bundle 名）时自动重新打包。返回是否 staged。与 CustomStubAutoBake 的域重载钩子
    /// 配合形成「改母本→编译→自动打包」闭环，杜绝导出打进过期 DLL。</summary>
    public static bool StageRuntimeQuiet()
    {
        try
        {
            var dllAbs = RuntimeDllAbs();
            if (!File.Exists(dllAbs))
                return false; // 未编译（异常态，交给导出时的显式报错）
            var bytesAbs = BytesAbs();
            var importer = AssetImporter.GetAtPath(BytesAssetPath);
            var needsStage = !File.Exists(bytesAbs)
                || File.GetLastWriteTime(dllAbs) > File.GetLastWriteTime(bytesAbs).AddSeconds(2)
                || (importer != null && importer.assetBundleName != RuntimeBundleName);
            if (!needsStage)
                return false;
            StageRuntime(false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 静默 staging 异常: " + ex.Message);
            return false;
        }
    }

    [MenuItem("Layout Editor/CustomStub/编译打包 Runtime DLL")]
    public static void StageRuntimeManual()
    {
        AssetDatabase.Refresh();
        EditorApplication.delayCall += delegate
        {
            try
            {
                StageRuntime(false);
                EditorUtility.DisplayDialog("WebCustomStubRuntime",
                    "已打包统一运行时 " + RuntimeAsmName + ".dll → " + BytesAssetPath
                    + "（bundle " + RuntimeBundleName + "）。", "确定");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("WebCustomStubRuntime", "打包失败:\n" + ex.Message, "确定");
            }
        };
    }

    [MenuItem("Layout Editor/CustomStub/Toggle Prepare For Building", false, 11)]
    public static void TogglePrepareForBuilding()
    {
        LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding =
            !LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding;
        if (LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding)
            LevelEditor.PseudoPrefabManager.Instance.DeInit();
        else
            LevelEditor.PseudoPrefabManager.Instance.Init();
        Debug.Log("[CustomStub] prepareForBuilding = "
            + LevelEditor.PseudoPrefabManager.Instance.prepareForBuilding);
    }

    [MenuItem("Layout Editor/CustomStub/Build AssetBundles（含 Runtime staging）", false, 100)]
    public static void BuildAssetBundlesWithStaging()
    {
        BuildAssetBundlesWithStaging(BuildAssetBundleOptions.None);
    }

    [MenuItem("Layout Editor/CustomStub/Build AssetBundles（含 Runtime staging，ForceRebuild）", false, 101)]
    public static void BuildAssetBundlesWithStagingForceRebuild()
    {
        BuildAssetBundlesWithStaging(BuildAssetBundleOptions.ForceRebuildAssetBundle);
    }

    private static void BuildAssetBundlesWithStaging(BuildAssetBundleOptions options)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog("Build AssetBundles",
                "Unity 正在编译/导入脚本，请等待完成后再构建。", "确定");
            return;
        }
        var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!CheckPrepareForBuildingSoft(activeScene))
            return;

        // 切勿在此追加 AssetDatabase.Refresh()（触发脚本重编译 → build 被取消）。
        StageRuntime(false);

        var assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
            Directory.CreateDirectory(assetBundleDirectory);
        BuildPipeline.BuildAssetBundles(assetBundleDirectory, options, BuildTarget.StandaloneWindows);
    }

    /// <summary>「保存/构建前须 Prepare For Building」守卫的软调用（反射优先上游实现，
    /// 类型缺失回落内联同款检查——干净环境防 CS0103）。</summary>
    private static bool CheckPrepareForBuildingSoft(UnityEngine.SceneManagement.Scene activeScene)
    {
        var validatorType = FindType("TargetSceneSaveValidator");
        if (validatorType != null)
        {
            try
            {
                var m = validatorType.GetMethod("CheckPrepareForBuilding",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m != null)
                    return (bool)m.Invoke(null, new object[] { activeScene });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CustomStub] TargetSceneSaveValidator 软调用失败，回落内联检查: " + ex.Message);
            }
        }
        return InlinePrepareForBuildingCheck(activeScene);
    }

    private static Type FindType(string typeName)
    {
        var t = Type.GetType(typeName);
        if (t != null)
            return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(typeName);
            if (t != null)
                return t;
        }
        return null;
    }

    private static bool InlinePrepareForBuildingCheck(UnityEngine.SceneManagement.Scene activeScene)
    {
        if (!activeScene.IsValid())
            return true;
        foreach (var root in activeScene.GetRootGameObjects())
        {
            var mgr = root.GetComponent<LevelEditor.PseudoPrefabManager>();
            if (mgr == null)
                continue;
            if (mgr.prepareForBuilding)
                return true;
            EditorUtility.DisplayDialog("错误",
                "保存或构建场景前先点击 Tools - Toggle Prepare For Building 清除临时物体！", "确定");
            return false;
        }
        return true;
    }

    /// <summary>统一 runtime DLL 状态（web 状态端点用）：
    /// missing=DLL 尚未编译；stale=母本源码比 DLL 新（编辑后未编译完成）；
    /// fresh=DLL 就绪可 staging/导出。</summary>
    public static string GetRuntimeStageState()
    {
        var dllAbs = RuntimeDllAbs();
        if (!File.Exists(dllAbs))
            return "missing";
        var dllTime = File.GetLastWriteTime(dllAbs);
        foreach (var f in Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (File.GetLastWriteTime(f) > dllTime.AddSeconds(2))
                return "stale";
        }
        var asmdef = RuntimeRoot + "/" + RuntimeAsmName + ".asmdef";
        if (File.Exists(asmdef) && File.GetLastWriteTime(asmdef) > dllTime.AddSeconds(2))
            return "stale";
        return "fresh";
    }

    /// <summary>把 Library/ScriptAssemblies/WebCustomStubRuntime.dll staging 为
    /// .dll.bytes 并赋 bundle 名。throwOnStale=true（导出流程）：DLL 缺失/过期直接
    /// 抛错中断导出；false（手动/静默）：打警告返回。</summary>
    public static void StageRuntime(bool throwOnStale)
    {
        var dllAbs = RuntimeDllAbs();
        var error = "";
        if (!File.Exists(dllAbs))
        {
            error = "未找到 " + RuntimeAsmName + ".dll（Library/ScriptAssemblies）。请等待 Unity 编译完成后重试。";
        }
        else
        {
            var dllTime = File.GetLastWriteTime(dllAbs);
            foreach (var f in Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTime(f) > dllTime.AddSeconds(2))
                {
                    error = "母本源码比 " + RuntimeAsmName + ".dll 新（编辑后尚未编译完成）。请等 Unity 编译结束后重试。";
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            if (throwOnStale)
                throw new Exception("[CustomStub] " + error);
            Debug.LogWarning("[CustomStub] " + error);
            return;
        }

        if (!Directory.Exists(RuntimeDllDir))
            Directory.CreateDirectory(RuntimeDllDir);
        File.Copy(dllAbs, BytesAbs(), true);
        AssetDatabase.ImportAsset(BytesAssetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(BytesAssetPath);
        if (importer != null && importer.assetBundleName != RuntimeBundleName)
        {
            importer.assetBundleName = RuntimeBundleName;
            importer.SaveAndReimport();
        }
        Debug.Log("[CustomStub] 已打包统一运行时 " + RuntimeAsmName + ".dll → " + BytesAssetPath
            + "（bundle " + RuntimeBundleName + "）");
    }
}
