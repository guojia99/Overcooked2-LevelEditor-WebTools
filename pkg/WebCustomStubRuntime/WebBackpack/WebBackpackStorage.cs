using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Web 储物背包：原生 Backpack 可背 + IngredientContainer 多格存储（联机同步）。
    /// 背上后可见内容宫格。交互模型（3.8.10）：
    ///  - 空手按拾取 = 取出队首【食材型】节点（FIFO，生成裸食材进手）；
    ///  - 携带空盘族按放置 = 队首节点注入盘中（组合型节点唯一取出方式）；
    ///  - 携带食材按放置 = 节点入包、销毁手中食材；
    ///  - 携带盘装内容按放置 = 内容节点搬入包（盘清空留手，盘永不进包）。
    /// 盘族容器只作介质；严格 FIFO（随机模式已移除）；容量 1/2/4/9/16。
    /// </summary>
    public class WebBackpackStorage : MonoBehaviour
    {
        public const string TagPrefix = "WebBackpack|";

        public int m_capacity = 4;

        public PseudoPrefabSO[] m_initialFillSOs = new PseudoPrefabSO[0];

        /// <summary>自定义菜谱初填（CustomRecipeSO 不在游戏 bundle，无法按 SO 加载；
        /// 写回侧自动注入 optionalRecipeMatchListItems，运行时按 uID 从关卡匹配表
        /// 解析节点）。</summary>
        public int[] m_initialFillCustomUids = new int[0];

        /// <summary>锅具同款内容宫格 UI（PseudoPrefabSO → bundle47 的
        /// IngredientsContentsUI 原版 prefab；commonW4 内置，写回自动赋值）。</summary>
        public PseudoPrefabSO m_contentsUiSO;

        /// <summary>支持的容量集（宫格列多行少：6=3×2、12=4×3、20=5×4、30=6×5）。
        /// 上限 30：原版 IngredientContainerMessage 子数仅 5 位（上限 31），
        /// 超过即线上截断——不做协议扩展（3.8.17 回退决策）。</summary>
        private static readonly int[] ValidCapacities = new[] { 1, 2, 4, 6, 9, 12, 16, 20, 25, 30 };

        private GameObject _backpackGo;
        private Component _serverIngredient;
        private bool _isServer;
        private bool _containersReady;

        /// <summary>食材型节点 uID → 裸物品 prefab（本机取出注册缓存）。</summary>
        private readonly Dictionary<int, GameObject> _ingredientPrefabByUid =
            new Dictionary<int, GameObject>();

        /// <summary>uID 解析失败负缓存（避免反复扫场景食材箱）。</summary>
        private readonly HashSet<int> _uidResolveFailed = new HashSet<int>();

        private readonly HashSet<string> _warnedOnce = new HashSet<string>();

        private static readonly List<WebBackpackStorage> s_pendingAssembly = new List<WebBackpackStorage>();
        private static readonly Dictionary<Component, WebBackpackStorage> s_dispenserMap =
            new Dictionary<Component, WebBackpackStorage>();

        internal static void OnSceneChanged()
        {
            s_pendingAssembly.Clear();
            s_dispenserMap.Clear();
            s_wornCount = 0;
        }

        internal static bool HasAnyWebBackpack
        {
            get { return s_dispenserMap.Count > 0; }
        }

        internal static WebBackpackStorage FindForDispenser(Component dispenserOrSpawner)
        {
            if (dispenserOrSpawner == null)
                return null;
            WebBackpackStorage wb;
            if (s_dispenserMap.TryGetValue(dispenserOrSpawner, out wb))
                return wb;
            // 注册时序 + 层级双兜底：先查 child 标记（背包被背起后 child 挂在厨师
            // 身上，父级查找必然落空），再退父级（未背起时有效）。
            var marker = dispenserOrSpawner.GetComponent<WebBackpackChild>();
            if (marker != null && marker.storage != null)
            {
                s_dispenserMap[dispenserOrSpawner] = marker.storage;
                return marker.storage;
            }
            wb = dispenserOrSpawner.GetComponentInParent<WebBackpackStorage>();
            if (wb != null && wb._backpackGo != null)
                s_dispenserMap[dispenserOrSpawner] = wb;
            return wb;
        }

        internal static void FlushPendingAssemblies()
        {
            for (int i = 0; i < s_pendingAssembly.Count; i++)
            {
                var wb = s_pendingAssembly[i];
                if (wb == null)
                    continue;
                wb.EnsureBackpackChild();
                wb.TryAssembleContainers();
            }
        }

        internal void EnsureBackpackChild()
        {
            if (_backpackGo != null)
                return;
            _backpackGo = FindBackpackChild();
            if (_backpackGo == null)
                _backpackGo = FindChildFromPseudoPrefab();
        }

        /// <summary>场景自愈时尽早装配（须在 ScanEntities 之前）。</summary>
        internal static void PrepareOnWrapper(GameObject wrapper, WebBackpackStorage wb)
        {
            if (wrapper == null || wb == null)
                return;
            if (!s_pendingAssembly.Contains(wb))
                s_pendingAssembly.Add(wb);
            wb.EnsureBackpackChild();
            wb.TryAssembleContainers();
            wb.WirePlacementIfReady();
        }

        internal void WirePlacementIfReady()
        {
            EnsureBackpackChild();
            if (_backpackGo != null)
                WireServerPlacementReferences(_backpackGo);
        }

        private void Awake()
        {
            if (!s_pendingAssembly.Contains(this))
                s_pendingAssembly.Add(this);
        }

        private IEnumerator Start()
        {
            EntryPoint.EnsureWebBackpackPatches();
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
                catch (Exception ex)
                {
                    StubLog.LogWarn("[WebBackpack] 协程异常: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            if (!s_pendingAssembly.Contains(this))
                s_pendingAssembly.Add(this);

            // 先找子背包再等扫描：容器必须在实体链接（LinkAllEntities）之前挂上，
            // 等扫描结束再找就晚了（同步器不会再补建）。
            float t0 = Time.realtimeSinceStartup;
            yield return WaitForChildBackpack();
            StubLog.Log("[WebBackpack] 子背包: " + (_backpackGo != null ? _backpackGo.name : "<未找到>")
                + "（等待 " + (Time.realtimeSinceStartup - t0).ToString("F2") + "s，扫描中="
                + GameApi.IsEntityScanActive() + "）: " + name);
            TryAssembleContainers();
            EnsureRuntimeSyncComponents();

            float t1 = Time.realtimeSinceStartup;
            while (GameApi.IsEntityScanActive())
                yield return new WaitForSeconds(0.1f);
            float t2 = Time.realtimeSinceStartup;
            while (!GameApi.IsSynchronisationActive())
                yield return null;
            StubLog.Log("[WebBackpack] 同步就绪: " + name + "（等扫描 " + (t2 - t1).ToString("F2")
                + "s，等同步 " + (Time.realtimeSinceStartup - t2).ToString("F2") + "s）");

            _isServer = GameApi.IsServerMachine();
            if (_isServer)
                ApplyInitialFill();
            else
                RegisterClientFillPrefabs();
            RegisterDispenserMap();
            EnsureRuntimeSyncComponents();
            WebBackpackPlacementBridge.EnsureOn(_backpackGo, this);
            WebBackpackCosmeticClient.EnsureOn(_backpackGo, this);
            StubLog.Log("[WebBackpack] 就绪: " + name + " cap=" + NormalizeCapacity(m_capacity)
                + " 角色=" + (_isServer ? "服务端" : "客户端")
                + " 已存=" + ServerStoredCountForLog()
                + " 容器[Ingredient=" + HasComponentOnChild(GameApi.IngredientContainerType)
                + ",Placement=" + HasComponentOnChild(GameApi.PlacementContainerType)
                + ",SrvIng=" + HasComponentOnChild(GameApi.ServerIngredientContainerType)
                + ",SrvPlace=" + HasComponentOnChild(GameApi.ServerPlacementContainerType)
                + ",Dispenser=" + HasComponentOnChild(GameApi.Find("ServerBackpackDispenser"))
                + "]");

            // 1s 后复查（链接完成后的权威状态；早于此刻的读取可能仍在链接窗口内）
            yield return new WaitForSeconds(1f);
            StubLog.Log("[WebBackpack] 复查: " + name + " 已存=" + ServerStoredCountForLog()
                + " 容器[SrvIng=" + HasComponentOnChild(GameApi.ServerIngredientContainerType)
                + ",CliIng=" + HasComponentOnChild(GameApi.ClientIngredientContainerType)
                + ",SrvPlace=" + HasComponentOnChild(GameApi.ServerPlacementContainerType)
                + ",CliPlace=" + HasComponentOnChild(GameApi.Find("ClientPlacementContainer"))
                + "] worn=" + SafeIsWorn());

            // 初填失败条目限时重试（bundle 晚加载 / 瞬态 LoadAsset null）
            for (int attempt = 0; attempt < 10 && _pendingFillSOs.Count > 0; attempt++)
            {
                yield return new WaitForSeconds(3f);
                var sic = GetServerIngredient();
                if (sic == null)
                    break;
                int cap = NormalizeCapacity(m_capacity);
                for (int i = _pendingFillSOs.Count - 1; i >= 0; i--)
                {
                    if (InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) >= cap)
                    {
                        _pendingFillSOs.Clear();
                        break;
                    }
                    string fail;
                    GameObject prefab;
                    object node;
                    if (!TryResolveFillEntry(_pendingFillSOs[i], out prefab, out node, out fail))
                    {
                        if (attempt == 9)
                            StubLog.LogWarn("[WebBackpack] 初填重试 10 次仍失败，放弃: "
                                + _pendingFillSOs[i].prefabName + " —— " + fail);
                        continue;
                    }
                    GameApi.IngredientAddIngredientMethod.Invoke(sic, new object[] { node });
                    if (prefab != null)
                    {
                        int uid = NodeUidOf(node);
                        if (uid >= 0)
                            _ingredientPrefabByUid[uid] = prefab;
                        GameApi.RegisterSpawnable(_backpackGo, prefab);
                    }
                    StubLog.Log("[WebBackpack] 初填重试成功(第" + (attempt + 1) + "次): " + _pendingFillSOs[i].prefabName
                        + " → " + node.GetType().Name);
                    _pendingFillSOs.RemoveAt(i);
                }
            }
        }

        private bool HasComponentOnChild(Type t)
        {
            return _backpackGo != null && t != null && _backpackGo.GetComponent(t) != null;
        }

        private int ServerStoredCountForLog()
        {
            var sic = GetServerIngredient();
            return sic != null ? InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) : -1;
        }

        private bool SafeIsWorn()
        {
            try
            {
                return IsWorn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IEnumerator WaitForChildBackpack()
        {
            for (int i = 0; i < 120; i++)
            {
                var child = FindBackpackChild();
                if (child != null)
                {
                    _backpackGo = child;
                    yield break;
                }
                yield return null;
            }
            StubLog.LogWarn("[WebBackpack] 未找到子物体 Backpack 组件: " + name);
        }

        private GameObject FindBackpackChild()
        {
            var backpackType = GameApi.Find("Backpack");
            if (backpackType == null)
                return null;
            var comps = GetComponentsInChildren(backpackType, true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i] as Component;
                if (c != null && c.gameObject != gameObject)
                    return c.gameObject;
            }
            return null;
        }

        private GameObject FindChildFromPseudoPrefab()
        {
            var pseudoType = GameApi.Find("PseudoPrefab");
            if (pseudoType == null)
                pseudoType = GameApi.Find("LevelEditor.PseudoPrefab");
            if (pseudoType == null)
                return null;
            var pseudo = GetComponent(pseudoType);
            if (pseudo == null)
                return null;
            var field = pseudoType.GetField("childGameObject",
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
                return null;
            return field.GetValue(pseudo) as GameObject;
        }

        private void EnsureRuntimeSyncComponents()
        {
            if (_backpackGo != null)
                WireServerPlacementReferences(_backpackGo);
            _serverIngredient = null;
        }

        /// <summary>
        /// 运行时 AddComponent 的 ServerPlacementContainer 不会走 StartSynchronising，
        /// 必须在交互前补上 m_placementContainer / m_ingredientContainer 引用。
        /// </summary>
        internal static void WireServerPlacementReferences(GameObject backpackGo)
        {
            if (backpackGo == null || GameApi.ServerPlacementContainerType == null)
                return;
            var spc = backpackGo.GetComponent(GameApi.ServerPlacementContainerType);
            if (spc == null)
                return;
            var pcType = GameApi.PlacementContainerType;
            var sicType = GameApi.ServerIngredientContainerType;
            if (pcType == null || sicType == null)
                return;
            var pc = backpackGo.GetComponent(pcType);
            var sic = backpackGo.GetComponent(sicType);
            if (pc == null || sic == null)
                return;
            var t = GameApi.ServerPlacementContainerType;
            var fPc = t.GetField("m_placementContainer",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fIc = t.GetField("m_ingredientContainer",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fPc != null)
                fPc.SetValue(spc, pc);
            if (fIc != null)
                fIc.SetValue(spc, sic);
        }

        private void TryAssembleContainers()
        {
            if (_containersReady || _backpackGo == null)
                return;
            var go = _backpackGo;
            int cap = NormalizeCapacity(m_capacity);
            m_capacity = cap;

            bool addedIngredient = false;
            bool addedPlacement = false;
            if (GameApi.IngredientContainerType != null && go.GetComponent(GameApi.IngredientContainerType) == null)
            {
                go.AddComponent(GameApi.IngredientContainerType);
                addedIngredient = true;
            }
            if (GameApi.PlacementContainerType != null && go.GetComponent(GameApi.PlacementContainerType) == null)
            {
                go.AddComponent(GameApi.PlacementContainerType);
                addedPlacement = true;
            }

            if (GameApi.IngredientCapacityField != null)
            {
                var ic = go.GetComponent(GameApi.IngredientContainerType);
                if (ic != null)
                    GameApi.IngredientCapacityField.SetValue(ic, cap);
            }
            // 层级无关寻址标记：背包被背起后 child 会挂到厨师身上，
            // Harmony 前缀一律经本标记找回 storage。
            var marker = go.GetComponent<WebBackpackChild>();
            if (marker == null)
                marker = go.AddComponent<WebBackpackChild>();
            marker.storage = this;
            _containersReady = true;
            StubLog.Log("[WebBackpack] 容器装配: " + go.name + " cap=" + cap
                + "（新增 Ingredient=" + addedIngredient + ", Placement=" + addedPlacement
                + "；此际扫描中=" + GameApi.IsEntityScanActive()
                + "——扫描中=true 时机正确，链接会自动补同步器）");
        }

        private void RegisterDispenserMap()
        {
            if (_backpackGo == null)
                return;
            var dispType = GameApi.Find("ServerBackpackDispenser");
            if (dispType != null)
            {
                var d = _backpackGo.GetComponent(dispType);
                if (d != null)
                {
                    s_dispenserMap[(Component)d] = this;
                    StubLog.Log("[WebBackpack] dispenser 已注册: " + _backpackGo.name);
                }
                else
                {
                    StubLog.LogWarn("[WebBackpack] 子物体上无 ServerBackpackDispenser（链接未建？取出前缀将找不到本背包）: " + _backpackGo.name);
                }
            }
        }

        // -------------------------------------------------------------
        // worn 状态（每帧轮询，供宫格显隐与放置门控共用）
        // -------------------------------------------------------------

        private Behaviour _serverPlacementBehaviour;
        private static Type s_clientPlacementType;
        private Behaviour _clientPlacementBehaviour;
        private bool _gatingLogged;
        private int _childRetryCounter;
        private bool _wornCounted;
        private static int s_wornCount;

        /// <summary>场景内是否有背包正被背着（交互路由探针：worn 中发生的
        /// 非背包放置交互会被 Harmony 前缀打点，用于定位「按键了但没路由到背包」）。</summary>
        internal static bool AnyWorn
        {
            get { return s_wornCount > 0; }
        }

        private void Update()
        {
            try
            {
                if (_backpackGo == null)
                {
                    // 子背包晚实例化兜底（真机/重载路径）：每 ~0.5s 重找一次
                    if ((++_childRetryCounter & 31) == 0)
                    {
                        EnsureBackpackChild();
                        if (_backpackGo != null)
                        {
                            TryAssembleContainers();
                            StubLog.Log("[WebBackpack] Update 兜底找到子背包: " + _backpackGo.name
                                + "（扫描中=" + GameApi.IsEntityScanActive() + "）");
                        }
                    }
                    return;
                }
                ApplyPlacementGating(IsWorn());
            }
            catch (Exception ex)
            {
                WarnOnce("gating", "[WebBackpack] 放置门控异常: " + ex.Message);
            }
        }

        /// <summary>未背起时禁用 Placement 同步器：地面背包不把「放置」判定为
        /// 可交互（GetHighestPriority / GetControllingPlacementHandler_* 均跳过
        /// disabled 组件），玩家持物面向地面背包仍可正常丢弃。</summary>
        private void ApplyPlacementGating(bool worn)
        {
            if (_backpackGo == null)
                return;
            if (_serverPlacementBehaviour == null && GameApi.ServerPlacementContainerType != null)
                _serverPlacementBehaviour = _backpackGo.GetComponent(GameApi.ServerPlacementContainerType) as Behaviour;
            if (_clientPlacementBehaviour == null)
            {
                if (s_clientPlacementType == null)
                    s_clientPlacementType = GameApi.Find("ClientPlacementContainer");
                if (s_clientPlacementType != null)
                    _clientPlacementBehaviour = _backpackGo.GetComponent(s_clientPlacementType) as Behaviour;
            }
            if (!_gatingLogged
                && (_serverPlacementBehaviour != null || _clientPlacementBehaviour != null))
            {
                _gatingLogged = true;
                StubLog.Log("[WebBackpack] 门控就绪: " + _backpackGo.name
                    + "（SrvPlace=" + (_serverPlacementBehaviour != null) + " CliPlace=" + (_clientPlacementBehaviour != null)
                    + "，初始 worn=" + worn + " → enabled=" + worn + "）");
            }
            if (_serverPlacementBehaviour != null && _serverPlacementBehaviour.enabled != worn)
            {
                _serverPlacementBehaviour.enabled = worn;
                StubLog.Log("[WebBackpack] 门控: SrvPlace.enabled=" + worn + "（worn 变更）");
            }
            if (_clientPlacementBehaviour != null && _clientPlacementBehaviour.enabled != worn)
            {
                _clientPlacementBehaviour.enabled = worn;
                StubLog.Log("[WebBackpack] 门控: CliPlace.enabled=" + worn + "（worn 变更）");
            }
            if (worn != _wornCounted)
            {
                _wornCounted = worn;
                s_wornCount += worn ? 1 : -1;
            }
        }

        internal bool IsWorn()
        {
            EnsureBackpackChild();
            if (_backpackGo == null)
                return false;
            if (IsAttachedViaPhysicalAttachment())
                return true;
            return IsParentedToChef();
        }

        private bool IsAttachedViaPhysicalAttachment()
        {
            var spaType = GameApi.Find("ServerPhysicalAttachment");
            if (spaType != null)
            {
                var spa = _backpackGo.GetComponent(spaType);
                if (spa != null)
                {
                    var m = spaType.GetMethod("IsAttached", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (m != null && (bool)m.Invoke(spa, null))
                        return true;
                }
            }
            var cpaType = GameApi.Find("ClientPhysicalAttachment");
            if (cpaType != null)
            {
                var cpa = _backpackGo.GetComponent(cpaType);
                if (cpa != null)
                {
                    var m = cpaType.GetMethod("IsAttached", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (m != null && (bool)m.Invoke(cpa, null))
                        return true;
                }
            }
            var attachType = GameApi.Find("IAttachment");
            if (attachType != null)
            {
                var attach = _backpackGo.GetComponent(attachType);
                if (attach != null)
                {
                    var isAttached = attachType.GetMethod("IsAttached",
                        BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (isAttached != null && (bool)isAttached.Invoke(attach, null))
                        return true;
                }
            }
            return false;
        }

        private bool IsParentedToChef()
        {
            var t = _backpackGo.transform.parent;
            if (t == null)
                return false;
            var playerType = GameApi.Find("PlayerControls");
            if (playerType != null && t.GetComponentInParent(playerType) != null)
                return true;
            var chefType = GameApi.Find("PlayerAnimationDecisions");
            if (chefType != null && t.GetComponentInParent(chefType) != null)
                return true;
            return false;
        }

        // -------------------------------------------------------------
        // 初填（服务端建内容；客户端预注册食材 prefab）
        // -------------------------------------------------------------

        private readonly List<PseudoPrefabSO> _pendingFillSOs = new List<PseudoPrefabSO>();
        private bool _fillDiagLogged;

        private void ApplyInitialFill()
        {
            var sic = GetServerIngredient();
            if (sic == null)
            {
                StubLog.LogWarn("[WebBackpack] 初填时无 ServerIngredientContainer: " + name);
                return;
            }
            int cap = NormalizeCapacity(m_capacity);
            int added = InvokeInt(sic, GameApi.IngredientGetContentsCountMethod);
            StubLog.Log("[WebBackpack] 初填开始: " + name + "（SO 条目="
                + (m_initialFillSOs != null ? m_initialFillSOs.Length : 0)
                + "，自定义 uID=" + (m_initialFillCustomUids != null ? m_initialFillCustomUids.Length : 0)
                + "，已有=" + added + "，cap=" + cap + "）");

            _pendingFillSOs.Clear();
            if (m_initialFillSOs != null)
            {
                for (int i = 0; i < m_initialFillSOs.Length && added < cap; i++)
                {
                    var so = m_initialFillSOs[i];
                    if (so == null)
                        continue;
                    string fail;
                    GameObject prefab;
                    object node;
                    if (TryResolveFillEntry(so, out prefab, out node, out fail))
                    {
                        GameApi.IngredientAddIngredientMethod.Invoke(sic, new object[] { node });
                        if (prefab != null)
                        {
                            int uid = NodeUidOf(node);
                            if (uid >= 0)
                                _ingredientPrefabByUid[uid] = prefab;
                            GameApi.RegisterSpawnable(_backpackGo, prefab);
                        }
                        added++;
                        StubLog.Log("[WebBackpack] 初填+" + so.prefabName + " → 节点 " + node.GetType().Name
                            + "（uid=" + NodeUidOf(node) + "，prefab=" + prefab.name
                            + "，已存 " + added + "/" + cap + "）");
                    }
                    else
                    {
                        _pendingFillSOs.Add(so);
                        StubLog.LogWarn("[WebBackpack] 初填暂缓: " + so.prefabName + " —— " + fail
                            + "（将重试）");
                    }
                }
            }

            if (m_initialFillCustomUids != null && m_initialFillCustomUids.Length > 0)
            {
                var allNodes = GameApi.GetAllOrderNodes();
                for (int u = 0; u < m_initialFillCustomUids.Length && added < cap; u++)
                {
                    int uid = m_initialFillCustomUids[u];
                    var orderNode = FindOrderNodeByUid(allNodes, uid);
                    if (orderNode == null)
                    {
                        WarnOnce("cuid" + uid, "[WebBackpack] 自定义初填 uID=" + uid
                            + " 不在关卡匹配表（写回应注入 optionalRecipeMatchListItems），已跳过");
                        continue;
                    }
                    var assembled = ConvertOrderNode(orderNode);
                    if (assembled == null)
                        continue;
                    GameApi.IngredientAddIngredientMethod.Invoke(sic, new object[] { assembled });
                    added++;
                    StubLog.Log("[WebBackpack] 自定义初填+uid=" + uid + " → 节点 "
                        + assembled.GetType().Name + "（已存 " + added + "/" + cap + "）");
                }
            }
            StubLog.Log("[WebBackpack] 初填完成: " + name + "（已存 " + added + "/" + cap
                + "，待重试 " + _pendingFillSOs.Count + " 条）");
        }

        /// <summary>初填条目解析（分步诊断）：bundle → prefab → 节点，任何一步失败
        /// 给出可读原因（宿主 PseudoPrefabManager.LoadAsset 对「bundle 已加载但
        /// LoadAsset 为 null」的瞬态有自己的全量重载兜底；此处先重试再判死）。</summary>
        private bool TryResolveFillEntry(PseudoPrefabSO so, out GameObject prefab, out object node, out string failReason)
        {
            prefab = null;
            node = null;
            failReason = null;
            // common04 / custom_content：PseudoPrefabSO.asset 直引优先。
            var direct = GameApi.GetPseudoPrefabDirectAsset(so);
            if (direct != null)
            {
                prefab = direct as GameObject;
                if (prefab != null)
                {
                    node = NodeFromPrefab(prefab);
                    if (node != null)
                        return true;
                    failReason = "prefab 无 IOrderDefinition: " + prefab.name;
                    return false;
                }
                node = ConvertOrderNode(direct);
                if (node != null)
                    return true;
            }
            var bundle = GameApi.GetAssetBundle(so.bundleName);
            if (bundle == null)
            {
                failReason = "bundle " + so.bundleName + " 未加载";
                return false;
            }
            try
            {
                prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            }
            catch (Exception ex)
            {
                failReason = "LoadAsset 异常: " + ex.Message;
                return false;
            }
            if (prefab == null)
            {
                // 菜谱/中间产物 SO：assetPath 指向订单节点资产（无 GameObject 属预期）
                node = NodeFromSoOrderAsset(so);
                if (node != null)
                    return true;
                failReason = "bundle 已加载但 prefab 为空（瞬态未就绪/路径失效）: " + so.assetPath;
                return false;
            }
            node = NodeFromPrefab(prefab);
            if (node == null)
            {
                failReason = "prefab 无 IOrderDefinition（煤块/冰块等特殊物不可入包）: " + prefab.name;
                return false;
            }
            return true;
        }

        /// <summary>客户端：初填里的食材 prefab 预注册到本地 spawnable 表
        /// （客户端按 spawner 本地注册表解析生成 ID）。</summary>
        private void RegisterClientFillPrefabs()
        {
            if (m_initialFillSOs == null || _backpackGo == null)
                return;
            int registered = 0;
            for (int i = 0; i < m_initialFillSOs.Length; i++)
            {
                var so = m_initialFillSOs[i];
                if (so == null)
                    continue;
                var prefab = LoadPrefabFromPseudoSo(so);
                if (prefab == null)
                    continue;
                var node = NodeFromPrefab(prefab);
                if (node == null)
                    continue;
                int uid = NodeUidOf(node);
                if (uid >= 0)
                    _ingredientPrefabByUid[uid] = prefab;
                if (GameApi.RegisterSpawnable(_backpackGo, prefab))
                    registered++;
            }
            StubLog.Log("[WebBackpack] 客户端初填预注册: " + registered + " 个 prefab");
        }

        /// <summary>客户端内容变化（宫格轮询发现）：为新增食材型节点解析并注册
        /// prefab，覆盖运行期放入的食材（取出消息到达前完成注册）。</summary>
        internal void OnClientContentsChangedForRegistration(Array contents)
        {
            if (contents == null || _backpackGo == null)
                return;
            for (int i = 0; i < contents.Length; i++)
            {
                var node = contents.GetValue(i);
                if (node == null || !IsBareHandNodeType(node))
                    continue;
                EnsureRegisteredForNode(node);
            }
        }

        /// <summary>确保食材型节点的裸物品已解析 + 已注册（幂等；负缓存防重扫）。</summary>
        private void EnsureRegisteredForNode(object node)
        {
            int uid = NodeUidOf(node);
            if (uid < 0)
                return;
            if (_ingredientPrefabByUid.ContainsKey(uid) || _uidResolveFailed.Contains(uid))
                return;
            var prefab = ResolveIngredientItemPrefab(uid);
            if (prefab == null)
            {
                _uidResolveFailed.Add(uid);
                WarnOnce("uid" + uid, "[WebBackpack] 节点 uID=" + uid + " 无法解析食材 prefab（场景无对应食材箱），空手取出将不可用");
                return;
            }
            _ingredientPrefabByUid[uid] = prefab;
            GameApi.RegisterSpawnable(_backpackGo, prefab);
        }

        /// <summary>放入路径的精化注册：原版整料与切碎共享节点 uid，按【携带物
        /// 是否带 WorkableItem】匹配食材箱链成员（整料=带 Workable 的链级，
        /// 切碎=不带的下一级）——切好的进、切好的出。</summary>
        private void EnsureRegisteredForNodeFromItem(object node, GameObject item)
        {
            int uid = NodeUidOf(node);
            if (uid < 0)
                return;
            GameObject prefab = null;
            if (item != null && GameApi.WorkableItemType != null)
            {
                bool itemWorkable = item.GetComponent(GameApi.WorkableItemType) != null;
                var crates = GameObject.FindGameObjectsWithTag("Crate");
                GameObject firstMatch = null;
                for (int i = 0; i < crates.Length && prefab == null; i++)
                {
                    var sp = crates[i].GetComponent(GameApi.PickupItemSpawnerType);
                    if (sp == null || GameApi.PickupItemPrefabField == null)
                        continue;
                    var cur = GameApi.PickupItemPrefabField.GetValue(sp) as GameObject;
                    int hops = 0;
                    while (cur != null && hops++ < 8)
                    {
                        if (PrefabNodeUid(cur) == uid)
                        {
                            if (firstMatch == null)
                                firstMatch = cur;
                            bool curWorkable = cur.GetComponent(GameApi.WorkableItemType) != null;
                            if (curWorkable == itemWorkable)
                            {
                                prefab = cur;
                                break;
                            }
                        }
                        var w = cur.GetComponent(GameApi.WorkableItemType);
                        cur = w != null && GameApi.WorkableNextPrefabField != null
                            ? GameApi.WorkableNextPrefabField.GetValue(w) as GameObject
                            : null;
                    }
                }
                if (prefab == null)
                    prefab = firstMatch;
            }
            if (prefab == null)
            {
                EnsureRegisteredForNode(node);
                return;
            }
            _ingredientPrefabByUid[uid] = prefab;
            _uidResolveFailed.Remove(uid);
            GameApi.RegisterSpawnable(_backpackGo, prefab);
            StubLog.Log("[WebBackpack] 放入精化注册: uid=" + uid + " → " + prefab.name
                + "（按携带物形态匹配）");
        }

        /// <summary>uID → 裸食材 prefab：先查初填缓存，再扫场景食材箱
        /// （PickupItemSpawner.m_itemPrefab + WorkableItem 切分链逐级比对，
        /// 同 GameUtils.GetIngredientCrates 的原版范例）。</summary>
        private GameObject ResolveIngredientItemPrefab(int uid)
        {
            if (uid < 0)
                return null;
            GameObject cached;
            if (_ingredientPrefabByUid.TryGetValue(uid, out cached))
                return cached;
            var crates = GameObject.FindGameObjectsWithTag("Crate");
            for (int i = 0; i < crates.Length; i++)
            {
                var sp = crates[i].GetComponent(GameApi.PickupItemSpawnerType);
                if (sp == null || GameApi.PickupItemPrefabField == null)
                    continue;
                var cur = GameApi.PickupItemPrefabField.GetValue(sp) as GameObject;
                int hops = 0;
                while (cur != null && hops++ < 8)
                {
                    if (PrefabNodeUid(cur) == uid)
                    {
                        StubLog.Log("[WebBackpack] 食材箱解析: uid=" + uid + " → " + cur.name
                            + "（来自 " + crates[i].name + "）");
                        return cur;
                    }
                    var w = cur.GetComponent(GameApi.WorkableItemType);
                    cur = w != null && GameApi.WorkableNextPrefabField != null
                        ? GameApi.WorkableNextPrefabField.GetValue(w) as GameObject
                        : null;
                }
            }
            StubLog.Log("[WebBackpack] 食材箱解析: uid=" + uid + " 未命中（场景 Crate 数="
                + crates.Length + "）");
            return null;
        }

        // -------------------------------------------------------------
        // 取出 / 放入
        // -------------------------------------------------------------

        internal bool CanDispense()
        {
            if (!_isServer || !IsWorn())
                return false;
            var sic = GetServerIngredient();
            if (sic == null)
                return false;
            return InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) > 0;
        }

        /// <summary>空手取出：仅队首食材型节点；组合型（成品菜/中间产物）需空盘接。</summary>
        internal bool TryDispense(object carrier, object directionXZ)
        {
            if (!_isServer || !IsWorn())
            {
                StubLog.Log("[WebBackpack] 取出拒绝: 非" + (_isServer ? "服务端" : "") + "worn=" + SafeIsWorn() + ": " + name);
                return false;
            }
            var sic = GetServerIngredient();
            if (sic == null)
            {
                StubLog.Log("[WebBackpack] 取出拒绝: 无 ServerIngredientContainer（容器未在链接前装配？）: " + name);
                return false;
            }
            int stored = InvokeInt(sic, GameApi.IngredientGetContentsCountMethod);
            if (stored <= 0)
            {
                StubLog.Log("[WebBackpack] 取出拒绝: 背包为空: " + name);
                return false;
            }
            var head = InvokeObj(sic, GameApi.IngredientGetContentsElementMethod, 0);
            if (head == null)
            {
                StubLog.Log("[WebBackpack] 取出拒绝: 队首节点为 null（stored=" + stored + "）: " + name);
                return false;
            }
            if (!IsBareHandNodeType(head))
            {
                StubLog.Log("[WebBackpack] 取出拒绝: 队首为组合型 " + head.GetType().Name + "（需空盘接）: " + name);
                return false;
            }
            int uid = NodeUidOf(head);
            GameObject prefab = uid >= 0 ? ResolveIngredientItemPrefab(uid) : null;
            if (prefab == null)
            {
                WarnOnce("disp" + uid, "[WebBackpack] 队首食材 uID=" + uid
                    + " 无 prefab 来源，空手取出不可用（可用空盘接）");
                return false;
            }
            var spawned = GameApi.ServerSpawnPrefab(_backpackGo, prefab);
            if (spawned == null)
            {
                StubLog.LogWarn("[WebBackpack] 取出拒绝: ServerSpawnPrefab 失败（prefab=" + prefab.name + "）: " + name);
                return false;
            }
            GameApi.IngredientRemoveIngredientMethod.Invoke(sic, new object[] { 0 });
            CarryItemReflect(carrier, spawned);
            _backpackGo.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
            StubLog.Log("[WebBackpack] 取出成功: " + prefab.name + "（uid=" + uid + "，余 " + (stored - 1) + "）");
            return true;
        }

        /// <summary>放置键统一入口（Harmony HandlePlacement 前缀）：
        /// 空盘接 / 空组装基底存本体 / 食材放入。成品菜（拼好的组装品、
        /// 盘装/容器内容）不支持放入（3.8.18 移除）。</summary>
        internal bool TryHandlePlacementCarrier(object carrier)
        {
            if (!_isServer || carrier == null || !IsWorn())
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 非" + (_isServer ? "服务端" : "") + "worn=" + SafeIsWorn() + ": " + name);
                return false;
            }
            var item = InspectCarried(carrier);
            if (item == null)
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 手上无物: " + name);
                return false;
            }

            var itemContainer = GameApi.ServerIngredientContainerType != null
                ? item.GetComponent(GameApi.ServerIngredientContainerType) as Component
                : null;
            int itemCount = itemContainer != null
                ? InvokeInt(itemContainer, GameApi.IngredientGetContentsCountMethod)
                : -1;
            bool isRealPlate = IsRealPlateItem(item);

            // A) 真盘族（盘子/杯子/马克杯/托盘，非组装基底）空盘 → 队首节点注入盘中（取出）。
            if (isRealPlate && itemContainer != null && itemCount == 0)
            {
                var ok = TryDispenseHeadIntoContainer(itemContainer);
                StubLog.Log("[WebBackpack] 放置·空盘接: " + (ok ? "成功" : "拒绝") + "（item=" + item.name + "）");
                return ok;
            }

            // 成品菜不再支持放入：拼好的组装品（面包皮+夹心）与盘装/容器内容一律拒绝
            if (itemContainer != null && itemCount > 0)
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 成品菜/容器内容不支持入包（item=" + item.name
                    + "，其内容=" + itemCount + "）");
                return false;
            }

            // B) 空组装基底（面包皮/卷饼皮）→ 存本体（平节点优先，避免组合型堵队首）
            if (itemContainer != null && IsAssemblyBaseItem(item))
            {
                var baseNode = PlainNodeFromComposite(OrderNodeFromCarriedItem(item));
                if (baseNode == null)
                    baseNode = NodeFromWorkableSource(item);
                if (baseNode == null)
                    baseNode = OrderNodeFromCarriedItem(item);
                if (baseNode != null)
                {
                    var ok = TryStoreNodeAndDestroyCarried(carrier, item, baseNode, "组装基底本体",
                        IsBareHandNodeType(baseNode));
                    if (!ok)
                        StubLog.Log("[WebBackpack] 放置拒绝: 基底存入失败（满/无容器）: " + item.name);
                    return ok;
                }
                StubLog.Log("[WebBackpack] 放置拒绝: 空组装基底无可用节点（item=" + item.name + "）");
                return false;
            }

            // C) 其它空容器（空锅等）→ 不可接/不可存
            if (itemContainer != null)
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 空容器不可接/不可存（item=" + item.name + "）");
                return false;
            }

            // D) 食材/源生物 → 节点入包 + 销毁手中物
            var node = OrderNodeFromCarriedItem(item);
            if (node == null)
                node = NodeFromWorkableSource(item);
            if (node != null)
                return TryStoreNodeAndDestroyCarried(carrier, item, node, null, true);
            StubLog.Log("[WebBackpack] 放置拒绝: 携带物无可用节点（item=" + item.name + "）");
            return false;
        }

        /// <summary>真盘族：有 Plate 组件，且非组装基底（无 WorkableItem、无
        /// BurgerBun/Burrito/Hotdog/Pizza 组装装饰组件）。面包皮/卷饼皮虽带
        /// Plate 组件但可组装/可切分，接收注入会把背包节点拼进皮里（实测）。</summary>
        internal static bool IsRealPlateItem(GameObject item)
        {
            if (item == null || GameApi.PlateType == null)
                return false;
            if (item.GetComponent(GameApi.PlateType) == null)
                return false;
            return !IsAssemblyBaseItem(item);
        }

        /// <summary>组装基底（面包皮/卷饼皮/热狗面包/披萨底）：带 WorkableItem
        /// 或组装装饰组件。有内容=拼好的组装品（成品菜，不支持入包）；
        /// 空=可存本体。</summary>
        internal static bool IsAssemblyBaseItem(GameObject item)
        {
            if (item == null)
                return false;
            if (GameApi.WorkableItemType != null && item.GetComponent(GameApi.WorkableItemType) != null)
                return true;
            string[] markers = { "BurgerBunCosmeticDecisions", "BurritoCosmeticDecisions",
                "HotdogCosmeticDecisions", "PizzaCosmeticDecisions" };
            for (int i = 0; i < markers.Length; i++)
            {
                var t = GameApi.Find(markers[i]);
                if (t != null && item.GetComponentInChildren(t, true) != null)
                    return true;
            }
            return false;
        }

        /// <summary>组合节点内仅含一个平节点时取该平节点（空面包皮的组装复合
        /// = [皮节点] → 存皮平节点，空手可取）；多子/非组合返回 null。</summary>
        internal static object PlainNodeFromComposite(object node)
        {
            if (node == null || GameApi.CompositeAssembledType == null
                || GameApi.CompositeCompositionField == null
                || !GameApi.CompositeAssembledType.IsInstanceOfType(node))
                return null;
            try
            {
                var comp = GameApi.CompositeCompositionField.GetValue(node) as Array;
                if (comp == null || comp.Length != 1)
                    return null;
                var only = comp.GetValue(0);
                return only != null && IsBareHandNodeType(only) ? only : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>落水（KillPlane 触发玩家/背包本体）：服务端清空背包内容。
        /// 背包随玩家落水应丢失全部存货（2026-10-13 需求）。</summary>
        internal void OnFellInWater()
        {
            if (!_isServer)
                return;
            var sic = GetServerIngredient();
            if (sic == null)
                return;
            int count = InvokeInt(sic, GameApi.IngredientGetContentsCountMethod);
            if (count <= 0)
                return;
            if (GameApi.IngredientEmptyMethod != null)
                GameApi.IngredientEmptyMethod.Invoke(sic, null);
            else
            {
                for (int i = 0; i < count; i++)
                    GameApi.IngredientRemoveIngredientMethod.Invoke(sic, new object[] { 0 });
            }
            _ingredientPrefabByUid.Clear();
            _uidResolveFailed.Clear();
            StubLog.Log("[WebBackpack] 落水清空: " + name + "（清除 " + count + " 格）");
        }

        /// <summary>E/组装 公共入包：节点写入 + 注册 + 销毁手中物 + 动画。
        /// directItem=true 时按携带物形态精化注册（切碎保持）。</summary>
        private bool TryStoreNodeAndDestroyCarried(object carrier, GameObject item, object node,
            string label, bool directItem)
        {
            var sic = GetServerIngredient();
            if (sic == null)
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 无 ServerIngredientContainer: " + name);
                return false;
            }
            if (InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) >= NormalizeCapacity(m_capacity))
            {
                StubLog.Log("[WebBackpack] 放置拒绝: 已满（cap=" + NormalizeCapacity(m_capacity) + "）");
                return false;
            }
            GameApi.IngredientAddIngredientMethod.Invoke(sic, new object[] { node });
            if (directItem && IsBareHandNodeType(node))
                EnsureRegisteredForNodeFromItem(node, item);
            else
                EnsureRegisteredForNode(node);
            if (GameApi.CarrierDestroyCarriedMethod != null)
                GameApi.CarrierDestroyCarriedMethod.Invoke(carrier, null);
            _backpackGo.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
            StubLog.Log("[WebBackpack] 放置·入包: " + item.name + (label != null ? "（" + label + "）" : "")
                + "（节点 " + node.GetType().Name
                + (IsBareHandNodeType(node) ? "" : "·组合") + "）");
            return true;
        }

        /// <summary>CanHandlePlacement 覆盖判定（服务端）：本类流程任一路径
        /// 可成功即放行放置交互（原版 CanCombine 链不认空盘，会误拒）。</summary>
        internal bool CanHandlePlacementCarrier(object carrier)
        {
            if (carrier == null || !IsWorn())
                return false;
            var item = InspectCarried(carrier);
            if (item == null)
                return false;
            var sic = GetServerIngredient();
            int stored = sic != null ? InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) : 0;

            var itemContainer = GameApi.ServerIngredientContainerType != null
                ? item.GetComponent(GameApi.ServerIngredientContainerType) as Component
                : null;
            bool isRealPlate = IsRealPlateItem(item);
            bool isAssembly = itemContainer != null && !isRealPlate && IsAssemblyBaseItem(item);
            bool ok;
            string path;
            if (itemContainer != null)
            {
                int itemCount = InvokeInt(itemContainer, GameApi.IngredientGetContentsCountMethod);
                if (itemCount == 0)
                {
                    // 真盘族空→接；组装基底空→存本体；其它空容器→拒
                    bool baseNode = isAssembly
                        && (OrderNodeFromCarriedItem(item) != null || NodeFromWorkableSource(item) != null);
                    ok = (isRealPlate && stored > 0) || baseNode;
                    path = "空容器(真盘=" + isRealPlate + ",组装基底=" + isAssembly
                        + ",可存本体=" + baseNode + ",stored=" + stored + ")";
                }
                else
                {
                    ok = false; // 成品菜/容器内容不支持入包（3.8.18 移除）
                    path = "成品菜拒绝(其内容=" + itemCount + ")";
                }
            }
            else
            {
                bool storable = CarriedItemHasOrder(item) || NodeFromWorkableSource(item) != null;
                ok = stored < NormalizeCapacity(m_capacity) && storable;
                path = "食材/源生物放入(storable=" + storable + ",stored=" + stored + ")";
            }
            StubLog.Log("[WebBackpack] 放置判定: " + (ok ? "放行" : "拒绝") + " ← " + path + "（item="
                + item.name + "）");
            return ok;
        }

        /// <summary>客户端 Allow 回调（ClientPlacementContainer.CanCombine 内调用）：
        /// 真实判定在服务端，这里宽松放行（worn + 携带物非空）。</summary>
        internal bool CanAcceptPlacementItem(GameObject item)
        {
            return item != null && IsWorn();
        }

        private bool TryDispenseHeadIntoContainer(Component itemContainer)
        {
            var sic = GetServerIngredient();
            if (sic == null)
            {
                StubLog.Log("[WebBackpack] 盘接拒绝: 无背包容器");
                return false;
            }
            if (InvokeInt(sic, GameApi.IngredientGetContentsCountMethod) <= 0)
            {
                StubLog.Log("[WebBackpack] 盘接拒绝: 背包为空");
                return false;
            }
            var head = InvokeObj(sic, GameApi.IngredientGetContentsElementMethod, 0);
            if (head == null)
            {
                StubLog.Log("[WebBackpack] 盘接拒绝: 队首节点为 null");
                return false;
            }
            if (!InvokeBool(itemContainer, GameApi.IngredientCanAddIngredientMethod, head))
            {
                StubLog.Log("[WebBackpack] 盘接拒绝: 目标容器拒收（CanAddIngredient=false，节点 "
                    + head.GetType().Name + "）");
                return false;
            }
            GameApi.IngredientRemoveIngredientMethod.Invoke(sic, new object[] { 0 });
            GameApi.IngredientAddIngredientMethod.Invoke(itemContainer, new object[] { head });
            InformOfInternalChange(itemContainer);
            _backpackGo.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
             StubLog.Log("[WebBackpack] 盘接成功: 节点 " + head.GetType().Name + " → "
                 + ((Component)itemContainer).gameObject.name);
             return true;
         }

        // -------------------------------------------------------------
        // 节点 / 载具反射工具
        // -------------------------------------------------------------

        private static GameObject InspectCarried(object carrier)
        {
            if (carrier == null)
                return null;
            var inspect = carrier.GetType().GetMethod("InspectCarriedItem",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (inspect == null)
                return null;
            try
            {
                return inspect.Invoke(carrier, null) as GameObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void CarryItemReflect(object carrier, GameObject spawned)
        {
            if (carrier == null || spawned == null)
                return;
            var carry = carrier.GetType().GetMethod("CarryItem",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(GameObject) }, null);
            if (carry == null)
                return;
            carry.Invoke(carrier, new object[] { spawned });
        }

        private static void InformOfInternalChange(object container)
        {
            var inform = container != null
                ? container.GetType().GetMethod("InformOfInternalChange",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                : null;
            if (inform == null)
                return;
            try
            {
                inform.Invoke(container, null);
            }
            catch (Exception)
            {
                // 容器自身广播失败不影响主流程
            }
        }

        /// <summary>找 IOrderDefinition 实现：GetComponent(接口Type) 对反射传入的
        /// 接口在部分宿主路径不稳——手动遍历组件按 IsInstanceOfType 判定（子物体
        /// 兜底同样手动扫描）。</summary>
        private static object FindOrderDefinition(GameObject item)
        {
            if (item == null)
                return null;
            var orderDefType = GameApi.Find("IOrderDefinition");
            if (orderDefType == null)
                return null;
            var comps = item.GetComponents(typeof(Component));
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c != null && orderDefType.IsInstanceOfType(c))
                    return c;
            }
            var childs = item.GetComponentsInChildren(typeof(Component), true);
            for (int i = 0; i < childs.Length; i++)
            {
                var c = childs[i];
                if (c != null && orderDefType.IsInstanceOfType(c))
                    return c;
            }
            return null;
        }

        /// <summary>GetOrderComposition() 原始节点（不做 Simpilfy——切碎/熟制包裹层
        /// 必须原样保全；原版整料与切碎共享节点 uid，形态信息只在 prefab 上）。</summary>
        private static object InvokeOrderComposition(object orderDef)
        {
            if (orderDef == null)
                return null;
            var getOrder = orderDef.GetType().GetMethod("GetOrderComposition",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (getOrder == null)
                return null;
            return getOrder.Invoke(orderDef, null);
        }

        private static object NodeFromPrefab(GameObject prefab)
        {
            return InvokeOrderComposition(FindOrderDefinition(prefab));
        }

        /// <summary>菜谱/中间产物 SO：assetPath 指向 bundle 内 OrderDefinitionNode
        /// 资产（orderdefinitions/*），LoadAsset 按类型取节点 → Convert().Simpilfy()。</summary>
        private static object NodeFromSoOrderAsset(PseudoPrefabSO so)
        {
            if (so == null || GameApi.OrderDefinitionNodeType == null || GameApi.NodeConvertMethod == null)
                return null;
            try
            {
                var direct = GameApi.GetPseudoPrefabDirectAsset(so);
                if (direct != null && GameApi.OrderDefinitionNodeType.IsInstanceOfType(direct))
                    return ConvertOrderNode(direct);
                if (string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                    return null;
                var bundle = GameApi.GetAssetBundle(so.bundleName);
                if (bundle == null)
                    return null;
                var orderNode = bundle.LoadAsset(so.assetPath, GameApi.OrderDefinitionNodeType);
                if (orderNode == null)
                    return null;
                return ConvertOrderNode(orderNode);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[WebBackpack] 菜谱节点加载异常 " + so.bundleName + "/" + so.assetPath + ": " + ex.Message);
                return null;
            }
        }

        private static object ConvertOrderNode(object orderNode)
        {
            if (orderNode == null)
                return null;
            // 原始节点（不 Simpilfy，保全切碎/熟制包裹层）
            return GameApi.NodeConvertMethod.Invoke(orderNode, null);
        }

        private static object FindOrderNodeByUid(object[] allNodes, int uid)
        {
            if (allNodes == null || GameApi.NodeUidField == null)
                return null;
            for (int i = 0; i < allNodes.Length; i++)
            {
                var n = allNodes[i];
                if (n == null)
                    continue;
                try
                {
                    var v = GameApi.NodeUidField.GetValue(n);
                    if (v is int && (int)v == uid)
                        return n;
                }
                catch (Exception)
                {
                    // 单项读取失败跳过
                }
            }
            return null;
        }

        /// <summary>食材型节点（可徒手取出）判定：IngredientAssembledNode 族
        /// （含切分中间阶段）；组合/熟制/搅拌节点需盘接。</summary>
        internal static bool IsBareHandNodeType(object node)
        {
            return node != null && GameApi.IngredientAssembledNodeType != null
                && GameApi.IngredientAssembledNodeType.IsInstanceOfType(node);
        }

        /// <summary>Assembled 节点 → 包裹的订单侧节点 uID（-1 = 非 Ingredient 型）。</summary>
        internal static int NodeUidOf(object assembledNode)
        {
            if (assembledNode == null || GameApi.IngredientAssembledOrderField == null
                || GameApi.NodeUidField == null)
                return -1;
            try
            {
                var orderNode = GameApi.IngredientAssembledOrderField.GetValue(assembledNode);
                if (orderNode == null)
                    return -1;
                var v = GameApi.NodeUidField.GetValue(orderNode);
                return v is int ? (int)v : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static int PrefabNodeUid(GameObject prefab)
        {
            if (prefab == null)
                return -1;
            if (GameApi.IngredientPropertiesType != null)
            {
                var props = prefab.GetComponent(GameApi.IngredientPropertiesType);
                if (props != null)
                {
                    var uid = NodeUidOf(InvokeOrderComposition(props));
                    if (uid >= 0)
                        return uid;
                }
            }
            // Workable 源生物（整蔬菜/面包皮）：源节点（对称于放入路径）
            return NodeUidOf(NodeFromWorkableSource(prefab));
        }

        private static readonly HashSet<string> s_orderDiagLogged = new HashSet<string>();

        private static bool CarriedItemHasOrder(GameObject item)
        {
            var orderDef = FindOrderDefinition(item);
            if (orderDef == null)
            {
                OrderDiagOnce(item, "无 IOrderDefinition（整肉/冰块/煤块等源生物，原版也不可入锅/装盘，需先加工）");
                return false;
            }
            var orderNode = InvokeOrderComposition(orderDef);
            if (orderNode == null)
            {
                OrderDiagOnce(item, "订单节点为 null（orderDef=" + orderDef.GetType().Name + "）");
                return false;
            }
            var assembledType = GameApi.Find("AssembledDefinitionNode");
            var nullNodeField = assembledType != null
                ? assembledType.GetField("NullNode", BindingFlags.Public | BindingFlags.Static)
                : null;
            if (nullNodeField != null)
            {
                var nullNode = nullNodeField.GetValue(null);
                if (Equals(orderNode, nullNode))
                {
                    OrderDiagOnce(item, "订单为空节点（如空盘）");
                    return false;
                }
            }
            return true;
        }

        private static void OrderDiagOnce(GameObject item, string reason)
        {
            var key = item != null ? item.name : "<null>";
            if (s_orderDiagLogged.Add(key))
                StubLog.Log("[WebBackpack] 不可入包: " + key + " —— " + reason);
        }

        private static object OrderNodeFromCarriedItem(GameObject item)
        {
            return InvokeOrderComposition(FindOrderDefinition(item));
        }

        /// <summary>Workable 源生物（整蔬菜/面包皮等，本体无订单节点）→ 源节点：
        /// 取其加工形态节点（nextPrefab 的 IOrderDefinition，即 CookedComposite）
        /// 的首个组成——vanilla 切分模型中 composition[0] 就是源物品自身节点
        /// （dump 实证：chopped 资产 = {m_composition:[源], m_cookingStep:Chop}）。</summary>
        internal static object NodeFromWorkableSource(GameObject item)
        {
            if (item == null || GameApi.WorkableItemType == null || GameApi.WorkableNextPrefabField == null)
                return null;
            var workable = item.GetComponent(GameApi.WorkableItemType);
            if (workable == null)
                return null;
            var next = GameApi.WorkableNextPrefabField.GetValue(workable) as GameObject;
            if (next == null)
                return null;
            var nextNode = NodeFromPrefab(next);
            if (nextNode == null)
                return null;
            if (GameApi.CompositeCompositionField != null
                && GameApi.CompositeAssembledType != null
                && GameApi.CompositeAssembledType.IsInstanceOfType(nextNode))
            {
                try
                {
                    var comp = GameApi.CompositeCompositionField.GetValue(nextNode) as Array;
                    if (comp != null && comp.Length > 0)
                    {
                        var source = comp.GetValue(0);
                        if (source != null && IsBareHandNodeType(source))
                            return source;
                    }
                }
                catch (Exception)
                {
                    // 回落：非组合加工型
                }
            }
            // 加工形态与源同节点（罕见）：直接用
            return IsBareHandNodeType(nextNode) ? nextNode : null;
        }

        private object GetServerIngredient()
        {
            if (_serverIngredient != null)
                return _serverIngredient;
            if (_backpackGo == null || GameApi.ServerIngredientContainerType == null)
                return null;
            _serverIngredient = _backpackGo.GetComponent(GameApi.ServerIngredientContainerType);
            return _serverIngredient;
        }

        /// <summary>宫格列数（列多行少：1→1、2/4→2、6/9→3、12/16→4、20/25→5、30→6）。</summary>
        public int GridCols()
        {
            int c = NormalizeCapacity(m_capacity);
            switch (c)
            {
                case 1:
                    return 1;
                case 2:
                case 4:
                    return 2;
                case 6:
                case 9:
                    return 3;
                case 12:
                case 16:
                    return 4;
                case 20:
                case 25:
                    return 5;
                default:
                    return 6;
            }
        }

        /// <summary>宫格行数（容量/列数，各档均为整除）。</summary>
        public int GridRows()
        {
            return NormalizeCapacity(m_capacity) / GridCols();
        }

        public static int NormalizeCapacity(int cap)
        {
            for (int i = 0; i < ValidCapacities.Length; i++)
            {
                if (ValidCapacities[i] == cap)
                    return cap;
            }
            // 历史兼容：曾支持 36（线上 >31 截断，3.8.17 收回）→ 归一到 30
            if (cap > 30)
                return 30;
            return 4;
        }

        private void WarnOnce(string key, string msg)
        {
            if (_warnedOnce.Add(key))
                StubLog.LogWarn(msg);
        }

        private static int InvokeInt(object target, MethodInfo method)
        {
            if (method == null || target == null)
                return 0;
            try
            {
                return (int)method.Invoke(target, null);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool InvokeBool(object target, MethodInfo method, params object[] args)
        {
            if (method == null || target == null)
                return false;
            try
            {
                return (bool)method.Invoke(target, args);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static object InvokeObj(object target, MethodInfo method, params object[] args)
        {
            if (method == null || target == null)
                return null;
            try
            {
                return method.Invoke(target, args);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>从 PseudoPrefabSO 的 bundleName/assetPath 加载食材 prefab。编辑器
        /// Play 优先走宿主 LevelEditor.PseudoPrefabManager.LoadAsset（自带「瞬态
        /// null → 全量重载重试」兜底，与食材箱/生成器同路径）；真机无该类型自动
        /// 回落直连 bundle.LoadAsset。</summary>
        private static Type s_hostPpmType;
        private static bool s_hostPpmSearched;
        private static MethodInfo s_hostLoadAssetMethod;

        internal static GameObject LoadPrefabFromPseudoSo(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            var direct = GameApi.GetPseudoPrefabDirectAsset(so) as GameObject;
            if (direct != null)
                return direct;
            if (string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                return null;
            var viaHost = LoadPrefabViaHostManager(so);
            if (viaHost != null)
                return viaHost;
            try
            {
                var bundle = GameApi.GetAssetBundle(so.bundleName);
                if (bundle == null)
                {
                    StubLog.Dbg("[WebBackpack] bundle 未加载: " + so.bundleName + " (" + so.prefabName + ")");
                    return null;
                }
                var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
                if (prefab == null)
                    StubLog.LogWarn("[WebBackpack] LoadAsset 为空: " + so.bundleName + "/" + so.assetPath);
                return prefab;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[WebBackpack] prefab 加载异常 " + so.bundleName + "/" + so.assetPath + ": " + ex.Message);
                return null;
            }
        }

        private static GameObject LoadPrefabViaHostManager(PseudoPrefabSO so)
        {
            try
            {
                if (!s_hostPpmSearched)
                {
                    s_hostPpmSearched = true;
                    s_hostPpmType = Type.GetType("LevelEditor.PseudoPrefabManager, Assembly-CSharp");
                    if (s_hostPpmType != null)
                    {
                        foreach (var m in s_hostPpmType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (m.Name != "LoadAsset" || m.IsGenericMethodDefinition)
                                continue;
                            var ps = m.GetParameters();
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(PseudoPrefabSO)
                                && m.ReturnType == typeof(GameObject))
                            {
                                s_hostLoadAssetMethod = m;
                                break;
                            }
                        }
                    }
                }
                if (s_hostLoadAssetMethod == null)
                    return null;
                return s_hostLoadAssetMethod.Invoke(null, new object[] { so }) as GameObject;
            }
            catch (Exception)
            {
                return null; // 宿主管理器不可用（真机）/加载异常 → 直连回落
            }
        }
    }
}
