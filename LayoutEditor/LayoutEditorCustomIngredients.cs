using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通用内容源库（Assets/common03）与背景占位库（Assets/commonW1）的引用校验与 bundle 依赖注册。
///
/// 历史机制（已废弃）：源库曾在 Assets/Editor/LayoutEditor/Import 下，无法进入
/// AssetBundle 构建，保存时把用到的资产拷入 Assets/LevelSets/&lt;set&gt;/custom_web/
/// （分配新 guid、改写引用）。现源库已迁移到 Assets/common03/（folder meta 的
/// assetBundleName=common03），可直接被 Tools/Build AssetBundles 打成 common03
/// bundle，关卡/场景**直接引用 common03 内资产**，不再依赖后端动态同步/拷贝。
/// 原「Web 内置」白名单/manifest 门槛已随 common03 通用化移除，内容全量可用。
///
/// 本类保留的职责：
///  1. EnsureDocCopies()：仅校验——场景文档仍引用历史 Import/custom_web 资产时告警。
///  2. EnsureWebDependencies()：把关卡引用到的 common03 资产所需 bundle
///     （common03 本体 + 各资产 bundleName 指向的游戏原 bundle）注册进
///     LevelInfoSO.dependencies。仅写入 StreamingAssets/Windows 下已存在的 bundle，
///     防御宿主 PseudoPrefabManager 对缺失 bundle 抛 KeyNotFoundException。
///
/// common03 与 common01/common02 同级：后端正常扫描、前端菜单全量可用（无白名单/
/// manifest/web 分组）。宿主文件一律不动。
/// </summary>
public static class LayoutEditorCustomIngredients
{
    /// <summary>通用内容源库根目录（由 Assets/Editor/LayoutEditor/Import 迁移而来）。</summary>
    public const string Common03Root = "Assets/common03";

    /// <summary>背景与食材/成品菜装饰占位源库（prefab + .meta，打包为 commonW1 bundle）。</summary>
    public const string CommonW1Root = "Assets/commonW1";

    /// <summary>旧 custom_web 拷贝目录名（机制已废弃，仅为兼容读取历史数据保留）。</summary>
    public const string CustomDirName = "custom_web";

    /// <summary>common03 bundle 名（folder meta assetBundleName）。</summary>
    public const string Common03BundleName = "common03";

    /// <summary>commonW1 bundle 名（folder meta assetBundleName）。</summary>
    public const string CommonW1BundleName = "commonW1";

    /// <summary>场景保存时从 doc 收集到的 common03 引用所需游戏 bundle，
    ///  由随后 SyncLevelInfo → EnsureWebDependencies 一并注册。</summary>
    private static readonly HashSet<string> _pendingDocBundles = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>旧 custom_web 拷贝目录（兼容读取历史数据用，新逻辑不再写入）。</summary>
    public static string CustomIngredientsDir(string levelSet)
    {
        return "Assets/LevelSets/" + levelSet + "/" + CustomDirName;
    }

    /// <summary>是否 common03 源库内资产（通用内容）。</summary>
    public static bool IsCommon03Asset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        return assetPath.IndexOf("/common03/", StringComparison.Ordinal) >= 0;
    }

    /// <summary>是否 commonW1 背景占位源库内资产。</summary>
    public static bool IsCommonW1Asset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        return assetPath.IndexOf("/commonW1/", StringComparison.Ordinal) >= 0;
    }

    /// <summary>是否本地源库资产（common03 / commonW1 / common01 / common02）：这些目录里的
    ///  wrapper prefab 与 PseudoPrefabSO 都按 bundleName 指向游戏原 bundle，
    ///  可安全解析注册依赖（AddDependency 只收实际存在的 bundle）。</summary>
    private static bool IsLocalSourceAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        return IsCommon03Asset(assetPath)
            || IsCommonW1Asset(assetPath)
            || assetPath.IndexOf("/common01/", StringComparison.Ordinal) >= 0
            || assetPath.IndexOf("/common02/", StringComparison.Ordinal) >= 0;
    }

    /// <summary>保存布局（/api/scene/layout）时调用：仅校验文档中的历史引用
    ///  （历史 Import / custom_web 拷贝引用告警，提示重新放置），并收集 doc 引用到的
    ///  common03 资产所需游戏 bundle，供 SyncLevelInfo 注册依赖。
    ///  不再做任何拷贝/引用改写。返回空映射（保留签名兼容调用方）。</summary>
    public static Dictionary<string, string> EnsureDocCopies(string levelSet, LayoutDocumentDto doc)
    {
        var map = new Dictionary<string, string>();
        if (doc == null || doc.items == null)
            return map;
        _pendingDocBundles.Clear();
        foreach (var it in doc.items)
        {
            if (it == null)
                continue;
            ValidateDocGuid(it.prefabGuid);
            ValidateDocGuid(it.pseudoPrefabGuid);
            if (it.dispenser != null)
                ValidateDocGuid(it.dispenser.spawnerItemPrefabGuid);
            // 随机食材箱：候选食材的 bundle 也要进依赖收集（CustomStub.RandomCrate
            // 运行时按 bundleName 直读食材 prefab；菜谱保存重建 dependencies 时
            // 依赖 _pendingDocBundles 里这份收集，避免随机食材的 bundle 被挤掉）
            if (it.dispenser != null && it.dispenser.randomItemGuids != null)
                foreach (var g in it.dispenser.randomItemGuids)
                    ValidateDocGuid(g);
            if (it.foodSpawner != null && it.foodSpawner.attachmentPrefabGuids != null)
                foreach (var g in it.foodSpawner.attachmentPrefabGuids)
                    ValidateDocGuid(g);
            if (it.cookingUtensil != null && it.cookingUtensil.allowedIngredientGuids != null)
                foreach (var g in it.cookingUtensil.allowedIngredientGuids)
                    ValidateDocGuid(g);
            if (it.cleanPlateStack != null)
                ValidateDocGuid(it.cleanPlateStack.platePrefabGuid);
            if (it.meshWithMaterial != null)
                ValidateDocGuid(it.meshWithMaterial.pseudoPrefabGuid);
            if (it.soArray != null && it.soArray.pseudoPrefabGuids != null)
                foreach (var g in it.soArray.pseudoPrefabGuids)
                    ValidateDocGuid(g);
        }
        return map;
    }

    /// <summary>校验单个 doc 引用 guid：历史引用告警；common03 引用收集其游戏 bundle。</summary>
    private static void ValidateDocGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return; // 无法解析的 guid 由上层既有逻辑处理
        if (path.IndexOf("/Editor/LayoutEditor/Import/", StringComparison.Ordinal) >= 0)
        {
            LayoutEditorLog.LogWarning("[通用内容] 场景仍引用已迁移的 Import 源库资产（请删除并重新放置）: " + path);
            return;
        }
        if (path.IndexOf("/" + CustomDirName + "/", StringComparison.Ordinal) >= 0)
        {
            LayoutEditorLog.LogWarning("[通用内容] 场景仍引用已废弃的 custom_web 拷贝（请删除并重新放置为 common03 资产）: " + path);
            return;
        }
        // 依赖注册仅针对本地源库资产（common03 / common01 / common02）：
        // 这里的 wrapper prefab / SO 都指向游戏原 bundle（AddDependency 只收
        // StreamingAssets 实际存在的 bundle，多注册无害）。
        if (!IsLocalSourceAsset(path))
            return;
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
        if (so != null && !string.IsNullOrEmpty(so.bundleName))
        {
            _pendingDocBundles.Add(so.bundleName);
            return;
        }
        // guid 指向 wrapper prefab（新放置物品的 prefabGuid 即此形态）而非 SO：
        // 读其 PseudoPrefabStub 家族指向的 PseudoPrefabSO（含真实 bundleName），
        // 否则首次保存时 bundle 依赖缺失，编辑器 Reload 按缺失 bundle 实例化
        // 抛 KeyNotFoundException（被吞）→ 场景里该物品为空。
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return;
        foreach (var stub in prefab.GetComponents<PseudoPrefabStub>())
        {
            if (stub == null || stub.pseudoPrefabSO == null)
                continue;
            if (!string.IsNullOrEmpty(stub.pseudoPrefabSO.bundleName))
                _pendingDocBundles.Add(stub.pseudoPrefabSO.bundleName);
            var stackStub = stub as PseudoPrefabCleanPlateStackStub;
            if (stackStub != null && stackStub.platePseudoPrefabSO != null &&
                !string.IsNullOrEmpty(stackStub.platePseudoPrefabSO.bundleName))
                _pendingDocBundles.Add(stackStub.platePseudoPrefabSO.bundleName);
        }
    }

    /// <summary>保存菜谱（SetLevelRecipes）/场景写回后调用：注册 common03 相关 bundle 依赖。
    ///  不再做任何拷贝/引用改写。返回 0（保留签名兼容调用方）。</summary>
    public static int SyncLevelInfo(string levelSet, LevelInfoSO info)
    {
        if (info == null)
            return 0;
        EnsureWebDependencies(levelSet, info);
        return 0;
    }

    /// <summary>（已废弃）custom_web 全量同步机制随 common03 迁移退役，保留方法壳兼容调用方。</summary>
    public static int SyncAllWebContent(string levelSet)
    {
        return 0;
    }

    /// <summary>（已废弃）custom_web prefab 副本修复机制退役，保留方法壳兼容调用方。</summary>
    public static int RepairAllPrefabCopies(string levelSet)
    {
        return 0;
    }

    /// <summary>（已废弃）custom_web 副本 guid/分数修复机制退役，保留方法壳兼容调用方。</summary>
    public static void RepairLevelSet(string levelSet)
    {
    }

    // -------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------

    /// <summary>兼容旧调用：等同于 EnsureWebDependencies。</summary>
    public static void EnsureAllWebBundleDependencies(string levelSet, LevelInfoSO info)
    {
        EnsureWebDependencies(levelSet, info);
    }

    /// <summary>把关卡引用到的 common03 资产所需 bundle 加入 LevelInfoSO.dependencies：
    ///  common03 本体（stub 资产所在包）+ 各被引用资产 bundleName 指向的游戏原 bundle
    ///  （菜谱/食材/道具伪 SO 的真实 prefab 所在）+ 菜谱叶食材的游戏 bundle
    ///  + 场景 doc 引用收集到的 bundle（EnsureDocCopies）。
    ///  仅当 bundle 已存在于 StreamingAssets 才写入，缺失跳过（避免宿主
    ///  PseudoPrefabManager.GetAssetBundle 抛 KeyNotFoundException）——
    ///  common03 未构建（Tools/Build AssetBundles 未跑）时本体依赖自动缺席。</summary>
    public static void EnsureWebDependencies(string levelSet, LevelInfoSO info)
    {
        EnsureWebDependencies(levelSet, info, false);
    }

    /// <param name="replaceExisting">true：按当前 LevelInfo 引用重建 dependencies（保存菜谱覆盖写回）；
    ///  false：在已有 dependencies 上追加（场景写回）。</param>
    public static void EnsureWebDependencies(string levelSet, LevelInfoSO info, bool replaceExisting)
    {
        if (info == null)
            return;
        var deps = replaceExisting
            ? new List<string>()
            : new List<string>(info.dependencies ?? new string[0]);
        if (levelSet != null)
        {
            var customBundle = levelSet + "/custom_recipes";
            if (LayoutEditorCatalogApi.BundleFileExists(customBundle))
                AddDependency(deps, customBundle);
            foreach (var r in info.recipes ?? new ScriptableObject[0])
            {
                var custom = r as CustomRecipeSO;
                if (custom == null || custom.platingStepSO == null)
                    continue;
                AddDependency(deps, custom.platingStepSO.bundleName);
            }
        }
        AddDependency(deps, Common03BundleName);
        AddDependency(deps, CommonW1BundleName);
        foreach (var b in _pendingDocBundles)
            AddDependency(deps, b);

        if (info.recipes != null)
        {
            foreach (var r in info.recipes)
            {
                if (r == null)
                    continue;
                var rp = AssetDatabase.GetAssetPath(r);
                if (string.IsNullOrEmpty(rp) || !IsCommon03Asset(rp))
                    continue;
                // 菜谱资产自身指向的游戏 bundle（真实菜谱 prefab 所在）
                var pseudo = r as PseudoPrefabSO;
                if (pseudo != null && !string.IsNullOrEmpty(pseudo.bundleName))
                    AddDependency(deps, pseudo.bundleName);
                // 叶食材的游戏 bundle（食材未必出现在 allIngredients 中）
                var id = Path.GetFileNameWithoutExtension(rp);
                string step;
                string[] ings;
                if (LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings))
                {
                    foreach (var ing in ings)
                        AddWebAssetBundleDep(deps, "Ingredients", ing);
                }
            }
        }
        if (info.allIngredients != null)
        {
            foreach (var ing in info.allIngredients)
            {
                if (ing == null)
                    continue;
                var ip = AssetDatabase.GetAssetPath(ing);
                if (string.IsNullOrEmpty(ip) || !IsCommon03Asset(ip))
                    continue;
                if (!string.IsNullOrEmpty(ing.bundleName))
                    AddDependency(deps, ing.bundleName);
            }
        }
        if (info.includeRecipeMatchLists != null)
        {
            foreach (var ml in info.includeRecipeMatchLists)
            {
                if (ml == null)
                    continue;
                if (!string.IsNullOrEmpty(ml.bundleName))
                    AddDependency(deps, ml.bundleName);
            }
        }
        info.dependencies = deps.ToArray();
        EditorUtility.SetDirty(info);
    }

    /// <summary>在 common03/&lt;sub&gt; 下按 id 找资产并注册其 bundleName 依赖。</summary>
    private static void AddWebAssetBundleDep(List<string> deps, string sub, string id)
    {
        if (string.IsNullOrEmpty(id))
            return;
        var absRoot = AbsPath(Common03Root + "/" + sub);
        if (!Directory.Exists(absRoot))
            return;
        var files = Directory.GetFiles(absRoot, id + ".asset", SearchOption.AllDirectories);
        if (files.Length == 0)
            return;
        var rel = Common03Root + "/" + sub + files[0].Substring(absRoot.Length).Replace('\\', '/');
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(rel);
        if (so != null && !string.IsNullOrEmpty(so.bundleName))
            AddDependency(deps, so.bundleName);
    }

    private static void AddDependency(List<string> deps, string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
            return;
        if (deps.Contains(bundleName))
            return;
        if (!LayoutEditorCatalogApi.BundleFileExists(bundleName))
            return;
        deps.Add(bundleName);
    }

    // -------------------------------------------------------------
    // Web 内置菜谱：显式安装 / 移除（已废弃）
    // -------------------------------------------------------------

    /// <summary>（已废弃）通用内容改为 common03 直接引用，无需安装。</summary>
    public static string InstallRecipes(string levelSet, string[] ids)
    {
        return "通用内容已改为 common03 直接引用（静态 JSON 选取），无需安装。";
    }

    /// <summary>（已废弃）通用内容改为 common03 直接引用，无需卸载。</summary>
    public static WebRecipeUninstallResultDto UninstallRecipes(string levelSet, string[] ids)
    {
        return new WebRecipeUninstallResultDto
        {
            ok = false,
            error = "通用内容已改为 common03 直接引用（静态 JSON 选取），无需卸载。"
        };
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------

    private static string AbsPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../" + assetPath)).Replace('\\', '/');
    }
}
