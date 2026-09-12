using System.Collections;
using System.Collections.Generic;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 可推动大火锅装配器（CustomStub 版，接替原 LayoutRuntimePushablePot）。
    ///
    /// 背景：pushable_object.prefab 是纯逻辑载具（Rigidbody + 四向抓取点 + 碰撞，
    /// 无 Renderer/容器）；原版关卡里大锅作为独立道具放在载具上。宿主/游戏模组按
    /// stub 标记只会实例化载具本身——本组件在其出现后把「完整功能大锅」从 bundle
    /// 加载并挂到载具下（带 IngredientContainer/CookingHandler，可丢食材、可加热），
    /// 剥掉与载具冲突的组件（Rigidbody/Collider 中非 trigger 的交互组件/
    /// Interactable/AttachStation/EditorGridSnap——Collider 保留：锅的食材投放区
    /// 靠 trigger collider 接收食材）。
    ///
    /// 数据双通道：
    ///  - 权威通道：本组件 m_potSO + m_extraIngredientBundles/m_extraIngredientPaths；
    ///  - 载体通道：SpecificPseudoPrefabTag.prefabTag = "PushablePot|" +
    ///    "<bundle>:<path>;<bundle>:<path>;..."（第一项=大锅，其余=食材节点；
    ///    EntryPoint 场景自愈解析还原）；PseudoPrefabSOArray 槽 0 也持有大锅 SO
    ///    （commonW1 的 utensil_large_pot_01_pushable.prefab 自带）。
    ///
    /// 铁律（空气锅教训）：网络同步启动后（EntitySerialisationRegistry 已给载具/锅
    /// 挂上同步组件）绝不销毁重建——本组件只在「无 marker」时装配一次，幂等。
    /// </summary>
    public class PushablePot : MonoBehaviour
    {
        /// <summary>大锅 SO（common03/commonW1 静态大锅，bundle226）。为空时回落 soArray 槽 0 / m_potBundle+m_potPath。</summary>
        public PseudoPrefabSO m_potSO;

        /// <summary>大锅 bundle 直读路径（tag 载体自愈通道；三选一，SO 优先）。</summary>
        public string m_potBundle;
        public string m_potPath;

        /// <summary>web「锅具管理」填充的额外食材节点（bundle 内 OrderDefinitionNode
        /// 资产路径；与 m_extraIngredientPaths 一一对应）。空 = 默认许可表（DLC4 原版）。
        /// 非空 = 与默认表合并（2026-09-11 起，见 ApplyAllowedIngredients）。</summary>
        public string[] m_extraIngredientBundles = new string[0];
        public string[] m_extraIngredientPaths = new string[0];

        /// <summary>时间参数（web 锅具管理；0 = 原版默认，煮糊 = 2× 煮熟）。
        /// 装配大锅时经 UtensilTiming.ApplyValues 应用（cook 直接写字段，
        /// burn 特殊值走 Harmony 阈值接管）。</summary>
        public float m_cookTime;
        public float m_burnTime;

        /// <summary>容量（web 锅具管理「最多食材数」；<=0 = 原版默认，不写真实
        /// prefab 的 m_capacity）。装配时写 IngredientContainer.m_capacity。</summary>
        public int m_capacity;

        /// <summary>tag 载体前缀。</summary>
        public const string TagPrefix = "PushablePot|";

        private static bool s_loggedSelfCheck;
        private static bool s_lookupReflectionWarned;
        private static bool s_containerMissingWarned;
        private static bool s_orderNodeTypeWarned;

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield。</summary>
        private IEnumerator Start()
        {
            var inner = RunInner();
            while (true)
            {
                object current;
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                    current = hasNext ? inner.Current : null;
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[PushablePot] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Log("[PushablePot] 反射自检: PushableObject=" + (GameApi.PushableObjectType != null)
                    + " CookableContainer=" + (GameApi.CookableContainerType != null)
                    + " OrderToPrefabLookup=" + (GameApi.OrderToPrefabLookupType != null)
                    + " LookupNested=" + (GameApi.ContentPrefabLookupType != null));
            }

            while (true)
            {
                // 载具出现即装配（重开关卡后宿主重建载具，重新走一遍）
                var carrier = FindCarrier();
                if (carrier == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
                if (HasPotAssembled(carrier))
                {
                    // 已装配（含同步启动后的防线）——不重建，只监视载具失效
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                var potPrefab = LoadPotPrefab();
                if (potPrefab == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // 双装配竞态收口（2026-09-12 日志实证「装配完成×2」）：LoadPotPrefab 可
                // 耗时（bundle LoadAsset 数十 ms），期间另一装配器可能已完成装配——
                // Instantiate 前按跨程序集标记复查，晚到者直接转监视模式。
                if (HasPotAssembled(carrier))
                    continue;
                Assemble(carrier, potPrefab);
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>载具是否已有任一程序集的装配标记（CustomStub.PushableVoidFallTarget）。
        /// 编辑器里多个关卡集 stub 程序集共存：场景烘焙组件与本程序集自愈组件可能
        /// 同名不同类型（GetComponent&lt;本程序集类型&gt; 判不到对方），必须按 FullName
        /// 判定，否则两口锅叠在同一载具上（食材被两口锅分流 = 「偶尔放不进菜」）。</summary>
        private static bool HasPotAssembled(GameObject carrier)
        {
            if (carrier == null)
                return false;
            var comps = carrier.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c != null && c.GetType().FullName == "CustomStub.PushableVoidFallTarget")
                    return true;
            }
            return false;
        }

        /// <summary>子孙里找 PushableObject（游戏原生类型）= 宿主/模组生成的载具。</summary>
        private GameObject FindCarrier()
        {
            var pushables = GameApi.GetComponentsInChildren(gameObject, GameApi.PushableObjectType, true);
            for (int i = 0; i < pushables.Length; i++)
            {
                if (pushables[i] != null)
                    return pushables[i].gameObject;
            }
            return null;
        }

        private PseudoPrefabSO PotSO()
        {
            if (m_potSO != null)
                return m_potSO;
            // soArray 槽 0 = 大锅 SO（commonW1 prefab 自带载体）
            var soArray = GetComponent<PseudoPrefabSOArray>();
            if (soArray != null && soArray.pseudoPrefabSOs != null && soArray.pseudoPrefabSOs.Length > 0)
                return soArray.pseudoPrefabSOs[0];
            return null;
        }

        // 加载失败原因分流（各只报一次；连续失败每 1s 静默重试，见 RunInner）
        private static bool s_potSoMissingWarned;
        private static bool s_potBundleMissingWarned;
        private static bool s_potLoadNullWarned;
        // 编辑器宿主兜底每会话只试一次（见 LoadPotPrefabViaEditorHost 闸门注释）
        private static bool s_hostFallbackTried;

        private GameObject LoadPotPrefab()
        {
            string bundleName = null;
            string assetPath = null;
            var so = PotSO();
            if (so != null && !string.IsNullOrEmpty(so.bundleName) && !string.IsNullOrEmpty(so.assetPath))
            {
                bundleName = so.bundleName;
                assetPath = so.assetPath;
            }
            else if (!string.IsNullOrEmpty(m_potBundle) && !string.IsNullOrEmpty(m_potPath))
            {
                bundleName = m_potBundle;
                assetPath = m_potPath;
            }
            if (bundleName == null)
            {
                if (!s_potSoMissingWarned)
                {
                    s_potSoMissingWarned = true;
                    StubLog.LogWarn("[PushablePot] 大锅 SO 三通道皆空（m_potSO/soArray 槽 0/m_potBundle），无法装配: " + name);
                }
                return null;
            }
            try
            {
                var bundle = GameApi.GetAssetBundle(bundleName);
                if (bundle != null)
                {
                    var prefab = bundle.LoadAsset<GameObject>(assetPath);
                    if (prefab != null)
                        return prefab;
                }
                // 直接加载失败（bundle 未在全局列表 / LoadAsset 落空）：编辑器宿主兜底
                // 再试一次（宿主链含 DeInit/Init 全量重载恢复），真机自然跳过。
                // 闸门：仅网络同步未启动时、每会话最多一次——宿主 DeInit 会清掉全部
                // 伪 prefab 子物体（含载具），同步启动后调用 = 「空气锅」铁律事故。
                var viaHost = LoadPotPrefabViaEditorHost(so, bundleName, assetPath);
                if (viaHost != null)
                    return viaHost;
                if (bundle == null && !s_potBundleMissingWarned)
                {
                    s_potBundleMissingWarned = true;
                    StubLog.LogWarn("[PushablePot] bundle 未在已加载列表中找到: " + bundleName
                        + "（SO=" + (so != null ? so.name : "<null>") + "），持续重试: " + name);
                }
                else if (bundle != null && !s_potLoadNullWarned)
                {
                    s_potLoadNullWarned = true;
                    StubLog.LogWarn("[PushablePot] LoadAsset 返回 null: " + bundleName + "/" + assetPath
                        + "（bundle 内无该资产？SO 路径与 bundle 内容不符？）: " + name);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 大锅加载异常 " + bundleName + "/" + assetPath + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>编辑器宿主兜底加载：反射 LevelEditor.PseudoPrefabManager.LoadAsset
        ///（该类型只存在于编辑器宿主 AppDomain，真机 Find 返回 null 自然跳过）。
        /// 宿主版自带「LoadAsset 落空 → DeInit/Init 全量重载 bundle」恢复逻辑，
        /// 能救回编辑器 Play 里全局 bundle 列表与宿主 bundleDict 状态不一致的情况。</summary>
        private static GameObject LoadPotPrefabViaEditorHost(PseudoPrefabSO so, string bundleName, string assetPath)
        {
            if (so == null || s_hostFallbackTried)
                return null;
            // 同步启动后绝不触发宿主 DeInit/Init（会销毁载具等全部伪 prefab 子物体）
            if (GameApi.IsSynchronisationActive())
                return null;
            s_hostFallbackTried = true;
            try
            {
                var mgrType = GameApi.Find("LevelEditor.PseudoPrefabManager");
                if (mgrType == null)
                    return null;
                var m = mgrType.GetMethod("LoadAsset",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(PseudoPrefabSO) }, null);
                if (m == null)
                    return null;
                var prefab = m.Invoke(null, new object[] { so }) as GameObject;
                if (prefab != null)
                    StubLog.Log("[PushablePot] 大锅经编辑器宿主加载链兜底成功: " + bundleName);
                return prefab;
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 编辑器宿主兜底加载异常（忽略，走重试）: " + ex.Message);
                return null;
            }
        }

        private void Assemble(GameObject carrier, GameObject potPrefab)
        {
            try
            {
                var pot = (GameObject)Object.Instantiate(potPrefab);
                pot.name = potPrefab.name;
                pot.transform.SetParent(carrier.transform, false);
                pot.transform.localPosition = Vector3.zero;
                pot.transform.localRotation = Quaternion.identity;

                // 剥冲突组件：Rigidbody（物理由载具独占）、Interactable/AttachStation/
                // EditorGridSnap（交互走载具）。Collider 保留（食材投放 trigger 需要它）。
                StripComponent(pot, typeof(Rigidbody));
                StripByGameType(pot, GameApi.InteractableType);
                StripByGameType(pot, GameApi.EditorGridSnapType);
                StripByGameType(pot, GameApi.AttachStationType);

                ApplyAllowedIngredients(pot);
                ApplyCapacity(pot);

                // 时间参数（煮熟直接写 CookingHandler.m_cookingtime；特殊煮糊值挂
                // UtensilTiming 由 Harmony 前缀接管阈值）——须在同步启动前完成。
                UtensilTiming.ApplyValues(pot, m_cookTime, m_burnTime, 0f, 0f);

                // 空洞/水面坠落检测 marker（PushableVoidFall 只处理带此标记的载具）
                // + 主动注册进坠落检测缓存（v5：即时纳入，不等 ~1s 探测周期）
                var fallTarget = carrier.GetComponent<PushableVoidFallTarget>();
                if (fallTarget == null)
                    fallTarget = carrier.AddComponent<PushableVoidFallTarget>();
                PushableVoidFall.RegisterTarget(fallTarget);

                StubLog.Log("[PushablePot] 装配完成: " + name + " → 载具 " + carrier.name
                    + " + 大锅 " + pot.name + "（额外食材 " + (m_extraIngredientBundles != null ? m_extraIngredientBundles.Length : 0)
                    + "，cook=" + m_cookTime + " burn=" + m_burnTime + " cap=" + m_capacity + "）");
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 装配失败: " + name + "\n" + ex);
            }
        }

        private static void StripComponent(GameObject pot, System.Type type)
        {
            if (type == null)
                return;
            var comps = pot.GetComponentsInChildren(type, true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    Destroy(comps[i]);
            }
        }

        private static void StripByGameType(GameObject pot, System.Type type)
        {
            if (type == null)
                return;
            var comps = pot.GetComponentsInChildren(type, true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    Destroy(comps[i]);
            }
        }

        /// <summary>按额外食材配置重建锅的 CookableContainer.m_approvedContentsList
        /// （合并语义（2026-09-11 起）：保留默认许可表全部条目，extras 只补新节点。
        /// 旧版整体替换（对齐宿主 CookingUtensil.Setup），手选少量额外食材会把默认
        /// 火锅食材全部挡在锅外：被拒食材穿过纯 trigger 锅体掉到锅底被模型遮挡
        /// = 玩家视角「吞菜」。判重按节点引用相等（同 bundle 同源节点天然同实例）；
        /// 已在默认表里的节点保留默认条目（含正确 prefab 映射），新节点 prefab 兜底
        /// 取默认表首项（无专用漂浮模型可映射，与宿主 Setup 同款兜底）。
        /// 重建后把 extras 并入锅内漂浮食材视觉表（MergeExtrasIntoWokVisuals），
        /// 否则额外食材进锅不显示。空配置时保持原版许可表，什么都不动。</summary>
        private void ApplyAllowedIngredients(GameObject pot)
        {
            if (m_extraIngredientBundles == null || m_extraIngredientBundles.Length == 0)
                return;
            if (GameApi.CookableContainerType == null || GameApi.ApprovedContentsField == null
                || GameApi.OrderToPrefabLookupType == null || GameApi.ContentPrefabLookupType == null
                || GameApi.LookupArrayField == null || GameApi.LookupContentField == null
                || GameApi.LookupPrefabField == null)
            {
                // 配了额外食材但反射链断裂 = 许可表静默不生效，必须可见（只报一次）
                if (!s_lookupReflectionWarned)
                {
                    s_lookupReflectionWarned = true;
                    StubLog.LogWarn("[PushablePot] 额外食材已配置但许可表反射链缺失（CookableContainer="
                        + (GameApi.CookableContainerType != null) + " ApprovedContents=" + (GameApi.ApprovedContentsField != null)
                        + " Lookup=" + (GameApi.OrderToPrefabLookupType != null) + "），许可表保持原版: " + name);
                }
                return;
            }
            try
            {
                var container = GameApi.GetComponentInChildren(pot, GameApi.CookableContainerType);
                var oldLookup = container != null ? GameApi.ApprovedContentsField.GetValue(container) : null;
                if (oldLookup == null)
                {
                    if (!s_containerMissingWarned)
                    {
                        s_containerMissingWarned = true;
                        StubLog.LogWarn("[PushablePot] 大锅上找不到 CookableContainer/许可表，额外食材配置未生效: " + name);
                    }
                    return;
                }
                var oldArray = GameApi.LookupArrayField.GetValue(oldLookup) as System.Array;
                GameObject defaultPrefab = null;
                if (oldArray != null && oldArray.Length > 0 && GameApi.LookupPrefabField != null)
                    defaultPrefab = GameApi.LookupPrefabField.GetValue(oldArray.GetValue(0)) as GameObject;

                // 合并：默认条目原样保留（含原 prefab 映射），extras 只补新节点
                var entries = new List<object>();
                if (oldArray != null)
                {
                    for (int i = 0; i < oldArray.Length; i++)
                    {
                        var entry = oldArray.GetValue(i);
                        if (entry != null)
                            entries.Add(entry);
                    }
                }
                var extraEntries = new List<object>();
                for (int i = 0; i < m_extraIngredientBundles.Length; i++)
                {
                    var node = LoadNode(m_extraIngredientBundles[i],
                        i < m_extraIngredientPaths.Length ? m_extraIngredientPaths[i] : null);
                    if (node == null)
                        continue;
                    bool dup = false;
                    for (int e = 0; e < entries.Count; e++)
                    {
                        var content = GameApi.LookupContentField.GetValue(entries[e]) as UnityEngine.Object;
                        if (content != null && content == node)
                        {
                            dup = true;
                            break;
                        }
                    }
                    if (dup)
                        continue;
                    var inst = System.Activator.CreateInstance(GameApi.ContentPrefabLookupType);
                    GameApi.LookupContentField.SetValue(inst, node);
                    GameApi.LookupPrefabField.SetValue(inst, defaultPrefab);
                    entries.Add(inst);
                    extraEntries.Add(inst);
                }
                if (extraEntries.Count == 0)
                    return; // extras 全在默认表里 = 许可表与视觉表（默认即覆盖）都无需动

                var newLookup = ScriptableObject.CreateInstance(GameApi.OrderToPrefabLookupType);
                var newArray = System.Array.CreateInstance(GameApi.ContentPrefabLookupType, entries.Count);
                for (int i = 0; i < entries.Count; i++)
                    newArray.SetValue(entries[i], i);
                GameApi.LookupArrayField.SetValue(newLookup, newArray);
                GameApi.ApprovedContentsField.SetValue(container, newLookup);
                MergeExtrasIntoWokVisuals(container, extraEntries);
                StubLog.Log("[PushablePot] 食材许可表合并重建: " + name + "（默认 " + (entries.Count - extraEntries.Count)
                    + " + 新增 " + extraEntries.Count + "）");
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 食材许可表合并重建失败: " + name + " " + ex.Message);
            }
        }

        /// <summary>容量应用（web「最多食材数」）：>0 才写 IngredientContainer.m_capacity；
        /// <=0 = 原版默认（保持真实 prefab 值，绝不写 0）。</summary>
        private void ApplyCapacity(GameObject pot)
        {
            if (m_capacity <= 0)
                return;
            if (GameApi.IngredientContainerType == null || GameApi.IngredientCapacityField == null)
                return;
            var ic = GameApi.GetComponentInChildren(pot, GameApi.IngredientContainerType);
            if (ic == null)
                return;
            try
            {
                GameApi.IngredientCapacityField.SetValue(ic, m_capacity);
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 容量写入失败: " + name + " " + ex.Message);
            }
        }

        /// <summary>把新增食材条目并入锅体 WokCosmeticDecisions 的 raw/cooked 视觉
        /// 查找表（共享 bundle SO，并集语义：只增不删、判重幂等——视觉表只决定
        /// 「内容物能否被画出」，准入仍由各锅自己的许可表把关，并集不会越权显示；
        /// 多口锅 extras 不同也不会互相覆盖）。反射链缺失时静默跳过
        /// （视觉退回默认表，准入修复仍生效）。</summary>
        private static void MergeExtrasIntoWokVisuals(object container, List<object> extraEntries)
        {
            if (extraEntries == null || extraEntries.Count == 0)
                return;
            if (GameApi.CookableCosmeticsPrefabField == null || GameApi.WokCosmeticType == null
                || GameApi.WokPrefabLookupsField == null || GameApi.WokRawLookupField == null
                || GameApi.WokCookedLookupField == null || GameApi.LookupCacheFlagField == null
                || GameApi.LookupCacheMethod == null)
                return;
            var cosmeticsPrefab = GameApi.CookableCosmeticsPrefabField.GetValue(container) as GameObject;
            if (cosmeticsPrefab == null)
                return;
            var wok = cosmeticsPrefab.GetComponent(GameApi.WokCosmeticType);
            if (wok == null)
                return; // 非火锅锅具（无锅内漂浮食材视觉），不动
            // m_prefabLookups 是 private 嵌套 struct：装箱后读两个 lookup SO 引用
            // （只往 SO 里并条目，不改 struct 里的引用，无需写回 struct）。
            var lookups = GameApi.WokPrefabLookupsField.GetValue(wok);
            if (lookups == null)
                return;
            MergeEntriesIntoLookup(GameApi.WokRawLookupField.GetValue(lookups), extraEntries);
            MergeEntriesIntoLookup(GameApi.WokCookedLookupField.GetValue(lookups), extraEntries);
        }

        /// <summary>往 OrderToPrefabLookup（共享 SO）里并条目：判重后追加，追加后
        /// 重建简化节点缓存（新条目的 m_simplifiedAssembledContent 需重算，
        /// 否则 GetPrefabForNode 对新节点永不命中）。</summary>
        private static void MergeEntriesIntoLookup(object lookup, List<object> extraEntries)
        {
            if (lookup == null)
                return;
            var oldArray = GameApi.LookupArrayField.GetValue(lookup) as System.Array;
            int oldLen = oldArray != null ? oldArray.Length : 0;
            var toAdd = new List<object>();
            for (int i = 0; i < extraEntries.Count; i++)
            {
                var node = GameApi.LookupContentField.GetValue(extraEntries[i]) as UnityEngine.Object;
                if (node == null)
                    continue;
                bool dup = false;
                for (int e = 0; e < oldLen; e++)
                {
                    var existing = GameApi.LookupContentField.GetValue(oldArray.GetValue(e)) as UnityEngine.Object;
                    if (existing != null && existing == node)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup && !toAdd.Contains(extraEntries[i]))
                    toAdd.Add(extraEntries[i]);
            }
            if (toAdd.Count == 0)
                return;
            var newArray = System.Array.CreateInstance(GameApi.ContentPrefabLookupType, oldLen + toAdd.Count);
            for (int i = 0; i < oldLen; i++)
                newArray.SetValue(oldArray.GetValue(i), i);
            for (int i = 0; i < toAdd.Count; i++)
                newArray.SetValue(toAdd[i], oldLen + i);
            GameApi.LookupArrayField.SetValue(lookup, newArray);
            GameApi.LookupCacheFlagField.SetValue(lookup, false);
            GameApi.LookupCacheMethod.Invoke(lookup, null);
        }

        /// <summary>从 bundle 直读 OrderDefinitionNode（等价旧 PseudoPrefabManager.LoadAsset&lt;节点&gt;）。</summary>
        private static Object LoadNode(string bundleName, string assetPath)
        {
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetPath))
                return null;
            try
            {
                var bundle = GameApi.GetAssetBundle(bundleName);
                if (bundle == null)
                {
                    StubLog.LogWarn("[PushablePot] bundle 未加载，跳过食材节点 " + bundleName);
                    return null;
                }
                var nodeType = GameApi.Find("OrderDefinitionNode");
                if (nodeType == null)
                {
                    if (!s_orderNodeTypeWarned)
                    {
                        s_orderNodeTypeWarned = true;
                        StubLog.LogWarn("[PushablePot] OrderDefinitionNode 类型反射失败，额外食材节点无法加载");
                    }
                    return null;
                }
                return bundle.LoadAsset(assetPath, nodeType);
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 食材节点加载失败 " + bundleName + "/" + assetPath + ": " + ex.Message);
                return null;
            }
        }
    }
}
