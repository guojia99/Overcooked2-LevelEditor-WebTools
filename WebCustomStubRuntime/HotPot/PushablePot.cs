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
    ///
    /// 铁律二（2026-09-15 v14，联机 ID 错位事故）：**装配必须在网络实体扫描开始
    /// 【之前】完成**。实体 ID 是按扫描命中顺序发放的自增 FIFO，而
    /// LinkAllEntitiesToSynchronisationScripts 每 0.1s yield 一帧、逐类型现场
    /// GetComponentsInChildren——扫描窗口期内 Instantiate 出的锅会插进序列的随机位置，
    /// 主客机插入点不同 ⇒ 从该点起全部实体 ID 整体错位 ⇒ 客机完全不能动、
    /// 生成物没模型（且全程零报错）。为此有两道防线：
    ///  ① <see cref="FlushPendingAssemblies"/> 由 MultiplayerController.ScanEntities
    ///     的 Harmony 前缀调用，在发令时刻把所有锅【同步】装配完；
    ///  ② 协程 <see cref="RunInner"/> 在 GameApi.IsEntityScanActive() 为真时只等待、
    ///     绝不 Instantiate——即便前缀没装上，最坏也只是「这口锅晚到、自己残废」
    ///     （扫描结束后新增对象不影响已分配的 ID），不会再打崩整局联机。
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
        private static bool s_scanWaitLogged;
        private static bool s_lateAssembleWarned;

        /// <summary>场景切换清场（挂 EntryPoint.ResetSceneTickers）。
        /// 注意不复位 s_hostFallbackTried——编辑器宿主兜底按【每会话一次】设计
        /// （它会触发宿主 DeInit/Init 全量重载，不能每换一张图就再来一遍）。</summary>
        internal static void OnSceneChanged()
        {
            s_scanWaitLogged = false;
            s_lateAssembleWarned = false;
        }

        /// <summary>装配失败计数与告警去重（实例级，v12）。</summary>
        private int m_assembleFailures;
        private bool m_assembleFailLogged;

        /// <summary>本实例装配出来的大锅与其载具（v14 诊断用：实体注册自检）。</summary>
        private GameObject m_assembledPot;
        private GameObject m_assembledCarrier;

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield。</summary>
        private IEnumerator Start()
        {
            // 扫描前预装配补丁按需安装（幂等）。挂在 Start 而非只挂 HealScene：
            // 覆盖「场景里已烘焙组件但 tag 缺失」的对象；且 Start 远早于
            // ClientKitchenLoader 收到 GameState.ScanNetworkEntities，来得及。
            EntryPoint.EnsurePotAssemblyPatches();
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
                StubLog.Dbg("[PushablePot] 反射自检: PushableObject=" + (GameApi.PushableObjectType != null)
                    + " CookableContainer=" + (GameApi.CookableContainerType != null)
                    + " OrderToPrefabLookup=" + (GameApi.OrderToPrefabLookupType != null)
                    + " LookupNested=" + (GameApi.ContentPrefabLookupType != null));
            }

            while (true)
            {
                // 【扫描期硬闸，v14】正在分配网络实体 ID 时绝不新建对象——
                // 中途插对象 = 主客机实体序列分叉 = 全局 ID 错位（整局联机报废）。
                // 扫描结束后再装配只影响这口锅自己，代价可接受。
                if (GameApi.IsEntityScanActive())
                {
                    if (!s_scanWaitLogged)
                    {
                        s_scanWaitLogged = true;
                        StubLog.Dbg("[PushablePot] 网络实体扫描进行中，装配暂停（避免实体 ID 错位）: " + name);
                    }
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }

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

                var potLoad = LoadPotPrefabAsync();
                while (potLoad.MoveNext())
                    yield return potLoad.Current;
                var potPrefab = m_loadedPotPrefab;
                if (potPrefab == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // 双装配竞态收口（2026-09-12 日志实证「装配完成×2」）：LoadPotPrefab 可
                // 耗时（bundle LoadAsset 数十 ms；v12 改异步后等待窗口更长），期间另一
                // 装配器可能已完成装配——Instantiate 前按跨程序集标记复查，
                // 晚到者直接转监视模式。载具本身也可能已被销毁（重开关卡），一并复查。
                if (carrier == null || HasPotAssembled(carrier))
                    continue;
                // 扫描期硬闸复查（v14）：异步加载横跨若干帧，期间扫描可能刚好开跑——
                // 顶部的门控挡不住这种情况，必须在 Instantiate 前再看一眼。
                if (GameApi.IsEntityScanActive())
                    continue;
                if (Assemble(carrier, potPrefab))
                {
                    m_assembleFailures = 0;
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    // 装配失败指数退避（v12）：原实现失败后 0.5s 就重试，且 catch 里
                    // 每次都打全量堆栈——持续性失败 = 2 条全栈日志/秒，永久刷屏。
                    m_assembleFailures++;
                    yield return new WaitForSeconds(AssembleBackoffSeconds(m_assembleFailures));
                }
            }
        }

        /// <summary>装配失败退避：0.5 → 1 → 2 → 5s 封顶。</summary>
        private static float AssembleBackoffSeconds(int failures)
        {
            if (failures <= 1)
                return 0.5f;
            if (failures == 2)
                return 1f;
            if (failures == 3)
                return 2f;
            return 5f;
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

        /// <summary>异步加载大锅 prefab（v12）：同步 LoadAsset 一个完整大锅 prefab
        /// （网格/材质/多层子物体）在主线程上是几十 ms 级的一次性卡顿，而本方法跑在
        /// 关卡开局。改 LoadAssetAsync 把反序列化摊到多帧。
        /// 结果写 <see cref="m_loadedPotPrefab"/>（C#4 迭代器不能有返回值）。
        /// 失败语义与旧同步版逐条对齐：三通道皆空 / bundle 未加载 / LoadAsset 落空
        /// 各自 warn-once，调用方按退避重试。</summary>
        private GameObject m_loadedPotPrefab;

        private IEnumerator LoadPotPrefabAsync()
        {
            m_loadedPotPrefab = null;

            string bundleName;
            string assetPath;
            PseudoPrefabSO so;
            if (!ResolvePotSource(out bundleName, out assetPath, out so))
                yield break;

            // 发起请求（try 不能含 yield，拆两段）
            AssetBundle bundle = null;
            AssetBundleRequest request = null;
            try
            {
                bundle = GameApi.GetAssetBundle(bundleName);
                if (bundle != null)
                    request = bundle.LoadAssetAsync<GameObject>(assetPath);
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 大锅加载异常 " + bundleName + "/" + assetPath + ": " + ex.Message);
                yield break;
            }

            if (request != null)
            {
                yield return request;
                try
                {
                    m_loadedPotPrefab = request.asset as GameObject;
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[PushablePot] 大锅加载结果读取异常 " + bundleName + "/" + assetPath
                        + ": " + ex.Message);
                }
                if (m_loadedPotPrefab != null)
                    yield break;
            }

            // 直接加载失败（bundle 未在全局列表 / LoadAsset 落空）：编辑器宿主兜底
            // 再试一次（宿主链含 DeInit/Init 全量重载恢复），真机自然跳过。
            m_loadedPotPrefab = LoadPotPrefabFallback(so, bundle, bundleName, assetPath);
        }

        /// <summary>解析大锅来源三通道（SO → soArray 槽 0 → tag 载体字段）。
        /// 返回 false = 三通道皆空（已 warn-once）。</summary>
        private bool ResolvePotSource(out string bundleName, out string assetPath, out PseudoPrefabSO so)
        {
            bundleName = null;
            assetPath = null;
            so = PotSO();
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
            if (bundleName != null)
                return true;
            if (!s_potSoMissingWarned)
            {
                s_potSoMissingWarned = true;
                StubLog.LogWarn("[PushablePot] 大锅 SO 三通道皆空（m_potSO/soArray 槽 0/m_potBundle），无法装配: " + name);
            }
            return false;
        }

        /// <summary>同步加载大锅 prefab（v14）：供扫描前预装配走——那条路径必须在
        /// 一帧之内做完（ScanEntities 前缀里不能 yield），几十 ms 的一次性卡顿发生在
        /// 关卡加载流程内，代价可接受；换来的是主客机层级确定性一致。</summary>
        private GameObject LoadPotPrefabSync()
        {
            string bundleName;
            string assetPath;
            PseudoPrefabSO so;
            if (!ResolvePotSource(out bundleName, out assetPath, out so))
                return null;
            AssetBundle bundle = null;
            try
            {
                bundle = GameApi.GetAssetBundle(bundleName);
                if (bundle != null)
                {
                    var prefab = bundle.LoadAsset<GameObject>(assetPath);
                    if (prefab != null)
                        return prefab;
                }
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 大锅同步加载异常 " + bundleName + "/" + assetPath + ": " + ex.Message);
                return null;
            }
            return LoadPotPrefabFallback(so, bundle, bundleName, assetPath);
        }

        /// <summary>加载落空后的统一兜底与分流告警（编辑器宿主链 + 各一次的诊断）。</summary>
        private GameObject LoadPotPrefabFallback(PseudoPrefabSO so, AssetBundle bundle,
            string bundleName, string assetPath)
        {
            try
            {
                var prefab = LoadPotPrefabViaEditorHost(so, bundleName, assetPath);
                if (prefab != null)
                    return prefab;
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
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 大锅兜底加载异常 " + bundleName + "/" + assetPath + ": " + ex.Message);
            }
            return null;
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
                    StubLog.Dbg("[PushablePot] 大锅经编辑器宿主加载链兜底成功: " + bundleName);
                return prefab;
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 编辑器宿主兜底加载异常（忽略，走重试）: " + ex.Message);
                return null;
            }
        }

        private bool Assemble(GameObject carrier, GameObject potPrefab)
        {
            try
            {
                // 晚到告警（v14）：本场景的同步已启动 = 本锅错过了实体扫描，不会有
                // 任何同步组件（表现为「开局就有汤、丢不进食材」）。此时【不会】影响
                // 联机 ID 对齐（ID 已全部发完），所以照常装配，但必须让日志喊出来。
                // 判据用「本场景 StartSynchronisation 已发生」而非 IsSynchronisationActive
                // ——后者是跨关卡不复位的静态标志，会在每张新图上误报。
                if (NetDiagnostics.SyncStartedThisScene && !s_lateAssembleWarned)
                {
                    s_lateAssembleWarned = true;
                    StubLog.LogWarn("[PushablePot] ⚠ 本锅晚于网络实体扫描装配，将缺少同步组件"
                        + "（现象：开局就有汤、食材丢不进去）: " + name
                        + "——检查载具是否在 ScanEntities 时刻尚未生成");
                }
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

                m_assembledPot = pot;
                m_assembledCarrier = carrier;

                StubLog.Log("[PushablePot] 装配完成: " + name + " → 载具 " + carrier.name
                    + " + 大锅 " + pot.name + "（额外食材 " + (m_extraIngredientBundles != null ? m_extraIngredientBundles.Length : 0)
                    + "，cook=" + m_cookTime + " burn=" + m_burnTime + " cap=" + m_capacity + "）");
                return true;
            }
            catch (System.Exception ex)
            {
                // 首次全量堆栈（定位用），后续只留一条简讯——本方法在失败时会被
                // RunInner 按退避周期重试，无限制打全栈会把日志与主线程一起拖垮。
                if (!m_assembleFailLogged)
                {
                    m_assembleFailLogged = true;
                    StubLog.LogWarn("[PushablePot] 装配失败: " + name + "\n" + ex);
                }
                else
                {
                    StubLog.Dbg("[PushablePot] 装配重试仍失败: " + name + " " + ex.Message);
                }
                return false;
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

        // ============ 扫描前预装配（v14 联机 ID 错位修复的核心） ============

        /// <summary>本实例是否已装配（按载具上的跨程序集标记判定）。</summary>
        private bool IsAssembled()
        {
            var carrier = FindCarrier();
            return carrier != null && HasPotAssembled(carrier);
        }

        /// <summary>同步完成一次装配（不等协程、不等异步加载）。
        /// 返回 true = 已就位（含「本来就已装配」）。</summary>
        private bool EnsureAssembledNow()
        {
            var carrier = FindCarrier();
            if (carrier == null)
                return false;
            if (HasPotAssembled(carrier))
                return true;
            var prefab = LoadPotPrefabSync();
            if (prefab == null)
                return false;
            return Assemble(carrier, prefab);
        }

        /// <summary>【实体扫描发令时刻】把场景里所有可移动火锅同步装配完
        /// （由 MultiplayerController.ScanEntities 的 Harmony 前缀调用）。
        ///
        /// 这是主客机唯一确定性一致的锚点：两台机器都在「自己收到
        /// GameState.ScanNetworkEntities」时把锅装好，扫描遍历到的层级因而完全相同，
        /// 实体 ID 才会对齐。锅挂在各自载具下、层级位置固定，与本方法的遍历顺序无关。
        ///
        /// 任何一口锅没能就位（载具还没生成 / bundle 缺失）都会拉高告警等级——
        /// 那意味着两机层级可能不一致，联机会 ID 错位。</summary>
        internal static void FlushPendingAssemblies(out int total, out int assembled)
        {
            total = 0;
            assembled = 0;
            PushablePot[] pots;
            try
            {
                pots = FindObjectsOfType<PushablePot>();
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 预装配扫描异常（跳过）: " + ex.Message);
                return;
            }
            if (pots == null || pots.Length == 0)
                return;
            total = pots.Length;
            var already = 0;
            for (int i = 0; i < pots.Length; i++)
            {
                var pot = pots[i];
                if (pot == null)
                    continue;
                try
                {
                    if (pot.IsAssembled())
                    {
                        already++;
                        assembled++;
                        continue;
                    }
                    if (pot.EnsureAssembledNow())
                        assembled++;
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[PushablePot] 预装配单口锅失败 " + pot.name + ": " + ex.Message);
                }
            }
            var msg = "[PushablePot] 扫描前预装配: " + assembled + "/" + total + " 口锅就位"
                + "（本次新装 " + (assembled - already) + "，此前已装 " + already + "）";
            if (assembled < total)
                StubLog.LogWarn(msg + " ⚠ 有锅未就位（载具未生成？bundle 缺失？）"
                    + "——该锅将缺少同步组件，联机还可能因两机层级不一致导致实体 ID 错位");
            else
                StubLog.Log(msg);
        }

        /// <summary>实体注册自检（由 NetDiagnostics 在 StartSynchronisation 前缀调用）。
        /// 此时链接循环已结束、同步组件已挂好，但 StartSynchronising 还没跑——
        /// 正好能看出这口锅有没有拿到网络实体身份。
        ///
        /// 注意客机侧【没有】任何 Server* 同步器（AddSynchronisedType 只在
        /// host/单机注册 server 类型，EntitySerialisationRegistry.cs:105-116），
        /// 所以 SrvIngred=F 在客机上是正常的，不作为告警依据。</summary>
        internal static void DumpRegistrationSelfCheck()
        {
            PushablePot[] pots;
            try
            {
                pots = FindObjectsOfType<PushablePot>();
            }
            catch (System.Exception)
            {
                return;
            }
            if (pots == null || pots.Length == 0)
                return;
            var isServer = GameApi.IsServerMachine();
            for (int i = 0; i < pots.Length; i++)
            {
                var self = pots[i];
                if (self == null)
                    continue;
                var pot = self.m_assembledPot;
                if (pot == null)
                {
                    StubLog.LogWarn("[PushablePot] 实体注册自检: " + self.name
                        + " ⚠ 未装配大锅（这口锅本局不可用）");
                    continue;
                }
                var hasEntry = GameApi.HasEntityEntry(pot);
                var id = GameApi.GetEntityId(pot);
                var cliIngred = HasInChildren(pot, GameApi.ClientIngredientContainerType);
                var cliCookable = HasInChildren(pot, GameApi.ClientCookableContainerType);
                var cliContents = HasInChildren(pot, GameApi.ClientContentsCosmeticType);
                var srvIngred = HasInChildren(pot, GameApi.ServerIngredientContainerType);
                var carrierId = self.m_assembledCarrier != null
                    ? GameApi.GetEntityId(self.m_assembledCarrier) : 0u;
                var line = "[PushablePot] 实体注册自检: " + self.name
                    + " entry=" + (hasEntry ? "有" : "无") + " id=" + id + " 载具id=" + carrierId
                    + " CliIngred=" + Yn(cliIngred) + " CliCookable=" + Yn(cliCookable)
                    + " CliContents=" + Yn(cliContents) + " SrvIngred=" + Yn(srvIngred)
                    + "（角色=" + GameApi.RoleLabel() + "）";
                var bad = !hasEntry || id == 0u || !cliIngred || !cliContents
                    || (isServer && !srvIngred);
                if (bad)
                    StubLog.LogWarn(line + " ⚠ 同步组件缺失 = 这口锅会「开局就有汤且丢不进食材」"
                        + "——本锅错过了网络实体扫描");
                else
                    StubLog.Log(line);
            }
        }

        private static string Yn(bool v)
        {
            return v ? "T" : "F";
        }

        private static bool HasInChildren(GameObject go, System.Type type)
        {
            return go != null && type != null && go.GetComponentInChildren(type, true) != null;
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
                StubLog.Dbg("[PushablePot] 食材许可表合并重建: " + name + "（默认 " + (entries.Count - extraEntries.Count)
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
