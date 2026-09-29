using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 老鼠偷食材（RatHeist）运行时组件（母本）。
    ///
    /// 行为：开局藏在绑定工作台底下（wrapper 摆放格），按可调间隔出洞，
    /// 寻路到最近的「台面自由物品」（分类开关过滤），服务端权威偷取
    /// （AttachStation.TakeItem → ServerPlayerAttachmentCarrier.CarryItem），
    /// 拖回家门口销毁（NetworkUtils.DestroyObject），循环。玩家面对老鼠按
    /// 交互键 = 打一下：掉落携带物（carrier.TakeItem 物理落地）+ 逃回家。
    ///
    /// 资产：复古鼠 rat.prefab（bundle47，本体包，模型 bundle34/动画 bundle0），
    /// 皮肤 dlc08 = 官方 h18 关同款做法（复古鼠模型 + t_dlc08_rat_01_d 贴图，
    /// bundle355）。
    ///
    /// 原版考据（AssetRipper 导出现役 DLL + ilspy 反编译确认）：RatManager/
    /// RatBehaviour/RatSpawnPoint/RatCosmeticDecisions 在本体里只有序列化字段、
    /// 无任何方法——偷食逻辑从未在 OC2 实装，本组件为全自定义行为，复用原版
    /// 无主组件：GridNavigator（网格寻路）、Interactable（交互，MultiplayerController
    /// :483 注册了同步器）、PlayerAttachmentCarrier（携带，:373 注册同步器，
    /// ChefCarryMessage 全网同步携带物）。老鼠 prefab 由 wrapper 通道
    /// （编辑器 PseudoPrefabManager / 真机 OC2DIYLevel）在实体扫描前生成，
    /// 其同步类型组件被自动同步化；bundle 缺失时兜底自建（也在扫描前完成，
    /// 避开 2.2.1 实体 ID 错位事故的窗口）。
    ///
    /// 双端分工（服务端判定 = ConnectionStatus.IsHost() || !IsInSession()）：
    /// - 服务端：权威状态机（计时/选目标/偷/拖/销毁），全部走官方网络消息
    ///   （AttachStationMessage/ChefCarryMessage/DestroyEntity），零自建同步；
    /// - 客户端：影子演出——同 interval 计时 + 同确定性规则（最近目标，平局取
    ///   格坐标字典序）预测目标跑位，以「嘴上挂点出现/消失物品」的真实同步
    ///   事件矫正（挂上=回家；物品 active 但离嘴=被打掉落→回家；物品
    ///   inactive=销毁→回洞）。预测错误的代价只是老鼠在错误台面短等 2s。
    ///
    /// 目标枚举统一走 AttachStation.m_attachPoint.childCount（物品挂台面 =
    /// parent 在挂点下，双端一致且零同步器反射）；偷取动作只走
    /// ServerAttachStation.TakeItem（官方流程：消息广播 + referral 清理）。
    /// 分类过滤：Plate→plated 开关；Cookable/Preparation/MixableContainer→
    /// utensil 开关（且台面为加热/搅拌站=正在烹饪/搅拌，一律跳过）；
    /// IngredientPropertiesComponent→raw 开关（默认只开这项）。正在被切的
    /// 食材在 PreparationContainer 容器内部、不在台面挂点上，天然偷不到。
    ///
    /// 兼容性：未装加载器/程序集缺失时本组件不存在（脚本缺失为惰性警告），
    /// 场景里只剩一个不可见的 wrapper 壳（无行为）。
    /// </summary>
    public class RatHeist : MonoBehaviour
    {
        [SerializeField] public float m_interval = 20f;

        /// <summary>偷取半径（格，CELL=1.2m）。0/负 = 全厨房。</summary>
        [SerializeField] public float m_radius = 0f;

        /// <summary>移动速度倍率（GridNavigator 基速 4.5 m/s）。</summary>
        [SerializeField] public float m_speed = 1f;

        /// <summary>皮肤：retro（默认）/ dlc08（高清鼠贴图）/ cockroach（commonW3 内置蟑螂低模）。</summary>
        [SerializeField] public string m_skin = "retro";

        [SerializeField] public bool m_stealRaw = true;
        [SerializeField] public bool m_stealPlated = false;
        [SerializeField] public bool m_stealUtensil = false;

        /// <summary>场景自愈载体 tag 前缀。格式：
        /// RatHeist|&lt;interval&gt;,&lt;radius&gt;,&lt;speed&gt;,&lt;skin&gt;,&lt;raw 1|0&gt;,&lt;plated 1|0&gt;,&lt;utensil 1|0&gt;</summary>
        public const string TagPrefix = "RatHeist|";

        private const string Version = "v1(" + StubVersion.Value + ")";
        private const float CellSize = 1.2f;
        private const string RatBundle = "bundle47";
        private const string RatPrefabPath = "assets/prefabs/overcooked_legacy/beings/rat.prefab";
        private const string SkinBundle = "bundle355";
        private const string SkinTexturePath =
            "assets/downloadablecontent/dlc08/dlc_assets/models/characters/textures/t_dlc08_rat_01_d.png";
        private const string CockroachBundle = "commonW3";
        private const string CockroachModelPath =
            "assets/commonw3/rat/models/web_cockroach_low/web_cockroach_low.fbx";
        private const string CockroachEditorAssetPath =
            "Assets/commonW3/rat/models/Web_Cockroach_Low/Web_Cockroach_Low.fbx";
        private const string CockroachBaseColorPath =
            "assets/commonw3/rat/models/web_cockroach_low/web_cockroach_low_base_color.jpg";
        private const string CockroachNormalPath =
            "assets/commonw3/rat/models/web_cockroach_low/web_cockroach_low_normal.jpg";
        private const string CockroachBaseColorEditorPath =
            "Assets/commonW3/rat/models/Web_Cockroach_Low/Web_Cockroach_Low_base_color.jpg";
        private const string CockroachNormalEditorPath =
            "Assets/commonW3/rat/models/Web_Cockroach_Low/Web_Cockroach_Low_normal.jpg";
        /// <summary>蟑螂低模对齐复古鼠包围盒：绕 Y +90° 后非均匀缩放（见 gen-commonw3-rat-cockroach.mjs），
        /// 在基准比例上再放大 1.4×。</summary>
        private static readonly Vector3 CockroachLocalScale = new Vector3(1.82f, 1.90f, 1.44f);
        private const float CockroachYawDeg = 90f;
        private const float HideDropY = 1.05f;
        /// <summary>无目标巡逻的最少单程距离（格）。</summary>
        private const float MinPatrolCells = 5f;
        /// <summary>地板射线（对齐 PushableVoidFall：Ground 层 Col_Floor）。</summary>
        private const float GroundRayStartY = 2.5f;
        private const float GroundRayDistance = 5f;

        // ---- 运行时状态（不序列化） ----
        private GameObject _rat;
        private Component _nav;
        private Transform _attachPoint;
        private bool _isServer;
        private bool _fleeing;
        private GameObject _carried;
        private Vector3 _homePos;
        private Renderer[] _ratRenderers;
        private Collider[] _ratColliders;
        private GameObject _cockroachVisual;
        private bool _skinApplied;
        private bool _hitHooked;
        private Coroutine _fleeRoutine;
        private GameObject _targetStation; // 服务端本轮占用的目标台面（s_targets 配套）
        private float _channelWaited;      // 已等待 wrapper 通道的秒数（超过阈值走兜底）

        /// <summary>本机（服务端）目标占用表：多鼠不同时盯同一台面。
        /// 清场：OnSceneChanged（挂 EntryPoint.ResetSceneTickers 链）。</summary>
        private static readonly HashSet<GameObject> s_targets = new HashSet<GameObject>();
        private static int s_groundMask;
        private static bool s_groundMaskReady;
        /// <summary>是否已把 GridNavSpace 无地板格标为不可走（每场景一次）。</summary>
        private static bool s_navVoidPatched;
        private static bool s_navVoidPatchWarned;

        private static void Log(string msg)
        {
            StubLog.Dbg(msg);
        }

        private static void LogWarn(string msg)
        {
            StubLog.LogWarn(msg);
        }

        /// <summary>场景切换清场（铁律：静态缓存必须挂 ResetSceneTickers 链）。</summary>
        internal static void OnSceneChanged()
        {
            if (s_targets.Count > 0)
                Log("[RatHeist] 场景切换清目标占用表: " + s_targets.Count + " 个");
            s_targets.Clear();
            s_groundMaskReady = false;
            s_navVoidPatched = false;
            s_navVoidPatchWarned = false;
        }

        /// <summary>外层协程：C#4 禁止在有 catch 的 try 里 yield，嵌套枚举器给整个
        /// 协程套异常捕获（镜像 RandomCrate.Start）。</summary>
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
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            Log("[RatHeist " + Version + "] 初始化: " + name
                + "（间隔 " + m_interval.ToString("0.##") + "s，半径 "
                + (m_radius > 0f ? m_radius.ToString("0.##") + " 格" : "全图")
                + "，速度 ×" + m_speed.ToString("0.##") + "，皮肤 " + m_skin
                + "，偷取 raw=" + m_stealRaw + " plated=" + m_stealPlated
                + " utensil=" + m_stealUtensil + "）");
            while (true)
            {
                // 1. 老鼠视觉体：wrapper 通道（PseudoPrefabManager/OC2DIYLevel）会在
                //    场景加载早期生成真实 rat.prefab 实例（子孙里带 GridNavigator）。
                //    先纯等 5s 让通道生成；仍无再从 bundle47 兜底自建（也在实体扫描
                //    前，避免 2.2.1 实体 ID 错位窗口）。
                if (!EnsureRatVisual(5f))
                {
                    _channelWaited += 1f; // 外层 1s 重试节拍
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }
                _channelWaited = 0f;
                _homePos = transform.position;
                ApplySkin();
                HideRat();
                Log("[RatHeist] 老鼠就绪: " + name + " → " + _rat.name
                    + "（家 " + _homePos.ToString("0.##") + "，挂点 "
                    + (_attachPoint != null ? "有" : "无") + "）");

                // 2. 等网络同步完成（Server/Client 同步器在 AsyncScanEntities 之后
                //    才挂载；不设超时，_rat 失效则回外层重装配）。
                var waitStart = Time.realtimeSinceStartup;
                var lastBeat = waitStart;
                var waitingLogged = false;
                while (!GameApi.IsSynchronisationActive())
                {
                    if (_rat == null)
                        break;
                    if (!waitingLogged)
                    {
                        waitingLogged = true;
                        Log("[RatHeist] 等待网络同步完成: " + name);
                    }
                    else if (Time.realtimeSinceStartup - lastBeat > 5f)
                    {
                        lastBeat = Time.realtimeSinceStartup;
                        Log("[RatHeist] 仍在等待网络同步（已等 "
                            + (int)(lastBeat - waitStart) + "s）: " + name);
                    }
                    yield return new WaitForSecondsRealtime(0.2f);
                }
                if (_rat == null)
                {
                    yield return null;
                    continue;
                }

                // 3. 分端就绪（晚生成老鼠补挂同步器 + 打鼠回调）
                _isServer = GameApi.IsServerMachine();
                EnsureRatNetworkSync();
                TryHookInteraction();
                Log("[RatHeist] 网络同步就绪: " + name + "（isServer=" + _isServer
                    + "，实体=" + GameApi.HasEntityEntry(_rat) + "，打鼠="
                    + _hitHooked + "）");
                EnsureNavVoidMask();

                // 4. 状态机主循环（重开关卡后 _rat 失效 → 回外层重装配）
                while (_rat != null)
                {
                    var t = 0f;
                    while (t < m_interval && !_fleeing && _rat != null)
                    {
                        t += RatApi.DeltaTime(_rat);
                        yield return null;
                    }
                    if (_rat == null)
                        break;
                    if (_fleeing)
                    {
                        // 上一轮被打后已在回调里逃亡，此处等它消停
                        while (_fleeing && _rat != null)
                            yield return null;
                        continue;
                    }
                    if (_isServer)
                    {
                        var heist = ServerHeist();
                        while (heist.MoveNext())
                        {
                            if (_rat == null)
                                break;
                            yield return heist.Current;
                        }
                    }
                    else
                    {
                        var shadow = ClientShadow();
                        while (shadow.MoveNext())
                        {
                            if (_rat == null)
                                break;
                            yield return shadow.Current;
                        }
                    }
                }
                yield return null;
            }
        }

        // ==================== 服务端权威状态机 ====================

        /// <summary>出洞→寻路→偷取→回家→销毁。任何阶段被打（_fleeing）→ 逃亡。
        /// 无可偷目标 → 随机巡逻（≥MinPatrolCells 格）再回洞。
        /// stationGo==null 表示地面自由物品（直接捡起，不走台面 TakeItem）。
        /// 注意 C#4 禁止在带 finally 的 try 里 yield：目标占用（s_targets）的
        /// 释放由每条退出路径显式 ReleaseTarget 完成。</summary>
        private IEnumerator ServerHeist()
        {
            GameObject stationGo;
            GameObject itemGo;
            if (!FindTarget(out stationGo, out itemGo))
            {
                var patrol = Patrol();
                while (patrol.MoveNext())
                {
                    if (_rat == null)
                        break;
                    yield return patrol.Current;
                }
                yield break; // 无目标：巡逻后回洞重计时
            }
            var targetPos = stationGo != null ? stationGo.transform.position : itemGo.transform.position;
            _targetStation = stationGo;
            if (stationGo != null)
                s_targets.Add(stationGo);
            ShowRat();
            // —— 去程 ——
            if (!TryNavMoveTo(targetPos))
            {
                ReleaseTarget();
                var goHomeEarly = ReturnHome();
                while (goHomeEarly.MoveNext())
                    yield return goHomeEarly.Current;
                yield break;
            }
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null)
            {
                yield return null;
                if (_fleeing || _rat == null)
                {
                    ReleaseTarget();
                    yield break;
                }
                if (AbortNavIfOverVoid())
                {
                    ReleaseTarget();
                    var goHomeVoid = ReturnHome();
                    while (goHomeVoid.MoveNext())
                        yield return goHomeVoid.Current;
                    yield break;
                }
            }
            // —— 寻路失败守卫：NavPoint snap 到不可达岛时 HasCompletedRoute 立即为
            //    true（FindPath 空路径），老鼠没跑过去——放弃本轮（防远程偷取穿帮）。
            var offX = _rat != null ? _rat.transform.position.x - targetPos.x : 0f;
            var offZ = _rat != null ? _rat.transform.position.z - targetPos.z : 0f;
            var arrived = _rat != null && (offX * offX + offZ * offZ) < 6.25f; // ≤ 2.5m ≈ 2 格
            // —— 偷取（目标可能已被玩家拿走/别的老鼠偷走：重验）——
            GameObject taken = null;
            if (arrived && StillValidTarget(stationGo, itemGo))
            {
                if (stationGo != null)
                {
                    var serverStation = RatApi.GetComponent(stationGo, RatApi.ServerAttachStationType);
                    taken = RatApi.StationTakeItem(serverStation);
                }
                else
                {
                    taken = itemGo; // 地面自由物品：直接捡
                }
            }
            if (taken == null)
            {
                ReleaseTarget();
                var goHome = ReturnHome();
                while (goHome.MoveNext())
                    yield return goHome.Current;
                yield break; // 扑空：回家继续等
            }
            _carried = taken;
            RatApi.RatCarry(_rat, taken);
            Log("[RatHeist] 偷到: " + name + " → " + taken.name
                + (stationGo == null ? "（地面）" : ""));
            // —— 回程 ——
            if (!TryNavMoveTo(_homePos))
            {
                ReleaseTarget();
                HideRat();
                yield break;
            }
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null)
            {
                yield return null;
                if (_fleeing || _rat == null)
                {
                    // 掉落已在被打回调里处理；逃亡协程负责回家
                    ReleaseTarget();
                    yield break;
                }
                if (AbortNavIfOverVoid())
                {
                    ReleaseTarget();
                    HideRat();
                    yield break;
                }
            }
            // —— 到家销毁 ——
            var carried = _carried;
            _carried = null;
            ReleaseTarget();
            if (carried != null)
            {
                RatApi.RatDrop(_rat, carried);
                RatApi.DestroyObject(carried);
                Log("[RatHeist] 销毁食材: " + name + " → " + carried.name);
            }
            HideRat();
        }

        /// <summary>偷取前重验：台面目标=挂点首物仍是它；地面目标=仍激活且自由态。</summary>
        private static bool StillValidTarget(GameObject stationGo, GameObject itemGo)
        {
            if (itemGo == null)
                return false;
            if (stationGo == null)
                return itemGo.activeInHierarchy && !RatApi.IsAttached(itemGo);
            return GetStationItem(stationGo) == itemGo;
        }

        /// <summary>释放本轮目标占用（ServerHeist 各退出路径显式调用）。</summary>
        private void ReleaseTarget()
        {
            if (_targetStation != null)
                s_targets.Remove(_targetStation);
            _targetStation = null;
        }

        /// <summary>回家并藏起（不操作可见性——调用方保证当前可见）。守卫超时防
        /// 不可达时卡死（HasCompletedRoute 恒 false 的防御）。
        /// abortOnFlee：被打逃亡时 FleeHome 独占回家，其它协程里的 ReturnHome 应立即让出。</summary>
        private IEnumerator ReturnHome(bool hideWhenDone = true, bool abortOnFlee = true)
        {
            if (abortOnFlee && _fleeing)
                yield break;
            if (TryNavMoveTo(_homePos))
            {
                var guard = 0f;
                while (!RatApi.NavCompleted(_nav) && _rat != null && guard < 15f)
                {
                    if (abortOnFlee && _fleeing)
                        yield break;
                    guard += RatApi.DeltaTime(_rat);
                    yield return null;
                    if (AbortNavIfOverVoid())
                        break;
                }
            }
            if (abortOnFlee && _fleeing)
                yield break;
            if (hideWhenDone)
                HideRat();
        }

        /// <summary>被打回调（服务端，ServerInteractable.TriggerInteract 委托）。
        /// 立刻停导航、释放偷取目标、掉落携带物，并从当前位置逃回洞穴。</summary>
        internal void OnRatHit(GameObject interacter, Vector2 directionXZ)
        {
            if (!_isServer || _fleeing)
                return;
            Log("[RatHeist] 被打: " + name + (interacter != null ? "（by " + interacter.name + "）" : ""));
            _fleeing = true;
            ReleaseTarget();
            RatApi.NavClear(_nav);
            DropCarriedNow();
            SetRatVisible(true);
            if (_fleeRoutine != null)
            {
                StopCoroutine(_fleeRoutine);
                _fleeRoutine = null;
            }
            _fleeRoutine = StartCoroutine(FleeHome());
        }

        /// <summary>被打后从当前位置寻路回洞（不传送回 home，避免「瞬移回家」穿帮）。</summary>
        private IEnumerator FleeHome()
        {
            RatApi.NavClear(_nav);
            var goHome = ReturnHome(true, false);
            while (goHome.MoveNext())
            {
                if (_rat == null)
                    break;
                yield return goHome.Current;
            }
            _fleeing = false;
            _fleeRoutine = null;
        }

        /// <summary>掉落嘴上/缓存的携带物（被打时同步落地，不销毁）。</summary>
        private void DropCarriedNow()
        {
            if (_rat == null)
                return;
            if (_carried != null)
            {
                RatApi.RatDrop(_rat, _carried);
                _carried = null;
                return;
            }
            if (_attachPoint == null || _attachPoint.childCount == 0)
                return;
            var onMouth = _attachPoint.GetChild(0).gameObject;
            if (onMouth != null)
                RatApi.RatDrop(_rat, onMouth);
        }

        // ==================== 客户端影子演出 ====================

        /// <summary>无目标巡逻：走到距家 ≥MinPatrolCells 格的随机可走点再回洞
        /// （用户规则：多鼠关卡里老鼠也要固定出来溜达，不能全程蹲洞）。
        /// 双端本地随机（不追求同步——纯视觉行为，偷/毁仍由服务端事件驱动）。</summary>
        private IEnumerator Patrol()
        {
            ShowRat();
            var target = _homePos;
            var found = false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dist = UnityEngine.Random.Range(MinPatrolCells + 0.5f, MinPatrolCells + 4f) * CellSize;
                var candidate = _homePos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                if (HasGroundSupport(candidate.x, candidate.z))
                {
                    target = candidate;
                    found = true;
                    break;
                }
            }
            if (!found || !TryNavMoveTo(target))
            {
                var goHomeEarly = ReturnHome();
                while (goHomeEarly.MoveNext())
                {
                    if (_rat == null)
                        break;
                    yield return goHomeEarly.Current;
                }
                yield break;
            }
            var guard = 0f;
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null && guard < 25f)
            {
                guard += RatApi.DeltaTime(_rat);
                yield return null;
                if (_fleeing || _rat == null)
                    yield break;
                if (AbortNavIfOverVoid())
                    break;
            }
            if (_fleeing)
                yield break;
            var goHome = ReturnHome();
            while (goHome.MoveNext())
            {
                if (_rat == null)
                    break;
                yield return goHome.Current;
            }
        }

        /// <summary>预测跑位 + 真实事件矫正：
        /// - 预测目标（同服务端确定性规则）跑过去；无预测目标 → 本地巡逻；
        /// - 嘴上挂点出现物品（Attach 同步）或预测目标消失（服务端已偷走）→ 立刻回家；
        /// - 台面扑空（目标还在但迟迟没被抓）→ 原地等 2s 再回家。</summary>
        private IEnumerator ClientShadow()
        {
            GameObject stationGo;
            GameObject itemGo;
            if (!FindTarget(out stationGo, out itemGo))
            {
                var patrol = Patrol();
                while (patrol.MoveNext())
                {
                    if (_rat == null)
                        break;
                    yield return patrol.Current;
                }
                yield break;
            }
            var targetPos = stationGo != null ? stationGo.transform.position : itemGo.transform.position;
            ShowRat();
            if (!TryNavMoveTo(targetPos))
            {
                var goHomeFail = ReturnHome();
                while (goHomeFail.MoveNext())
                    yield return goHomeFail.Current;
                yield break;
            }
            while (!RatApi.NavCompleted(_nav) && _rat != null
                && (_attachPoint == null || _attachPoint.childCount == 0)
                && StillValidTarget(stationGo, itemGo)
                && !_fleeing)
            {
                if (AbortNavIfOverVoid())
                {
                    var goHomeVoid = ReturnHome();
                    while (goHomeVoid.MoveNext())
                        yield return goHomeVoid.Current;
                    yield break;
                }
                yield return null;
            }
            if (_fleeing || _rat == null)
                yield break;
            // 到位（或嘴上提前出现物品/目标已被服务端偷走）：稍等看有没有挂上嘴
            var wait = 0f;
            while (wait < 2f && _attachPoint != null && _attachPoint.childCount == 0
                && StillValidTarget(stationGo, itemGo))
            {
                wait += RatApi.DeltaTime(_rat);
                yield return null;
            }
            if (_rat == null)
                yield break;
            // 回家（无论偷没偷到，跟着嘴上物品走）
            var goHome = ReturnHome();
            while (goHome.MoveNext())
                yield return goHome.Current;
        }

        // ==================== 目标选择（双端同规则，确定性） ====================

        /// <summary>候选源：①台面挂点物品（AttachStation.m_attachPoint 首物）；
        /// ②地面自由物品（CarryableItem 且未 attach——掉在地上的食材/盘子等，
        /// 用户规则：地上的也要捡）。取离家最近（平局取格坐标 x 后 z 字典序，
        /// 保证双端确定性）。藏身格附近跳过。</summary>
        private bool FindTarget(out GameObject stationGo, out GameObject itemGo)
        {
            stationGo = null;
            itemGo = null;
            _bestDist = float.MaxValue;
            _bestX = 0f;
            _bestZ = 0f;
            _candStation = null;
            _candItem = null;

            var stations = GameApi.FindAll(RatApi.AttachStationType);
            if (stations != null)
            {
                for (int i = 0; i < stations.Length; i++)
                {
                    var station = stations[i] as Component;
                    if (station == null || station.gameObject == null)
                        continue;
                    var go = station.gameObject;
                    var item = GetStationItem(go);
                    if (item == null)
                        continue;
                    CollectCandidate(go, go.transform.position, item);
                }
            }

            var grounds = GameApi.FindAll(RatApi.CarryableItemType);
            if (grounds != null)
            {
                for (int i = 0; i < grounds.Length; i++)
                {
                    var c = grounds[i] as Component;
                    if (c == null || c.gameObject == null)
                        continue;
                    var go = c.gameObject;
                    if (!go.activeInHierarchy)
                        continue; // 容器内部物品（正在切/锅里）_inactive
                    if (RatApi.IsAttached(go))
                        continue; // 已被台面/玩家/其他老鼠持有
                    if (go.transform.IsChildOf(transform))
                        continue; // 自己 wrapper 子树
                    CollectCandidate(null, go.transform.position, go);
                }
            }
            stationGo = _candStation;
            itemGo = _candItem;
            return _candItem != null;
        }

        // CollectCandidate 的折叠状态（避免 C#4 无局部函数）
        private float _bestDist;
        private float _bestX;
        private float _bestZ;
        private GameObject _candStation;
        private GameObject _candItem;

        private void CollectCandidate(GameObject stationGo, Vector3 pos, GameObject item)
        {
            if (!HasGroundSupport(pos.x, pos.z))
                return;
            // 藏身格（wrapper 同格台面/家里地面）不偷：距家格中心 < 0.6 格视为同格
            var hdx = pos.x - _homePos.x;
            var hdz = pos.z - _homePos.z;
            if (hdx * hdx + hdz * hdz < 0.36f)
                return;
            if (!PassStealFilter(item, stationGo))
                return;
            if (_isServer && stationGo != null && s_targets.Contains(stationGo))
                return;
            var dist = hdx * hdx + hdz * hdz;
            if (m_radius > 0f)
            {
                var limit = m_radius * CellSize;
                if (Mathf.Sqrt(dist) > limit)
                    return;
            }
            // 确定性平局判定：先距离，后 x，再 z
            var better = dist < _bestDist - 0.01f;
            if (!better && Mathf.Abs(dist - _bestDist) <= 0.01f)
            {
                if (pos.x < _bestX - 0.01f)
                    better = true;
                else if (Mathf.Abs(pos.x - _bestX) <= 0.01f && pos.z < _bestZ - 0.01f)
                    better = true;
            }
            if (better)
            {
                _bestDist = dist;
                _bestX = pos.x;
                _bestZ = pos.z;
                _candStation = stationGo;
                _candItem = item;
            }
        }

        /// <summary>台面挂点物品（双端一致视图：物品挂台面 = parent 在 m_attachPoint 下）。</summary>
        private static GameObject GetStationItem(GameObject stationGo)
        {
            var station = RatApi.GetComponent(stationGo, RatApi.AttachStationType);
            if (station == null)
                return null;
            var point = RatApi.AttachPointField != null
                ? RatApi.AttachPointField.GetValue(station) as Transform
                : null;
            if (point == null || point.childCount == 0)
                return null;
            var first = point.GetChild(0);
            return first != null && first.gameObject.activeSelf ? first.gameObject : null;
        }

        /// <summary>分类过滤：Plate→plated；烹饪/备制/搅拌容器→utensil（且台面是
        /// 加热/搅拌站=正在烹饪/搅拌，一律跳过）；食材→raw（生/切好都算，
        /// 判定 = IngredientDisposalBehaviour 为主 + IPC 兜底，见 RatApi 注释）；
        /// 其余不偷。</summary>
        private bool PassStealFilter(GameObject item, GameObject stationGo)
        {
            if (RatApi.GetComponent(item, RatApi.PlateType) != null)
                return m_stealPlated;
            var isUtensil =
                RatApi.GetComponent(item, RatApi.CookableContainerType) != null
                || RatApi.GetComponent(item, RatApi.CookablePreparationContainerType) != null
                || RatApi.GetComponent(item, RatApi.PreparationContainerType) != null
                || RatApi.GetComponent(item, RatApi.MixableContainerType) != null;
            if (isUtensil)
            {
                if (!m_stealUtensil)
                    return false;
                // 正在烹饪/搅拌：锅在加热/搅拌台上 → 跳过（用户规则）
                return RatApi.GetComponent(stationGo, RatApi.HeatedCookingStationType) == null
                    && RatApi.GetComponent(stationGo, RatApi.CookingStationType) == null
                    && RatApi.GetComponent(stationGo, RatApi.MixingStationType) == null
                    && RatApi.GetComponent(stationGo, RatApi.HeatedStationType) == null;
            }
            if (!m_stealRaw)
                return false;
            return RatApi.GetComponent(item, RatApi.IngredientDisposalType) != null
                || RatApi.GetComponent(item, RatApi.IngredientPropertiesType) != null;
        }

        // ==================== 视觉体装配 ====================

        /// <summary>定位/生成老鼠视觉体。优先 wrapper 通道生成的真实老鼠（子孙里
        /// 带 GridNavigator 的物体，编辑器 PseudoPrefabManager / 真机 OC2DIYLevel
        /// 在场景加载早期生成）；累计等待超过 channelWaitSeconds 仍无，再从
        /// bundle47 兜底 Instantiate（一次性同步加载，仍在实体扫描前，避开
        /// 2.2.1 实体 ID 错位窗口）。</summary>
        private bool EnsureRatVisual(float channelWaitSeconds)
        {
            var navs = GameApi.GetComponentsInChildren(gameObject, RatApi.GridNavigatorType, true);
            if (navs != null)
            {
                for (int i = 0; i < navs.Length; i++)
                {
                    if (navs[i] != null)
                    {
                        BindRat(navs[i].gameObject, navs[i]);
                        return true;
                    }
                }
            }
            if (_channelWaited < channelWaitSeconds)
            {
                if (_channelWaited == 0f)
                    Log("[RatHeist] 等待 wrapper 通道生成老鼠: " + name);
                return false; // 外层 1s 后重试
            }
            // 兜底：bundle47 直接实例化
            var bundle = GameApi.GetAssetBundle(RatBundle);
            if (bundle == null)
            {
                LogWarn("[RatHeist] 兜底失败：bundle47 未加载（依赖未注册?）: " + name);
                return false;
            }
            try
            {
                var prefab = bundle.LoadAsset<GameObject>(RatPrefabPath);
                if (prefab == null)
                {
                    LogWarn("[RatHeist] 兜底失败：rat.prefab 加载失败 " + RatPrefabPath + ": " + name);
                    return false;
                }
                var rat = (GameObject)Instantiate(prefab, transform.position, Quaternion.identity);
                rat.transform.parent = transform;
                rat.name = "RatHeist_Rat";
                Log("[RatHeist] 兜底自建老鼠（bundle47）: " + name);
                BindRat(rat, null);
                return true;
            }
            catch (Exception ex)
            {
                LogWarn("[RatHeist] 兜底实例化异常: " + name + ": " + ex.Message);
                return false;
            }
        }

        private void BindRat(GameObject rat, Component navOrNull)
        {
            _rat = rat;
            _nav = navOrNull != null ? navOrNull : RatApi.GetComponent(rat, RatApi.GridNavigatorType);
            _attachPoint = EnsureAttachPoints(rat);
            _ratRenderers = rat.GetComponentsInChildren<Renderer>(true);
            _ratColliders = rat.GetComponentsInChildren<Collider>(true);
            var rb = rat.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true; // 完全由 GridNavigator 驱动，不参与物理推送
            RatApi.NavSetSpeed(_nav, m_speed > 0.05f ? m_speed : 1f);
            _skinApplied = false;
            _hitHooked = false;
            EnsureRatGridLocation(rat);
            EnsureRatInteractable(rat);
        }

        /// <summary>确保老鼠有可用的携带挂点（真机实证 2026-09-19：rat.prefab 是 OC1
        /// 移植资产，层级里没有 "Attachment" 子物体——PlayerAttachmentCarrier 的
        /// m_attachPoints 只在 GameState.StartEntities 时 FindChildRecursive("Attachment")
        /// 赋值，找不到时回调 NRE / 挂点全 null，物品 Attach→SetParent(null) 后停在
        /// 偷取处不跟老鼠走，回洞销毁时才消失）。双端补建同名挂点 + 反射直写
        /// carrier.m_attachPoints：StartEntities 已过/将过都安全——将来重跑
        /// FindChildRecursive 会找到本方法建的同名节点，幂等。</summary>
        private Transform EnsureAttachPoints(GameObject rat)
        {
            var front = FindChildRecursive(rat.transform, "Attachment");
            if (front == null)
            {
                var go = new GameObject("Attachment");
                go.transform.parent = rat.transform;
                go.transform.localPosition = new Vector3(0f, 0.12f, 0.22f); // 嘴前下方叼着
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                front = go.transform;
                Log("[RatHeist] 补建携带挂点 Attachment: " + name);
            }
            var back = FindChildRecursive(rat.transform, "Attachment_Backpack");
            if (back == null)
            {
                var go = new GameObject("Attachment_Backpack");
                go.transform.parent = rat.transform;
                go.transform.localPosition = new Vector3(0f, 0.12f, -0.18f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                back = go.transform;
            }
            try
            {
                var carrier = RatApi.GetComponent(rat, RatApi.PlayerAttachmentCarrierType);
                if (carrier != null && RatApi.CarrierAttachPointsField != null)
                {
                    var arr = RatApi.CarrierAttachPointsField.GetValue(carrier) as Transform[];
                    if (arr != null && arr.Length >= 2)
                    {
                        arr[0] = front;
                        arr[1] = back;
                    }
                    else
                    {
                        LogWarn("[RatHeist] carrier.m_attachPoints 数组异常（长度="
                            + (arr != null ? arr.Length.ToString() : "null") + "），仅靠同名子节点兜底: " + name);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn("[RatHeist] 直写 carrier 挂点异常: " + name + ": " + ex.Message);
            }
            return front;
        }

        /// <summary>皮肤：dlc08 换贴图；cockroach 隐藏鼠模并挂 commonW3 蟑螂低模（行为组件不变）。</summary>
        private void ApplySkin()
        {
            if (_skinApplied || _rat == null)
                return;
            _skinApplied = true;
            if (m_skin == "cockroach")
            {
                ApplyCockroachModel();
                return;
            }
            if (m_skin != "dlc08" || _ratRenderers == null || _ratRenderers.Length == 0)
                return;
            var bundle = GameApi.GetAssetBundle(SkinBundle);
            if (bundle == null)
            {
                LogWarn("[RatHeist] dlc08 皮肤：bundle355 未加载，保持复古材质: " + name);
                return;
            }
            try
            {
                var tex = bundle.LoadAsset<Texture2D>(SkinTexturePath);
                if (tex == null)
                {
                    LogWarn("[RatHeist] dlc08 皮肤贴图加载失败 " + SkinTexturePath + ": " + name);
                    return;
                }
                for (int i = 0; i < _ratRenderers.Length; i++)
                {
                    var r = _ratRenderers[i];
                    if (r == null)
                        continue;
                    var mats = r.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null)
                            continue;
                        var clone = new Material(mats[m]);
                        clone.mainTexture = tex;
                        mats[m] = clone;
                    }
                    r.sharedMaterials = mats;
                }
                Log("[RatHeist] dlc08 皮肤已应用: " + name);
            }
            catch (Exception ex)
            {
                LogWarn("[RatHeist] dlc08 皮肤应用异常: " + name + ": " + ex.Message);
            }
        }

        /// <summary>蟑螂皮肤：保留 rat.prefab 的寻路/交互/携带，仅替换可见网格。</summary>
        private void ApplyCockroachModel()
        {
            try
            {
                var modelPrefab = LoadCockroachModelPrefab();
                if (modelPrefab == null)
                {
                    LogWarn("[RatHeist] cockroach 模型加载失败（commonW3 未加载或未重打 bundle"
                        + "；编辑器可直读工程 FBX）: " + name);
                    return;
                }
                AttachCockroachVisual(modelPrefab);
            }
            catch (Exception ex)
            {
                LogWarn("[RatHeist] cockroach 模型应用异常: " + name + ": " + ex.Message);
            }
        }

        private void AttachCockroachVisual(GameObject modelPrefab)
        {
            if (_ratRenderers != null)
            {
                for (int i = 0; i < _ratRenderers.Length; i++)
                {
                    if (_ratRenderers[i] != null)
                        _ratRenderers[i].enabled = false;
                }
            }
            if (_cockroachVisual != null)
                Destroy(_cockroachVisual);
            _cockroachVisual = (GameObject)Instantiate(modelPrefab, _rat.transform);
            _cockroachVisual.name = "CockroachVisual";
            _cockroachVisual.transform.localPosition = Vector3.zero;
            _cockroachVisual.transform.localRotation = Quaternion.Euler(0f, CockroachYawDeg, 0f);
            _cockroachVisual.transform.localScale = CockroachLocalScale;
            DisableCollidersOnVisual(_cockroachVisual);
            _ratRenderers = _cockroachVisual.GetComponentsInChildren<Renderer>(true);
            ApplyCockroachMaterials();
            Log("[RatHeist] cockroach 模型已应用: " + name);
        }

        /// <summary>视觉子树碰撞体不参与交互扫描（Interactable 在老鼠根节点）。</summary>
        private static void DisableCollidersOnVisual(GameObject visualRoot)
        {
            if (visualRoot == null)
                return;
            var cols = visualRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = false;
            }
        }

        /// <summary>运行时从 commonW3 绑定贴图（bundle 直读 FBX 不会自动链贴图，否则白模）。</summary>
        private void ApplyCockroachMaterials()
        {
            if (_cockroachVisual == null)
                return;
            var baseTex = LoadCockroachTexture(CockroachBaseColorPath, CockroachBaseColorEditorPath);
            if (baseTex == null)
            {
                LogWarn("[RatHeist] cockroach 基础色贴图加载失败: " + name);
                return;
            }
            var normalTex = LoadCockroachTexture(CockroachNormalPath, CockroachNormalEditorPath);
            var renderers = _cockroachVisual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;
                var mats = r.sharedMaterials;
                if (mats == null)
                    continue;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null)
                        continue;
                    var clone = new Material(mats[m]);
                    AssignCockroachAlbedo(clone, baseTex);
                    if (normalTex != null)
                        AssignCockroachNormal(clone, normalTex);
                    mats[m] = clone;
                }
                r.sharedMaterials = mats;
            }
        }

        private static void AssignCockroachAlbedo(Material mat, Texture2D tex)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Diffuse_Map"))
                mat.SetTexture("_Diffuse_Map", tex);
        }

        private static void AssignCockroachNormal(Material mat, Texture2D tex)
        {
            if (mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", tex);
            if (mat.HasProperty("_Normal"))
                mat.SetTexture("_Normal", tex);
            if (mat.HasProperty("_NormalMap1"))
                mat.SetTexture("_NormalMap1", tex);
        }

        private Texture2D LoadCockroachTexture(string bundlePath, string editorPath)
        {
            var bundle = TryResolveCockroachBundle();
            if (bundle != null)
            {
                var tex = bundle.LoadAsset<Texture2D>(bundlePath);
                if (tex != null)
                    return tex;
                tex = bundle.LoadAsset<Texture2D>(editorPath);
                if (tex != null)
                    return tex;
                var names = bundle.GetAllAssetNames();
                var suffix = Path.GetFileName(bundlePath);
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        tex = bundle.LoadAsset<Texture2D>(names[i]);
                        if (tex != null)
                            return tex;
                    }
                }
            }
            return LoadCockroachTextureViaEditorHost(editorPath);
        }

        private static Texture2D LoadCockroachTextureViaEditorHost(string editorPath)
        {
            try
            {
                var assetDb = GameApi.Find("UnityEditor.AssetDatabase, UnityEditor");
                if (assetDb == null)
                    return null;
                var m = assetDb.GetMethod("LoadAssetAtPath",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(Type) }, null);
                if (m == null)
                    return null;
                return m.Invoke(null, new object[] { editorPath, typeof(Texture2D) }) as Texture2D;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>按名取已加载 commonW3；未加载时从 StreamingAssets 或
        /// Assets/AssetBundles 兜底 LoadFromFile（编辑器 Play 常见）。</summary>
        private static AssetBundle TryResolveCockroachBundle()
        {
            var bundle = GameApi.GetAssetBundle(CockroachBundle);
            if (bundle != null)
                return bundle;
            var streamingWindows = Path.Combine(Application.streamingAssetsPath, "Windows");
            var projectBundles = Path.Combine(Application.dataPath, "AssetBundles");
            var candidates = new string[]
            {
                Path.Combine(streamingWindows, "commonw3"),
                Path.Combine(streamingWindows, CockroachBundle),
                Path.Combine(projectBundles, "commonw3"),
                Path.Combine(projectBundles, CockroachBundle),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!File.Exists(candidates[i]))
                    continue;
                try
                {
                    var loaded = AssetBundle.LoadFromFile(candidates[i]);
                    if (loaded != null)
                        return loaded;
                }
                catch
                {
                }
            }
            return null;
        }

        private GameObject LoadCockroachModelPrefab()
        {
            var bundle = TryResolveCockroachBundle();
            if (bundle != null)
            {
                var prefab = bundle.LoadAsset<GameObject>(CockroachModelPath);
                if (prefab != null)
                    return prefab;
                prefab = bundle.LoadAsset<GameObject>(CockroachEditorAssetPath);
                if (prefab != null)
                    return prefab;
                var names = bundle.GetAllAssetNames();
                for (int i = 0; i < names.Length; i++)
                {
                    var assetName = names[i];
                    if (assetName.IndexOf("cockroach", StringComparison.OrdinalIgnoreCase) < 0
                        || assetName.IndexOf(".fbx", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    prefab = bundle.LoadAsset<GameObject>(assetName);
                    if (prefab != null)
                        return prefab;
                }
            }
            return LoadCockroachViaEditorHost();
        }

        /// <summary>编辑器宿主兜底：bundle 未构建/未打进 commonW3 时直读工程 FBX。</summary>
        private static GameObject LoadCockroachViaEditorHost()
        {
            try
            {
                var assetDb = GameApi.Find("UnityEditor.AssetDatabase, UnityEditor");
                if (assetDb == null)
                    return null;
                var m = assetDb.GetMethod("LoadAssetAtPath",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(Type) }, null);
                if (m == null)
                    return null;
                return m.Invoke(null, new object[] { CockroachEditorAssetPath, typeof(GameObject) })
                    as GameObject;
            }
            catch
            {
                return null;
            }
        }

        // ==================== 地板 / 虚空寻路守卫 ====================

        private static void EnsureGroundMask()
        {
            if (s_groundMaskReady)
                return;
            s_groundMaskReady = true;
            int layer = LayerMask.NameToLayer("Ground");
            s_groundMask = layer >= 0 ? (1 << layer) : (1 << 9);
        }

        private static bool HasGroundSupport(float x, float z)
        {
            EnsureGroundMask();
            return Physics.Raycast(
                new Vector3(x, GroundRayStartY, z),
                Vector3.down,
                GroundRayDistance,
                s_groundMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>官方 GridNavSpace 只排除格子占用物，虚空格仍可走——开局用
        /// Ground 层射线把无 Col_Floor 的格标为不可走（每场景一次）。</summary>
        private static void EnsureNavVoidMask()
        {
            if (s_navVoidPatched)
                return;
            try
            {
                var gameUtilsType = GameApi.Find("GameUtils");
                if (gameUtilsType == null)
                    return;
                var getNavSpace = gameUtilsType.GetMethod("GetGridNavSpace",
                    BindingFlags.Public | BindingFlags.Static);
                if (getNavSpace == null)
                    return;
                var navSpace = getNavSpace.Invoke(null, null);
                if (navSpace == null)
                    return;

                var navSpaceType = navSpace.GetType();
                var nodeMapField = navSpaceType.GetField("m_nodeMap",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var offsetField = navSpaceType.GetField("m_mapOffset",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var gridMgrField = navSpaceType.GetField("m_gridManager",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (nodeMapField == null || offsetField == null || gridMgrField == null)
                    return;

                var nodeMap = nodeMapField.GetValue(navSpace) as bool[,];
                if (nodeMap == null)
                    return; // GridNavSpace.Start 尚未构建 nodeMap，下轮老鼠再试

                var offset = offsetField.GetValue(navSpace);
                var gridMgr = gridMgrField.GetValue(navSpace);
                if (offset == null || gridMgr == null)
                    return;

                var point2Type = GameApi.Find("Point2");
                var gridIndexType = GameApi.Find("GridIndex");
                if (point2Type == null || gridIndexType == null)
                    return;

                int offsetX = (int)point2Type.GetField("X").GetValue(offset);
                int offsetY = (int)point2Type.GetField("Y").GetValue(offset);
                var getPosMethod = gridMgr.GetType().GetMethod("GetPosFromGridLocation",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getPosMethod == null)
                    return;

                int blocked = 0;
                int width = nodeMap.GetLength(0);
                int height = nodeMap.GetLength(1);
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        if (!nodeMap[i, j])
                            continue;
                        int gx = i - offsetX;
                        int gz = j - offsetY;
                        var gridIndex = Activator.CreateInstance(gridIndexType, gx, 0, gz);
                        var worldPos = (Vector3)getPosMethod.Invoke(gridMgr, new[] { gridIndex });
                        if (!HasGroundSupport(worldPos.x, worldPos.z))
                        {
                            nodeMap[i, j] = false;
                            blocked++;
                        }
                    }
                }
                s_navVoidPatched = true;
                if (blocked > 0)
                    Log("[RatHeist] GridNav 虚空格已屏蔽: " + blocked + " 格");
            }
            catch (Exception ex)
            {
                if (!s_navVoidPatchWarned)
                {
                    s_navVoidPatchWarned = true;
                    LogWarn("[RatHeist] GridNav 虚空屏蔽失败（仍用移动中射线守卫）: " + ex.Message);
                }
            }
        }

        private bool TryNavMoveTo(Vector3 pos)
        {
            EnsureNavVoidMask();
            if (!HasGroundSupport(pos.x, pos.z))
            {
                LogWarn("[RatHeist] 目标无地板，取消寻路: " + name + " → " + pos.ToString("0.##"));
                return false;
            }
            RatApi.NavMoveTo(_nav, pos);
            return true;
        }

        /// <summary>移动中踏入虚空 → 立刻停导航（防 GridNavigator 沿空路径滑行）。</summary>
        private bool AbortNavIfOverVoid()
        {
            if (_rat == null || HasGroundSupport(_rat.transform.position.x, _rat.transform.position.z))
                return false;
            LogWarn("[RatHeist] 踏入虚空，中止寻路: " + name);
            RatApi.NavClear(_nav);
            return true;
        }

        // ==================== 显隐与移动 ====================

        private void ShowRat()
        {
            if (_rat == null)
                return;
            _rat.transform.position = _homePos;
            SetRatVisible(true);
            EnsureRatNetworkSync();
            TryHookInteraction();
        }

        /// <summary>藏进工作台底下：移到台下 + 渲染/碰撞/动画全关（不挡交互与寻路）。</summary>
        private void HideRat()
        {
            if (_rat == null)
                return;
            _rat.transform.position = _homePos + Vector3.down * HideDropY;
            SetRatVisible(false);
            RatApi.NavClear(_nav);
        }

        private void SetRatVisible(bool visible)
        {
            if (_ratRenderers != null)
            {
                for (int i = 0; i < _ratRenderers.Length; i++)
                {
                    if (_ratRenderers[i] != null)
                        _ratRenderers[i].enabled = visible;
                }
            }
            if (_ratColliders != null)
            {
                for (int i = 0; i < _ratColliders.Length; i++)
                {
                    if (_ratColliders[i] != null)
                        _ratColliders[i].enabled = visible; // 隐藏时不挡交互/移动
                }
            }
            var animator = _rat.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = visible;
        }

        /// <summary>晚于实体扫描生成的老鼠补挂 Interactable 同步器（Server/Client
        /// Interactable）。根因：wrapper 缺 PseudoPrefab 时老鼠 5s 后才兜底实例化，
        /// 会错过 AsyncScanEntities 窗口。</summary>
        private void EnsureRatNetworkSync()
        {
            if (_rat == null)
                return;
            EnsureRatInteractable(_rat);
            if (!GameApi.IsSynchronisationActive() || GameApi.HasEntityEntry(_rat))
                return;
            if (GameApi.ServerRegisterObject(_rat))
                Log("[RatHeist] 老鼠已补注册网络实体（Interactable 同步器）: " + name);
            else
                LogWarn("[RatHeist] 老鼠网络实体补注册失败（打鼠可能无效）: " + name);
        }

        /// <summary>确保老鼠根节点有 Interactable（rat.prefab 自带；兜底实例化路径保险）。</summary>
        private static void EnsureRatInteractable(GameObject rat)
        {
            if (rat == null || RatApi.InteractableType == null)
                return;
            if (rat.GetComponent(RatApi.InteractableType) != null)
                return;
            rat.AddComponent(RatApi.InteractableType);
            Log("[RatHeist] 补挂 Interactable: " + rat.name);
        }

        /// <summary>老鼠会移动：StaticGridLocation 占格不更新会导致 gridSelection
        /// 关卡面对可见老鼠却打不到；无占格组件时补 DynamicGridLocation。</summary>
        private static void EnsureRatGridLocation(GameObject rat)
        {
            if (rat == null)
                return;
            var dynamicType = RatApi.DynamicGridLocationType;
            if (dynamicType == null)
                return;
            if (rat.GetComponent(dynamicType) != null)
                return;
            foreach (var component in rat.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                if (component.GetType().Name != "StaticGridLocation")
                    continue;
                var go = component.gameObject;
                DestroyImmediate(component);
                go.AddComponent(dynamicType);
                Log("[RatHeist] StaticGridLocation → DynamicGridLocation: " + rat.name);
                return;
            }
            rat.AddComponent(dynamicType);
            Log("[RatHeist] 补挂 DynamicGridLocation（占格随移动）: " + rat.name);
        }

        /// <summary>服务端：把本鼠的被打回调挂到 ServerInteractable（同步系统在
        /// 实体扫描后自动挂载该组件；RegisterTriggerCallbacks 官方委托通道，
        /// 玩家按交互键 → ChefEventMessage.TriggerInteract → 此回调）。</summary>
        private void TryHookInteraction()
        {
            if (_hitHooked || _rat == null || !_isServer)
                return;
            try
            {
                var serverInteract = RatApi.GetComponent(_rat, RatApi.ServerInteractableType);
                if (serverInteract == null || RatApi.RegisterTriggerCallbacksMethod == null
                    || RatApi.BeginInteractCallbackType == null)
                    return;
                var method = typeof(RatHeist).GetMethod("OnRatHit",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null)
                {
                    LogWarn("[RatHeist] OnRatHit 反射失败: " + name);
                    return;
                }
                var d = Delegate.CreateDelegate(RatApi.BeginInteractCallbackType, this, method);
                RatApi.RegisterTriggerCallbacksMethod.Invoke(serverInteract, new object[] { d });
                _hitHooked = true;
                Log("[RatHeist] 打鼠交互已挂载: " + name);
            }
            catch (Exception ex)
            {
                LogWarn("[RatHeist] 打鼠交互挂载异常: " + name + ": " + ex.Message);
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;
            if (root.name == childName)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>宿主（Assembly-CSharp）类型与 API 的反射缓存。类型缺失时安全返回
        /// null（降级不抛）。通用工具（Find/Field/GetAssetBundle/IsServerMachine/
        /// IsSynchronisationActive/FindAll/GetComponent*）复用 core GameApi。</summary>
        private static class RatApi
        {
            // —— 类型 ——
            public static readonly Type GridNavigatorType = Find("GridNavigator");
            public static readonly Type AttachStationType = Find("AttachStation");
            public static readonly Type ServerAttachStationType = Find("ServerAttachStation");
            public static readonly Type InteractableType = Find("Interactable");
            public static readonly Type DynamicGridLocationType = Find("DynamicGridLocation");
            public static readonly Type ServerInteractableType = Find("ServerInteractable");
            public static readonly Type PlateType = Find("Plate");
            public static readonly Type CookableContainerType = Find("CookableContainer");
            public static readonly Type CookablePreparationContainerType = Find("CookablePreparationContainer");
            public static readonly Type PreparationContainerType = Find("PreparationContainer");
            public static readonly Type MixableContainerType = Find("MixableContainer");
            public static readonly Type HeatedCookingStationType = Find("HeatedCookingStation");
            public static readonly Type CookingStationType = Find("CookingStation");
            public static readonly Type MixingStationType = Find("MixingStation");
            public static readonly Type HeatedStationType = Find("HeatedStation");
            public static readonly Type IngredientPropertiesType = Find("IngredientPropertiesComponent");
            /// <summary>食材标记（真物件实证 2026-09-19：本体生食材 Tomato/Egg 上
            /// 只有 IngredientDisposalBehaviour + WorkableItem，【没有】
            /// IngredientPropertiesComponent——后者仅切好食材（ChoppedX）才有
            /// （成为订单组成后的 IOrderDefinition）。只认 IPC 会把所有生食材
            /// 漏掉（用户现象：没切的/地上的食材都不偷）。垃圾桶识别食材走
            /// IDisposalBehaviour=IngredientDisposalBehaviour，生/切好全有）。</summary>
            public static readonly Type IngredientDisposalType = Find("IngredientDisposalBehaviour");
            public static readonly Type CarryableItemType = Find("CarryableItem");
            public static readonly Type TimeManagerType = Find("TimeManager");
            public static readonly Type NetworkUtilsType = Find("NetworkUtils");

            // —— AttachStation.m_attachPoint（public，物品挂点=台面内容的双端视图）——
            public static readonly FieldInfo AttachPointField = Field(AttachStationType, "m_attachPoint");

            // —— GridNavigator 驱动 ——
            public static readonly MethodInfo NavMoveToMethod = Safe(delegate
            {
                return GridNavigatorType != null
                    ? GridNavigatorType.GetMethod("MoveToTarget", new[] { typeof(Vector3) })
                    : null;
            });
            public static readonly MethodInfo NavClearMethod = Safe(delegate
            {
                return GridNavigatorType != null
                    ? GridNavigatorType.GetMethod("ClearTarget", Type.EmptyTypes)
                    : null;
            });
            public static readonly MethodInfo NavCompletedMethod = Safe(delegate
            {
                return GridNavigatorType != null
                    ? GridNavigatorType.GetMethod("HasCompletedRoute", Type.EmptyTypes)
                    : null;
            });
            public static readonly MethodInfo NavSetSpeedMethod = Safe(delegate
            {
                return GridNavigatorType != null
                    ? GridNavigatorType.GetMethod("SetSpeedModifier", new[] { typeof(float) })
                    : null;
            });

            // —— 台面偷取（官方权威流程：TakeItem 发 AttachStationMessage + referral 清理）——
            public static readonly MethodInfo StationTakeItemMethod = Safe(delegate
            {
                return ServerAttachStationType != null
                    ? ServerAttachStationType.GetMethod("TakeItem", Type.EmptyTypes)
                    : null;
            });

            // —— 老鼠携带：只用 ServerPhysicalAttachment.Attach/Detach（PhysicalAttachMessage
            //     双端一致）。禁止 ChefCarryMessage——ClientPlayerAttachmentCarrier.TakeItem
            //     在主机联机下会对已 Detach 的 ClientPhysicalAttachment 抛 MissingReferenceException。
            public static readonly Type ServerCarrierType = Find("ServerPlayerAttachmentCarrier");
            public static readonly Type PlayerAttachmentCarrierType = Find("PlayerAttachmentCarrier");
            public static readonly FieldInfo CarrierAttachPointsField = Field(
                PlayerAttachmentCarrierType, "m_attachPoints");
            public static readonly FieldInfo ServerCarrierObjectsField = Field(
                ServerCarrierType, "m_carriedObjects");
            public static readonly Type IAttachmentType = Find("IAttachment");
            public static readonly MethodInfo AttachmentAccessGameObjectMethod = Safe(delegate
            {
                return IAttachmentType != null
                    ? IAttachmentType.GetMethod("AccessGameObject", Type.EmptyTypes)
                    : null;
            });
            public static readonly Type ServerPhysicalAttachmentType = Find("ServerPhysicalAttachment");
            public static readonly Type IParentableType = Find("IParentable");
            public static readonly MethodInfo AttachMethod = Safe(delegate
            {
                return ServerPhysicalAttachmentType != null && IParentableType != null
                    ? ServerPhysicalAttachmentType.GetMethod("Attach", new[] { IParentableType })
                    : null;
            });
            public static readonly MethodInfo DetachMethod = Safe(delegate
            {
                return ServerPhysicalAttachmentType != null
                    ? ServerPhysicalAttachmentType.GetMethod("Detach", Type.EmptyTypes)
                    : null;
            });
            public static readonly MethodInfo IsAttachedMethod = Safe(delegate
            {
                return ServerPhysicalAttachmentType != null
                    ? ServerPhysicalAttachmentType.GetMethod("IsAttached", Type.EmptyTypes)
                    : null;
            });

            // —— 打鼠交互（ServerInteractable.RegisterTriggerCallbacks）——
            public static readonly Type BeginInteractCallbackType = Safe(delegate
            {
                return ServerInteractableType != null
                    ? ServerInteractableType.GetNestedType("BeginInteractCallback")
                    : null;
            });
            public static readonly MethodInfo RegisterTriggerCallbacksMethod = Safe(delegate
            {
                return ServerInteractableType != null
                    ? ServerInteractableType.GetMethod("RegisterTriggerCallbacks",
                        new[] { BeginInteractCallbackType })
                    : null;
            });

            // —— 物品销毁（SetActive(false) + DestroyEntity 消息，客户端同步）——
            public static readonly MethodInfo DestroyObjectMethod = Safe(delegate
            {
                return NetworkUtilsType != null
                    ? NetworkUtilsType.GetMethod("DestroyObject", new[] { typeof(GameObject) })
                    : null;
            });

            // —— 计时（TimeManager.GetDeltaTime(gameObject)：暂停感知）——
            public static readonly MethodInfo DeltaTimeMethod = Safe(delegate
            {
                return TimeManagerType != null
                    ? TimeManagerType.GetMethod("GetDeltaTime", new[] { typeof(GameObject) })
                    : null;
            });

            private static T Safe<T>(Func<T> f) where T : class
            {
                try
                {
                    return f();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            private static Type Find(string typeName)
            {
                var t = Type.GetType(typeName + ", Assembly-CSharp");
                if (t != null)
                    return t;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(typeName, false);
                    if (t != null)
                        return t;
                }
                return null;
            }

            private static FieldInfo Field(Type type, string fieldName)
            {
                return type != null
                    ? type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    : null;
            }

            // —— 门面（异常吞掉+告警一次，降级语义明确）——
            private static bool _navWarned;

            public static void NavMoveTo(Component nav, Vector3 pos)
            {
                if (nav == null || NavMoveToMethod == null)
                {
                    if (!_navWarned)
                    {
                        _navWarned = true;
                        LogWarn("[RatHeist] NavMoveTo 反射缺失，老鼠将不能移动");
                    }
                    return;
                }
                NavMoveToMethod.Invoke(nav, new object[] { pos });
            }

            public static void NavClear(Component nav)
            {
                if (nav != null && NavClearMethod != null)
                    NavClearMethod.Invoke(nav, null);
            }

            public static bool NavCompleted(Component nav)
            {
                if (nav == null || NavCompletedMethod == null)
                    return true;
                try
                {
                    return (bool)NavCompletedMethod.Invoke(nav, null);
                }
                catch (Exception)
                {
                    return true;
                }
            }

            public static void NavSetSpeed(Component nav, float speed)
            {
                if (nav != null && NavSetSpeedMethod != null)
                    NavSetSpeedMethod.Invoke(nav, new object[] { speed });
            }

            public static GameObject StationTakeItem(Component serverStation)
            {
                if (serverStation == null || StationTakeItemMethod == null)
                    return null;
                try
                {
                    return StationTakeItemMethod.Invoke(serverStation, null) as GameObject;
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] TakeItem 异常: " + ex.Message);
                    return null;
                }
            }

            public static void RatCarry(GameObject rat, GameObject item)
            {
                var carrier = GetComponent(rat, PlayerAttachmentCarrierType);
                var attachment = GetComponent(item, ServerPhysicalAttachmentType);
                if (carrier == null || attachment == null || AttachMethod == null)
                {
                    LogWarn("[RatHeist] 挂接不可用（carrier=" + (carrier != null)
                        + "，attachment=" + (attachment != null) + "）: " + item.name);
                    return;
                }
                try
                {
                    AttachMethod.Invoke(attachment, new object[] { carrier });
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] Attach 异常（物品将落地）: " + ex.Message);
                }
            }

            /// <summary>物理 Detach 落地（PhysicalAttachMessage）；并静默清服务端 carrier
            /// 槽位（不发 ChefCarryMessage，避免客户端 TakeItem 崩溃）。</summary>
            public static void RatDrop(GameObject rat, GameObject item)
            {
                if (item == null)
                    return;
                ClearServerCarrierSlotSilent(rat, item);
                if (!IsAttached(item))
                    return;
                var attachment = GetComponent(item, ServerPhysicalAttachmentType);
                if (attachment == null || DetachMethod == null)
                    return;
                try
                {
                    DetachMethod.Invoke(attachment, null);
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] Detach 异常: " + ex.Message);
                }
            }

            /// <summary>清 ServerPlayerAttachmentCarrier.m_carriedObjects 里对该物的引用，
            /// 不调用 TakeItem（= 不发 ChefCarryMessage）。兼容旧版曾走 CarryItem 的存档。</summary>
            private static void ClearServerCarrierSlotSilent(GameObject rat, GameObject item)
            {
                if (rat == null || item == null || ServerCarrierObjectsField == null
                    || AttachmentAccessGameObjectMethod == null)
                    return;
                var serverCarrier = GetComponent(rat, ServerCarrierType);
                if (serverCarrier == null)
                    return;
                var arr = ServerCarrierObjectsField.GetValue(serverCarrier) as Array;
                if (arr == null)
                    return;
                for (int i = 0; i < arr.Length; i++)
                {
                    var slot = arr.GetValue(i);
                    if (slot == null)
                        continue;
                    GameObject go = null;
                    try
                    {
                        go = AttachmentAccessGameObjectMethod.Invoke(slot, null) as GameObject;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (go != item)
                        continue;
                    arr.SetValue(null, i);
                    return;
                }
            }

            /// <summary>物品是否处于挂接态（被台面/玩家/老鼠持有）。同步器缺失按
            /// 自由态处理（地面物品）。</summary>
            public static bool IsAttached(GameObject go)
            {
                var a = GetComponent(go, ServerPhysicalAttachmentType);
                if (a == null || IsAttachedMethod == null)
                    return false;
                try
                {
                    return (bool)IsAttachedMethod.Invoke(a, null);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public static void DestroyObject(GameObject go)
            {
                if (go == null)
                    return;
                if (DestroyObjectMethod == null)
                {
                    go.SetActive(false); // 降级：仅本地隐藏
                    LogWarn("[RatHeist] NetworkUtils.DestroyObject 反射缺失，本地隐藏物品");
                    return;
                }
                try
                {
                    DestroyObjectMethod.Invoke(null, new object[] { go });
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] 销毁异常: " + ex.Message);
                }
            }

            public static float DeltaTime(GameObject go)
            {
                if (DeltaTimeMethod == null)
                    return Time.deltaTime;
                try
                {
                    return (float)DeltaTimeMethod.Invoke(null, new object[] { go });
                }
                catch (Exception)
                {
                    return Time.deltaTime;
                }
            }

            public static Component GetComponent(GameObject go, Type type)
            {
                if (go == null || type == null)
                    return null;
                return go.GetComponent(type);
            }
        }
    }
}
