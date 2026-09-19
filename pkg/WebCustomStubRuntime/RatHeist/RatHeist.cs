using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>皮肤：retro（默认，原版像素材质）/ dlc08（官方 h18 同款贴图换肤）。</summary>
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
        private const float HideDropY = 1.05f;
        /// <summary>无目标巡逻的最少单程距离（格）。</summary>
        private const float MinPatrolCells = 5f;

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
        private bool _skinApplied;
        private GameObject _targetStation; // 服务端本轮占用的目标台面（s_targets 配套）
        private float _channelWaited;      // 已等待 wrapper 通道的秒数（超过阈值走兜底）

        /// <summary>本机（服务端）目标占用表：多鼠不同时盯同一台面。
        /// 清场：OnSceneChanged（挂 EntryPoint.ResetSceneTickers 链）。</summary>
        private static readonly HashSet<GameObject> s_targets = new HashSet<GameObject>();

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

                // 3. 分端就绪
                _isServer = GameApi.IsServerMachine();
                if (_isServer)
                    HookInteraction();
                Log("[RatHeist] 网络同步就绪: " + name + "（isServer=" + _isServer + "）");

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
            RatApi.NavMoveTo(_nav, targetPos);
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null)
            {
                yield return null;
                if (_fleeing || _rat == null)
                {
                    ReleaseTarget();
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
            RatApi.NavMoveTo(_nav, _homePos);
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null)
            {
                yield return null;
                if (_fleeing || _rat == null)
                {
                    // 掉落已在被打回调里处理；逃亡协程负责回家
                    ReleaseTarget();
                    yield break;
                }
            }
            // —— 到家销毁 ——
            var carried = _carried;
            _carried = null;
            ReleaseTarget();
            if (carried != null)
            {
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
        /// 不可达时卡死（HasCompletedRoute 恒 false 的防御）。</summary>
        private IEnumerator ReturnHome()
        {
            RatApi.NavMoveTo(_nav, _homePos);
            var guard = 0f;
            while (!RatApi.NavCompleted(_nav) && _rat != null && guard < 15f)
            {
                guard += RatApi.DeltaTime(_rat);
                yield return null;
            }
            HideRat();
        }

        /// <summary>被打回调（服务端，ServerInteractable.TriggerInteract 委托）。
        /// 掉落携带物（物理落地，官方 PhysicsObjectSynchroniser 同步）+ 逃亡标记，
        /// 逃亡的实际移动由 FleeHome 协程完成。</summary>
        internal void OnRatHit(GameObject interacter, Vector2 directionXZ)
        {
            if (!_isServer || _fleeing)
                return;
            Log("[RatHeist] 被打: " + name + (interacter != null ? "（by " + interacter.name + "）" : ""));
            _fleeing = true;
            RatApi.NavClear(_nav);
            if (_carried != null)
            {
                RatApi.RatDrop(_rat, _carried); // Detach：物品物理落地
                _carried = null;
            }
            StartCoroutine(FleeHome());
        }

        private IEnumerator FleeHome()
        {
            ShowRat();
            var goHome = ReturnHome();
            while (goHome.MoveNext())
                yield return goHome.Current;
            _fleeing = false;
        }

        // ==================== 客户端影子演出 ====================

        /// <summary>无目标巡逻：走到距家 ≥MinPatrolCells 格的随机可走点再回洞
        /// （用户规则：多鼠关卡里老鼠也要固定出来溜达，不能全程蹲洞）。
        /// 双端本地随机（不追求同步——纯视觉行为，偷/毁仍由服务端事件驱动）。</summary>
        private IEnumerator Patrol()
        {
            ShowRat();
            var ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var dist = UnityEngine.Random.Range(MinPatrolCells + 0.5f, MinPatrolCells + 4f) * CellSize;
            var target = _homePos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
            RatApi.NavMoveTo(_nav, target);
            var guard = 0f;
            while (!RatApi.NavCompleted(_nav) && !_fleeing && _rat != null && guard < 25f)
            {
                guard += RatApi.DeltaTime(_rat);
                yield return null;
                if (_fleeing || _rat == null)
                    yield break; // FleeHome 接管
            }
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
            RatApi.NavMoveTo(_nav, targetPos);
            while (!RatApi.NavCompleted(_nav) && _rat != null
                && (_attachPoint == null || _attachPoint.childCount == 0)
                && StillValidTarget(stationGo, itemGo))
                yield return null;
            if (_rat == null)
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

        /// <summary>dlc08 皮肤：官方 h18 关同款（复古鼠模型 + dlc08 老鼠贴图）。</summary>
        private void ApplySkin()
        {
            if (_skinApplied || _ratRenderers == null || _ratRenderers.Length == 0)
                return;
            _skinApplied = true;
            if (m_skin != "dlc08")
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

        // ==================== 显隐与移动 ====================

        private void ShowRat()
        {
            if (_rat == null)
                return;
            _rat.transform.position = _homePos;
            SetRatVisible(true);
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

        /// <summary>服务端：把本鼠的被打回调挂到 ServerInteractable（同步系统在
        /// 实体扫描后自动挂载该组件；RegisterTriggerCallbacks 官方委托通道，
        /// 玩家按交互键 → ChefEventMessage.TriggerInteract → 此回调）。</summary>
        private void HookInteraction()
        {
            try
            {
                var serverInteract = RatApi.GetComponent(_rat, RatApi.ServerInteractableType);
                if (serverInteract == null || RatApi.RegisterTriggerCallbacksMethod == null
                    || RatApi.BeginInteractCallbackType == null)
                {
                    LogWarn("[RatHeist] ServerInteractable 不可用（组件=" + (serverInteract != null)
                        + "），被打将不生效: " + name);
                    return;
                }
                var method = typeof(RatHeist).GetMethod("OnRatHit",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null)
                {
                    LogWarn("[RatHeist] OnRatHit 反射失败: " + name);
                    return;
                }
                var d = Delegate.CreateDelegate(RatApi.BeginInteractCallbackType, this, method);
                RatApi.RegisterTriggerCallbacksMethod.Invoke(serverInteract, new object[] { d });
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

            // —— 老鼠携带（ServerPlayerAttachmentCarrier.CarryItem/TakeItem 官方优先，
            //     ChefCarryMessage 全网同步；老鼠未被实体扫描同步化（无 Server 同步器，
            //     真机 2026-09-19 现象：物品 TakeItem 后掉地上、老鼠空嘴）时兜底直接
            //     ServerPhysicalAttachment.Attach(PlayerAttachmentCarrier)——prefab 自带
            //     场景组件不依赖同步化，挂点由 EnsureAttachPoints 保证）——
            public static readonly Type ServerCarrierType = Find("ServerPlayerAttachmentCarrier");
            public static readonly Type PlayerAttachmentCarrierType = Find("PlayerAttachmentCarrier");
            public static readonly FieldInfo CarrierAttachPointsField = Field(
                PlayerAttachmentCarrierType, "m_attachPoints");
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
            public static readonly MethodInfo CarryItemMethod = Safe(delegate
            {
                return ServerCarrierType != null
                    ? ServerCarrierType.GetMethod("CarryItem", new[] { typeof(GameObject) })
                    : null;
            });
            public static readonly MethodInfo CarrierTakeItemMethod = Safe(delegate
            {
                return ServerCarrierType != null
                    ? ServerCarrierType.GetMethod("TakeItem", Type.EmptyTypes)
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
                var serverCarrier = GetComponent(rat, ServerCarrierType);
                if (serverCarrier != null && CarryItemMethod != null)
                {
                    try
                    {
                        CarryItemMethod.Invoke(serverCarrier, new object[] { item });
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogWarn("[RatHeist] CarryItem 异常，转兜底挂接: " + ex.Message);
                    }
                }
                // 兜底：直接 Attach 到 prefab 自带的 PlayerAttachmentCarrier
                var carrier = GetComponent(rat, PlayerAttachmentCarrierType);
                var attachment = GetComponent(item, ServerPhysicalAttachmentType);
                if (carrier == null || attachment == null || AttachMethod == null)
                {
                    LogWarn("[RatHeist] 挂接兜底不可用（serverCarrier=" + (serverCarrier != null)
                        + "，carrier=" + (carrier != null) + "，attachment=" + (attachment != null)
                        + "），物品将落地");
                    return;
                }
                try
                {
                    AttachMethod.Invoke(attachment, new object[] { carrier });
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] Attach 兜底异常（物品将落地）: " + ex.Message);
                }
            }

            public static void RatDrop(GameObject rat, GameObject item)
            {
                if (item == null)
                    return;
                var serverCarrier = GetComponent(rat, ServerCarrierType);
                if (serverCarrier != null && CarrierTakeItemMethod != null)
                {
                    try
                    {
                        CarrierTakeItemMethod.Invoke(serverCarrier, null);
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogWarn("[RatHeist] 掉落异常，转兜底: " + ex.Message);
                    }
                }
                var attachment = GetComponent(item, ServerPhysicalAttachmentType);
                if (attachment == null || DetachMethod == null)
                    return;
                try
                {
                    DetachMethod.Invoke(attachment, null); // 物理落地
                }
                catch (Exception ex)
                {
                    LogWarn("[RatHeist] Detach 兜底异常: " + ex.Message);
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
