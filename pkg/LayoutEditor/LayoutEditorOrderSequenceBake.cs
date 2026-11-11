using System;
using System.Collections.Generic;
using System.Text;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 订单顺序烘焙（4.0.0.18（原 4.7.0））：data/&lt;level&gt;/order_seq~/order_seq.json（前端编辑的
/// 权威配置）→ 场景载体（CustomStub.OrderSequenceSettings 组件【权威】+
/// SpecificPseudoPrefabTag 的 OrderSeq| 标量 tag【降级/自愈/特征标记】）。
///
/// 挂载点 = PseudoPrefabManagerStub 所在物体（关卡数据锚点，levelInfo 同处；
/// 订单配置是关卡级而非道具级）。菜单组/菜谱引用全部编码为 OrderDefinitionNode
/// uID（int）——运行时 Director 按 uID 在关卡 RecipeList 解析 Entry，与编辑器
/// LevelConfigSetup / 真机 OC2DIYLevel 两条 Entry 构建链同源（同一菜谱资产
/// uID 必然一致），场景载体不携带 SO 引用，tag 可无损往返。
///
/// 调用点（幂等：JSON 为权威，禁用/缺失/无效即清理场景残留）：
///  - SceneLayoutApplier 写回链路；
///  - LayoutEditorSetExporter 导出 prepare（SettleCurrentScene 后）。
/// 导出特征 orderseq 经 SetExporter 三表（CustomStubTagPrefixes /
/// StubTagFeatureMap / StubComponentFeatureMap）识别；RefreshStubTagsInActiveScene
/// 以组件为权威重打 tag。
/// </summary>
public static class LayoutEditorOrderSequenceBake
{
    public const string TagPrefix = "OrderSeq|";

    /// <summary>按 LevelInfo 烘焙当前打开场景。返回告警文本（null = 成功/无需烘焙）。
    /// 配置缺失 / enabled=false / 解析后无有效组 = 清理场景残留（原版随机出单）。</summary>
    public static string ApplyForOpenScene(LevelInfoSO info)
    {
        if (info == null)
            return null;
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return "[OrderSeq] 场景未打开，跳过订单顺序烘焙";

        var infoPath = AssetDatabase.GetAssetPath(info);
        var cfg = LayoutEditorLevelAdminApi.LoadLevelOrderSequence(infoPath);
        if (cfg == null || !cfg.enabled || cfg.groups == null || cfg.groups.Length == 0)
        {
            RemoveFromScene(scene);
            return null;
        }

        // 组 → uID（失效 guid 跳过并告警；整组失效跳过整组）
        var modes = new List<int>();
        var sizes = new List<int>();
        var batches = new List<int>();
        var uids = new List<int>();
        var warned = new HashSet<string>();
        for (int i = 0; i < cfg.groups.Length; i++)
        {
            var g = cfg.groups[i];
            if (g == null || g.recipeGuids == null || g.recipeGuids.Length == 0)
                continue;
            var groupUids = new List<int>();
            for (int j = 0; j < g.recipeGuids.Length; j++)
            {
                var guid = g.recipeGuids[j];
                int uid;
                if (!TryResolveRecipeUid(guid, warned, out uid))
                    continue;
                groupUids.Add(uid);
            }
            if (groupUids.Count == 0)
            {
                LayoutEditorLog.LogWarning("[OrderSeq] 第 " + (i + 1) + " 组无有效菜谱，整组跳过");
                continue;
            }
            modes.Add(g.mode == 1 ? 1 : 0);
            sizes.Add(groupUids.Count);
            batches.Add(g.mode == 1 ? Mathf.Clamp(g.batchSize, 0, 999) : 0);
            uids.AddRange(groupUids);
        }
        if (modes.Count == 0)
        {
            LayoutEditorLog.LogWarning("[OrderSeq] 订单顺序无有效组（guid 全部失效？），清理场景载体");
            RemoveFromScene(scene);
            return null;
        }

        var end = cfg.endBehavior == "pingpong" || cfg.endBehavior == "stop" ? cfg.endBehavior : "loop";

        // 挂载点：PseudoPrefabManagerStub（关卡数据锚点）
        var stub = UnityEngine.Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub == null)
            return "[OrderSeq] 场景未找到 PseudoPrefabManagerStub，跳过订单顺序烘焙";
        var go = stub.gameObject;

        // 组件（反射软接入：LayoutEditor 独立编译铁律，不硬引用 CustomStub 类型）
        var type = LayoutEditorStubIO.FindCustomStubType(string.Empty, "OrderSequenceSettings");
        if (type == null)
        {
            // tag 仍写入（导出特征 + 运行时 HealObject 自愈重建组件）
            LayoutEditorLog.LogWarning("[OrderSeq] WebCustomStubRuntime 程序集缺失，仅写 tag 载体"
                + "（运行时将按 tag 自愈；重新编译母本后写回一次即烘焙组件）");
        }
        else
        {
            var comp = go.GetComponent(type);
            if (comp == null)
                comp = Undo.AddComponent(go, type);
            else
                Undo.RecordObject(comp, "Layout Editor Order Sequence");
            SetField(comp, "m_endBehavior", end);
            SetField(comp, "m_groupModes", modes.ToArray());
            SetField(comp, "m_groupSizes", sizes.ToArray());
            SetField(comp, "m_groupBatches", batches.ToArray());
            SetField(comp, "m_recipeUids", uids.ToArray());
            EditorUtility.SetDirty(comp);
        }

        // tag 载体（全标量，可无损重建组件）
        var tag = go.GetComponent<SpecificPseudoPrefabTag>();
        if (tag == null)
            tag = Undo.AddComponent<SpecificPseudoPrefabTag>(go);
        else
            Undo.RecordObject(tag, "Layout Editor Order Sequence");
        tag.prefabTag = BuildTag(end, modes.ToArray(), sizes.ToArray(), batches.ToArray(), uids.ToArray());
        EditorUtility.SetDirty(tag);

        EditorSceneManager.MarkSceneDirty(scene);
        LayoutEditorLog.Log("[OrderSeq] 订单顺序烘焙: " + modes.Count + " 组，"
            + uids.Count + " 条菜谱引用，结束行为=" + end);
        return null;
    }

    /// <summary>清理场景中全部订单顺序载体（组件 + OrderSeq| tag）。
    /// 禁用/删除配置后的写回与导出都会调用（幂等）。</summary>
    public static void RemoveFromScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        var removed = 0;

        var type = LayoutEditorStubIO.FindCustomStubType(string.Empty, "OrderSequenceSettings");
        if (type != null)
        {
            var comps = UnityEngine.Object.FindObjectsOfType(type);
            foreach (var c in comps)
            {
                if (c == null)
                    continue;
                Undo.DestroyObjectImmediate(c);
                removed++;
            }
        }
        else
        {
            // 类型缺失：按组件类型名兜底（域里残留的旧烘焙件）
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().FullName == "CustomStub.OrderSequenceSettings")
                {
                    Undo.DestroyObjectImmediate(mb);
                    removed++;
                }
            }
        }

        foreach (var tag in UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>())
        {
            if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                continue;
            if (!tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                continue;
            Undo.RecordObject(tag, "Layout Editor Order Sequence Clear");
            tag.prefabTag = "";
            EditorUtility.SetDirty(tag);
            removed++;
        }
        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            LayoutEditorLog.Log("[OrderSeq] 订单顺序载体清理 × " + removed);
        }
    }

    /// <summary>场景中是否已烘焙订单载体（导出特征检测 / 烘焙跳过判定）。</summary>
    public static bool PresentInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        foreach (var tag in UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>())
        {
            if (tag != null && tag.prefabTag != null
                && tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                return true;
        }
        var type = LayoutEditorStubIO.FindCustomStubType(string.Empty, "OrderSequenceSettings");
        return type != null && UnityEngine.Object.FindObjectsOfType(type).Length > 0;
    }

    /// <summary>构建 OrderSeq| tag：e=&lt;end&gt;|m=&lt;mode,...&gt;|s=&lt;size,...&gt;|b=&lt;batch,...&gt;|u=&lt;uid,...&gt;</summary>
    public static string BuildTag(string end, int[] modes, int[] sizes, int[] batches, int[] uids)
    {
        var sb = new StringBuilder(TagPrefix);
        sb.Append("e=").Append(end).Append('|');
        sb.Append("m=").Append(JoinInts(modes)).Append('|');
        sb.Append("s=").Append(JoinInts(sizes)).Append('|');
        sb.Append("b=").Append(JoinInts(batches)).Append('|');
        sb.Append("u=").Append(JoinInts(uids));
        return sb.ToString();
    }

    private static string JoinInts(int[] list)
    {
        if (list == null)
            return "";
        var sb = new StringBuilder();
        for (int i = 0; i < list.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(list[i]);
        }
        return sb.ToString();
    }

    /// <summary>guid → OrderDefinitionNode.m_uID（与运行时 RecipeList Entry 的
    /// 匹配键同源：PseudoPrefabSORecipe = bundle 资产固有 uID，CustomRecipeSO =
    /// SO.uID 字段）。三级解析：①SO.asset 直引；②PseudoPrefabManager.LoadAsset
    /// （未 strip 时 bundle 已在管理器字典）；③写回/导出链全程 Prepare For
    /// Building（管理器 DeInit、bundleDict 空，LoadAsset 会 KeyNotFound）→ 直接
    /// AssetBundle.LoadFromFile(StreamingAssets/Windows/&lt;bundle&gt;) 自载自卸
    /// （该 bundle 不在管理器字典 = 管理器未使用，Unload(true) 安全）。</summary>
    private static bool TryResolveRecipeUid(string guid, HashSet<string> warned, out int uid)
    {
        uid = 0;
        if (string.IsNullOrEmpty(guid))
            return false;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            WarnOnce(warned, guid, "guid 无法解析");
            return false;
        }
        var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (!(so is PseudoPrefabSORecipe) && !(so is CustomRecipeSO))
        {
            WarnOnce(warned, guid, "非菜谱资产: " + path);
            return false;
        }
        var custom = so as CustomRecipeSO;
        if (custom != null)
        {
            uid = custom.uID;
            return true;
        }
        var recipe = so as PseudoPrefabSORecipe;
        if (recipe.asset != null)
        {
            var direct = recipe.asset as OrderDefinitionNode;
            if (direct != null)
            {
                uid = direct.m_uID;
                return true;
            }
        }
        try
        {
            var node = RecipeHelper.GetOrderDefinitionNode(so);
            if (node != null)
            {
                uid = node.m_uID;
                return true;
            }
        }
        catch (Exception)
        {
            // strip 态：PseudoPrefabManager.bundleDict 空（GetAssetBundle 直接索引
            // 抛 KeyNotFound）——落入手动 bundle 兜底。
        }
        return TryLoadUidFromBundleFile(recipe, warned, out uid);
    }

    /// <summary>strip 态兜底：直接从 StreamingAssets/Windows/&lt;bundle&gt; 加载
    /// OrderDefinitionNode 读 uID，读完立即 Unload(true)（bundle 不在管理器
    /// 字典中，自载自卸零污染；场景保存不受影响——资产不进场景）。</summary>
    private static bool TryLoadUidFromBundleFile(PseudoPrefabSORecipe recipe, HashSet<string> warned, out int uid)
    {
        uid = 0;
        if (recipe == null || string.IsNullOrEmpty(recipe.bundleName) || string.IsNullOrEmpty(recipe.assetPath))
        {
            WarnOnce(warned, recipe != null ? recipe.name : "?", "SO 三元组缺失（bundleName/assetPath）");
            return false;
        }
        var bundlePath = System.IO.Path.Combine(
            System.IO.Path.Combine(Application.streamingAssetsPath, "Windows"), recipe.bundleName);
        if (!System.IO.File.Exists(bundlePath))
        {
            WarnOnce(warned, recipe.bundleName, "bundle 文件不存在: " + bundlePath);
            return false;
        }
        AssetBundle bundle = null;
        try
        {
            bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                WarnOnce(warned, recipe.bundleName, "bundle 加载失败: " + bundlePath);
                return false;
            }
            var node = bundle.LoadAsset<OrderDefinitionNode>(recipe.assetPath);
            if (node == null)
            {
                WarnOnce(warned, recipe.bundleName, "OrderDefinitionNode 不在 bundle 内: " + recipe.assetPath);
                return false;
            }
            uid = node.m_uID;
            return true;
        }
        catch (Exception e)
        {
            WarnOnce(warned, recipe.bundleName, "bundle 直读 uID 异常: " + e.Message);
            return false;
        }
        finally
        {
            if (bundle != null)
                bundle.Unload(true);
        }
    }

    private static void WarnOnce(HashSet<string> warned, string guid, string reason)
    {
        if (warned != null && warned.Add(guid))
            LayoutEditorLog.LogWarning("[OrderSeq] 菜谱 guid " + guid + " 跳过: " + reason);
    }

    private static void SetField(Component c, string fieldName, object value)
    {
        if (c == null)
            return;
        var f = c.GetType().GetField(fieldName);
        if (f != null)
            f.SetValue(c, value);
    }
}
