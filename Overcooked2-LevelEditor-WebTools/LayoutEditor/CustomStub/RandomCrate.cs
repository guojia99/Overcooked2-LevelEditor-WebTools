using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 随机食材箱运行时组件（母本）。
    ///
    /// 宿主程序集（Assembly-CSharp）不可在此编译期引用——本类随「关卡集专属
    /// stub 程序集」（Stub_&lt;set&gt;，Assets/LevelSets/&lt;set&gt;/stub/）编译，
    /// 游戏侧由 OC2LevelRuntimeLoader 插件在加载关卡前 Assembly.Load 该程序集，
    /// 场景里的脚本引用按程序集名解析（与 LevelEditorStub 同机制）。
    /// 所有游戏类型经 GameApi 反射缓存访问；Unity / LevelEditorStub 类型直接引用。
    ///
    /// 关键时序（时序错则全部失效，见各步注释）：
    /// 1. 等真实箱子：子孙中递归找 PickupItemSpawner（游戏原生类型，编辑器/游戏通用；
    ///    游戏侧 OC2DIYLevel 模组按 stub 标记生成真实箱子，没有 LevelEditor.PseudoPrefab
    ///    类型，切勿再反射它）；
    /// 2. 等网络同步完成 MultiplayerController.IsSynchronisationActive()：
    ///    - Server/Client 同步器组件（含 ServerPickupItemSpawner）在
    ///      AsyncScanEntities 协程（等所有玩家加载完成）之后才 AddComponent，
    ///      场景加载初期不存在；
    ///    - 游戏的 ClientItemCrateCosmeticDecisions 在同步握手时绘制首食材
    ///      箱盖图标（只绘一次）——问号必须晚于它，否则被覆盖；
    /// 3. 服务端判定 = ConnectionStatus.IsHost() || !IsInSession()（游戏标准写法，
    ///    见 EntitySerialisationRegistry.AddSynchronisedType）；
    /// 4. 注册全部候选为可生成 prefab（服务端带取出回调），服务端掷首个产出；
    /// 5. 绘制问号（渲染器查找顺序镜像游戏 ClientItemCrateCosmeticDecisions：
    ///    盖子 Skinned → 盖子 Mesh → 根 Mesh 兜底，兼容木纹·中秋等新结构皮肤）。
    ///
    /// 抽取机制（取出即掷 · 配额递减）：m_weights 为初始配额（默认 5）；每次取出
    /// （服务端 ServerSpawnPrefab 同步触发注册回调，见 NetworkUtils）该食材配额 -1，
    /// 归零回满初始值，随后立即按剩余配额掷下一个。状态纯服务端，客户端零同步负担。
    /// 兼容性：未装加载器/程序集缺失时本组件不存在（脚本缺失为惰性警告）；老模组下
    /// 箱子回落为 PseudoPrefabDispenserStub.spawnerItemPrefabSO 的固定食材箱。
    /// </summary>
    public class RandomCrate : MonoBehaviour
    {
        [SerializeField] public PseudoPrefabSO[] m_itemSOs;

        /// <summary>各食材初始权重/配额。每次取出 -1，归零回满初始值；缺省按 5。</summary>
        [SerializeField] public float[] m_weights;

        /// <summary>箱盖问号贴图（Assets/commonW1/question_mark/question_mark_chef_hat.png，随 commonW1 bundle 分发）。
        /// 为空时保留游戏绘制的首食材图标（自然降级）。</summary>
        [SerializeField] public Texture2D m_questionMarkTexture;

        // ---- 运行时状态（不序列化：每次进关卡/重开关卡自然重置） ----
        private List<GameObject> _prefabs;
        private List<float> _initial;
        private float[] _remaining;
        private bool _isServer;
        private Component _spawner;

        // ---- 日志桥（金丝雀版本号：真机日志确认 DLL 新鲜度用） ----
        private const string Version = "v5";
        private static MethodInfo _bridgeLog;
        private static bool _bridgeSearched;

        /// <summary>优先转发到 OC2LevelRuntimeLoader 插件日志（LogOutput.log 同一
        /// 来源可见，Debug.Log 可能被 BepInEx 级别过滤）；无加载器（编辑器宿主）
        /// 回落 Unity Console。</summary>
        private static void Log(string msg)
        {
            Bridge(msg, false);
        }

        private static void LogWarn(string msg)
        {
            Bridge(msg, true);
        }

        private static void Bridge(string msg, bool warn)
        {
            if (!_bridgeSearched)
            {
                _bridgeSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("OC2LevelRuntimeLoader.LevelRuntimeLoader", false);
                    if (t == null)
                        continue;
                    _bridgeLog = t.GetMethod("LogFromCrate", BindingFlags.Public | BindingFlags.Static);
                    break;
                }
            }
            if (_bridgeLog != null)
            {
                try
                {
                    _bridgeLog.Invoke(null, new object[] { msg, warn });
                    return;
                }
                catch (Exception)
                {
                    // 桥失败回落 Debug
                }
            }
            if (warn)
                Debug.LogWarning(msg);
            else
                Debug.Log(msg);
        }

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield，用嵌套枚举器
        /// 模式给整个协程套异常捕获——任何未捕获异常（含 GameApi 静态构造的
        /// TypeInitializationException）都打到日志桥上，不再静默死亡。</summary>
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
                    LogWarn("[RandomCrate] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            Log("[RandomCrate " + Version + "] 初始化: " + name
                + "（场景 " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                + "，配置候选 " + (m_itemSOs != null ? m_itemSOs.Length : 0)
                + "，权重 " + FormatFloats(m_weights)
                + "，问号贴图 " + (m_questionMarkTexture != null ? m_questionMarkTexture.name : "无") + "）");
            // 反射自检：强制触发 GameApi 静态构造（异常会被外层捕获打日志），
            // 并报告每个关键反射目标在游戏侧是否命中。
            // 注意：不依赖 LevelEditor.PseudoPrefab / PseudoPrefabManager——游戏侧
            // AppDomain 没有这些类型（OC2DIYLevel 模组只按 stub 标记加功能组件），
            // 真实箱子一律靠「子孙里找 PickupItemSpawner」定位、bundle 靠
            // AssetBundle.GetAllLoadedAssetBundles 按名匹配（v4 起，编辑器/游戏通用）。
            Log("[RandomCrate] 反射自检: " + name
                + "（PickupItemSpawnerType=" + (GameApi.PickupItemSpawnerType != null)
                + "，IsSyncActiveMethod=" + (GameApi.IsSyncActiveMethod != null)
                + "，RegisterSpawnable=" + (GameApi.RegisterSpawnableMethod != null)
                + "，RegisterSpawnableCallback=" + (GameApi.RegisterSpawnableCallbackMethod != null) + "）");
            // 外层循环：宿主重建真实箱子后（重开关卡等场景）重新走完整装配。
            while (true)
            {
                // 定位真实箱子：子孙中递归找 PickupItemSpawner（游戏原生类型）。
                var spawner = FindSpawnerInDescendants();
                GameObject child = spawner != null ? spawner.gameObject : null;
                if (child == null)
                    Log("[RandomCrate] 等待真实箱子 prefab: " + name);
                var deadline = Time.realtimeSinceStartup + 20f;
                var lastChildBeat = Time.realtimeSinceStartup;
                while (child == null)
                {
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        LogWarn("[RandomCrate] 等待真实箱子 prefab 超时（20s），本轮跳过: " + name
                            + "（直接子物体: " + DirectChildrenNames() + "）");
                        child = null;
                        break;
                    }
                    if (Time.realtimeSinceStartup - lastChildBeat > 5f)
                    {
                        lastChildBeat = Time.realtimeSinceStartup;
                        Log("[RandomCrate] 仍在等待真实箱子 prefab: " + name);
                    }
                    yield return null;
                    spawner = FindSpawnerInDescendants();
                    child = spawner != null ? spawner.gameObject : null;
                }
                if (child == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                Log("[RandomCrate] 真实箱子就绪: " + name + " → " + child.name);
                // 载入全部食材 prefab：已加载 bundle 按名直读（AssetBundle.
                // GetAllLoadedAssetBundles，避开宿主 PseudoPrefabManager.LoadAsset
                // 对 null 结果触发的 DeInit/Init 全量重置连锁，且游戏侧无该类型）。
                // 同 assetPath 的重复条目合并（配额相加），保证名字匹配无歧义。
                _prefabs = new List<GameObject>();
                _initial = new List<float>();
                var assetPaths = new List<string>();
                for (int i = 0; i < m_itemSOs.Length; i++)
                {
                    var so = m_itemSOs[i];
                    if (so == null || string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                    {
                        LogWarn("[RandomCrate] 跳过无效食材配置项 #" + i + ": " + name);
                        continue;
                    }
                    float w = m_weights != null && i < m_weights.Length && m_weights[i] >= 1f ? m_weights[i] : 5f;
                    try
                    {
                        var bundle = GameApi.GetAssetBundle(so.bundleName);
                        if (bundle == null)
                        {
                            LogWarn("[RandomCrate] bundle 未加载，跳过食材 " + so.prefabName + ": " + name);
                            continue;
                        }
                        var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
                        if (prefab == null)
                        {
                            LogWarn("[RandomCrate] 食材 prefab 加载失败 " + so.assetPath + ": " + name);
                            continue;
                        }
                        var merged = assetPaths.IndexOf(so.assetPath);
                        if (merged >= 0)
                        {
                            _initial[merged] = _initial[merged] + w;
                            continue;
                        }
                        assetPaths.Add(so.assetPath);
                        _prefabs.Add(prefab);
                        _initial.Add(w);
                    }
                    catch (Exception ex)
                    {
                        LogWarn("[RandomCrate] 食材加载异常（bundle 缺失?）" + so.bundleName + "/" + so.assetPath
                            + ": " + ex.Message);
                    }
                }
                if (_prefabs.Count == 0)
                {
                    LogWarn("[RandomCrate] 无可用食材，本轮跳过: " + name);
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                Log("[RandomCrate] 候选加载完成: " + name + "（" + PrefabSummary() + "）");

                _remaining = new float[_prefabs.Count];
                for (int i = 0; i < _initial.Count; i++)
                    _remaining[i] = _initial[i];

                _spawner = spawner;

                // 预画问号：childGameObject 就绪即显示，不等网络握手（开局不再先显示
                // 首食材图标）。同步握手时游戏 ClientItemCrateCosmeticDecisions 会重绘
                // 首食材图标，随后下面的权威重画再覆盖回问号。
                PaintQuestionMark(child, m_questionMarkTexture);

                // 等网络同步完成（见类头注释第 2 点）。同步是可玩局面的必要环节，
                // 不设超时；child 失效则回外层重装配。心跳日志便于定位"卡在等同步"。
                var waitStart = Time.realtimeSinceStartup;
                var lastBeat = waitStart;
                var waitingLogged = false;
                while (!GameApi.IsSynchronisationActive())
                {
                    if (child == null)
                        break;
                    if (!waitingLogged)
                    {
                        waitingLogged = true;
                        Log("[RandomCrate] 等待网络同步完成: " + name);
                    }
                    else if (Time.realtimeSinceStartup - lastBeat > 5f)
                    {
                        lastBeat = Time.realtimeSinceStartup;
                        Log("[RandomCrate] 仍在等待网络同步（已等 "
                            + (int)(lastBeat - waitStart) + "s）: " + name);
                    }
                    yield return null;
                }
                if (child == null)
                {
                    yield return null;
                    continue;
                }

                // 服务端判定（游戏标准写法：主机 或 未在联机会话=单机）
                _isServer = GameApi.IsServerMachine();
                Log("[RandomCrate] 网络同步就绪: " + name + "（isServer=" + _isServer + "）");

                // 注册全部候选（服务端与客户端都要——客户端网络实例化按 spawner 的
                // 注册表解析 spawnableID；静态查表，同步后注册依然有效）。
                // 服务端注册带回调：取出事件（ServerSpawnPrefab 内同步触发）驱动配额递减。
                var callback = _isServer ? CreateSpawnCallback() : null;
                var registered = 0;
                for (int i = 0; i < _prefabs.Count; i++)
                {
                    try
                    {
                        if (callback != null)
                            GameApi.RegisterSpawnable(child, _prefabs[i], callback);
                        else
                            GameApi.RegisterSpawnable(child, _prefabs[i]);
                        registered++;
                    }
                    catch (Exception ex)
                    {
                        LogWarn("[RandomCrate] 注册生成 prefab 失败 " + _prefabs[i].name + ": " + ex.Message);
                    }
                }
                Log("[RandomCrate] 候选注册完成: " + name + "（" + registered + "/" + _prefabs.Count
                    + (callback != null ? "，含取出回调" : "，无取出回调（客户端）") + "）");

                // 问号绘制：必须晚于游戏 ClientItemCrateCosmeticDecisions 的首食材
                // 图标绘制（发生在同步握手内），此刻已安全。
                PaintQuestionMark(child, m_questionMarkTexture);

                // 服务端：初始掷一次；后续每次取出由回调驱动（递减 → 回满 → 再掷）。
                // 本循环只负责监视 child 失效（重开关卡）后回到外层重装配。
                if (_isServer)
                    RollNext();
                while (child != null && _spawner != null)
                    yield return new WaitForSeconds(0.5f);
                yield return null;
            }
        }

        /// <summary>按当前剩余配额随机选择下一个产出食材。</summary>
        private void RollNext()
        {
            if (_spawner == null || _prefabs == null || _prefabs.Count == 0)
                return;
            float total = 0f;
            for (int i = 0; i < _remaining.Length; i++)
                total += _remaining[i];
            var r = UnityEngine.Random.value * total;
            var pick = _prefabs[_prefabs.Count - 1];
            for (int i = 0; i < _prefabs.Count; i++)
            {
                r -= _remaining[i];
                if (r <= 0f)
                {
                    pick = _prefabs[i];
                    break;
                }
            }
            GameApi.SetItemPrefab(_spawner, pick);
            Log("[RandomCrate] 掷出下一个食材: " + name + " → " + pick.name
                + "（剩余配额 " + FormatFloats(_remaining) + "）");
        }

        /// <summary>取出事件（仅服务端注册本回调）。匹配被取出的食材 → 配额 -1 →
        /// 归零回满初始值 → 按新配额掷下一个。</summary>
        private void OnServerItemSpawned(GameObject spawned)
        {
            if (!_isServer || spawned == null || _prefabs == null)
                return;
            try
            {
                var n = spawned.name;
                if (n.EndsWith("(Clone)"))
                    n = n.Substring(0, n.Length - "(Clone)".Length);
                var index = -1;
                for (int i = 0; i < _prefabs.Count; i++)
                {
                    if (_prefabs[i] != null && _prefabs[i].name == n)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    LogWarn("[RandomCrate] 取出物品无法匹配候选列表: " + spawned.name);
                    return;
                }
                _remaining[index] -= 1f;
                if (_remaining[index] <= 0f)
                    _remaining[index] = _initial[index];
                Log("[RandomCrate] 取出: " + name + " → " + n
                    + "（剩余配额 " + FormatFloats(_remaining) + "）");
                RollNext();
            }
            catch (Exception ex)
            {
                LogWarn("[RandomCrate] 取出回调异常: " + ex.Message);
            }
        }

        private System.Delegate CreateSpawnCallback()
        {
            var delegateType = GameApi.VoidGenericGameObjectType;
            if (delegateType == null)
                return null;
            var method = typeof(RandomCrate).GetMethod("OnServerItemSpawned",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                return null;
            return System.Delegate.CreateDelegate(delegateType, this, method);
        }

        /// <summary>把箱盖材质换成问号贴图（公开静态：编辑器侧 SyncQuestionMarks
        /// 经反射复用本方法，单一实现）。渲染器查找顺序镜像游戏
        /// ClientItemCrateCosmeticDecisions（盖子 Skinned → 盖子 Mesh → 根 Mesh 兜底），
        /// 兼容木纹·中秋等「盖子=普通 MeshRenderer、根无渲染器」的新结构皮肤。
        /// UV 换算与其一致（subRect=整图）：offset=(0, 1/uvScale.y)，scale=(uvScale.x, -uvScale.y)。</summary>
        public static void PaintQuestionMark(GameObject child, Texture2D texture)
        {
            if (child == null || texture == null)
                return;
            try
            {
                var decisions = child.GetComponent(GameApi.ItemCrateCosmeticDecisionsType);
                if (decisions == null || GameApi.LidMeshNameField == null)
                    return;
                var lidName = GameApi.LidMeshNameField.GetValue(decisions) as string;
                if (string.IsNullOrEmpty(lidName))
                    return;
                var lid = FindChildRecursive(child.transform, lidName);
                Renderer renderer = null;
                var rendererPath = "";
                if (lid != null)
                {
                    renderer = lid.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null)
                        rendererPath = "盖子 SkinnedMeshRenderer";
                    if (renderer == null)
                    {
                        renderer = lid.GetComponent<MeshRenderer>();
                        if (renderer != null)
                            rendererPath = "盖子 MeshRenderer";
                    }
                }
                if (renderer == null)
                {
                    renderer = child.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        rendererPath = "根 MeshRenderer";
                }
                if (renderer == null)
                {
                    LogWarn("[RandomCrate] 找不到箱盖渲染器: " + child.name);
                    return;
                }
                var matNumber = GameApi.MaterialNumberField != null ? (int)GameApi.MaterialNumberField.GetValue(decisions) : 0;
                var uvScale = GameApi.UvScaleField != null ? (Vector2)GameApi.UvScaleField.GetValue(decisions) : Vector2.one;
                var materials = renderer.sharedMaterials;
                if (materials == null || matNumber < 0 || matNumber >= materials.Length || materials[matNumber] == null)
                {
                    LogWarn("[RandomCrate] 箱盖材质数组无效: " + child.name);
                    return;
                }
                var material = new Material(materials[matNumber]);
                material.mainTexture = texture;
                // 问号贴图需左右翻转（盖子 UV 采样方向与独立贴图相反）：U 轴取负并平移一个周期
                material.mainTextureOffset = new Vector2(uvScale.x, 1f / uvScale.y);
                material.mainTextureScale = new Vector2(-uvScale.x, -uvScale.y);
                materials[matNumber] = material;
                renderer.sharedMaterials = materials;
                Log("[RandomCrate] 问号绘制成功: " + child.name + "（" + rendererPath
                    + "，材质 #" + matNumber + "，贴图 " + texture.name + "）");
            }
            catch (Exception ex)
            {
                LogWarn("[RandomCrate] 绘制问号图标失败: " + ex.Message);
            }
        }

        /// <summary>子孙中递归查找 PickupItemSpawner（真实箱子的定位锚点，游戏原生
        /// 类型，编辑器/游戏通用——不依赖 LevelEditor.PseudoPrefab.childGameObject，
        /// 游戏侧 OC2DIYLevel 模组根本没有 LevelEditor 命名空间的类型）。</summary>
        private Component FindSpawnerInDescendants()
        {
            var spawnerType = GameApi.PickupItemSpawnerType;
            if (spawnerType == null)
                return null;
            foreach (var comp in GetComponentsInChildren(spawnerType, true))
            {
                if (comp != null)
                    return comp;
            }
            return null;
        }

        private string DirectChildrenNames()
        {
            var names = new List<string>();
            for (int i = 0; i < transform.childCount; i++)
                names.Add(transform.GetChild(i).name);
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "无";
        }

        /// <summary>按名称深度查找子节点（宿主的 Transform.FindChildRecursive 是
        /// Assembly-CSharp 扩展方法，编译期不可引用，这里实现等价逻辑）。</summary>
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

        private string PrefabSummary()
        {
            var parts = new List<string>();
            for (int i = 0; i < _prefabs.Count; i++)
                parts.Add((_prefabs[i] != null ? _prefabs[i].name : "?") + "×" + _initial[i].ToString("0.##"));
            return string.Join(", ", parts.ToArray());
        }

        private static string FormatFloats(float[] values)
        {
            if (values == null || values.Length == 0)
                return "无";
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                parts[i] = values[i].ToString("0.##");
            return string.Join(",", parts);
        }

        /// <summary>Assembly-CSharp（宿主程序集）类型的反射缓存。类型缺失时安全返回 null。</summary>
        private static class GameApi
        {
            public static readonly Type VoidGenericType = Find("VoidGeneric`1");
            public static readonly Type VoidGenericGameObjectType = Safe(delegate
            {
                return VoidGenericType != null ? VoidGenericType.MakeGenericType(typeof(GameObject)) : null;
            });

            public static readonly Type NetworkUtilsType = Find("NetworkUtils");
            // 注意：GetMethod 的 types 数组含 null 元素会抛 ArgumentNullException——
            // 静态构造抛异常 = TypeInitializationException，协程无声死亡，一律 Safe 包裹。
            public static readonly MethodInfo RegisterSpawnableMethod = Safe(delegate
            {
                return NetworkUtilsType != null
                    ? NetworkUtilsType.GetMethod("RegisterSpawnablePrefab", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(GameObject), typeof(GameObject) }, null)
                    : null;
            });
            public static readonly MethodInfo RegisterSpawnableCallbackMethod = Safe(delegate
            {
                return NetworkUtilsType != null && VoidGenericGameObjectType != null
                    ? NetworkUtilsType.GetMethod("RegisterSpawnablePrefab", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(GameObject), typeof(GameObject), VoidGenericGameObjectType }, null)
                    : null;
            });

            public static readonly Type PickupItemSpawnerType = Find("PickupItemSpawner");
            public static readonly FieldInfo ItemPrefabField = Field(PickupItemSpawnerType, "m_itemPrefab");

            // 同步状态与服务端判定（游戏标准 API，见类头注释）
            public static readonly Type MultiplayerControllerType = Find("MultiplayerController");
            public static readonly MethodInfo IsSyncActiveMethod = Method(
                MultiplayerControllerType, "IsSynchronisationActive", Type.EmptyTypes);
            public static readonly Type ConnectionStatusType = Find("ConnectionStatus");
            public static readonly MethodInfo IsHostMethod = Method(
                ConnectionStatusType, "IsHost", Type.EmptyTypes);
            public static readonly MethodInfo IsInSessionMethod = Method(
                ConnectionStatusType, "IsInSession", Type.EmptyTypes);

            public static readonly Type ItemCrateCosmeticDecisionsType = Find("ItemCrateCosmeticDecisions");
            public static readonly FieldInfo LidMeshNameField = Field(ItemCrateCosmeticDecisionsType, "m_crateLidMeshName");
            public static readonly FieldInfo MaterialNumberField = Field(ItemCrateCosmeticDecisionsType, "m_materialNumber");
            public static readonly FieldInfo UvScaleField = Field(ItemCrateCosmeticDecisionsType, "m_uvScale");

            /// <summary>静态字段初始化防爆：任何反射异常返回 null（缺失按降级处理），
            /// 绝不让 TypeInitializationException 逃出静态构造。</summary>
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
                return type != null ? type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;
            }

            private static MethodInfo Method(Type type, string methodName, Type[] args)
            {
                return type != null ? type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, args, null) : null;
            }

            /// <summary>按名找已加载的 AssetBundle（Unity 原生 API，不再反射
            /// PseudoPrefabManager.GetAssetBundle——游戏侧没有该类型）。
            /// bundleName 为 bundle 文件名（如 common01 / bundle18）。</summary>
            public static AssetBundle GetAssetBundle(string bundleName)
            {
                if (string.IsNullOrEmpty(bundleName))
                    return null;
                foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (b == null)
                        continue;
                    if (string.Equals(b.name, bundleName, StringComparison.OrdinalIgnoreCase))
                        return b;
                }
                return null;
            }

            /// <summary>网络实体扫描+链接+StartSynchronising 是否全部完成
            /// （Server/Client 同步器已挂载、游戏箱盖图标已绘制）。
            /// 反射失败（API 缺失）按 true 处理，保持旧版编辑器兼容。</summary>
            public static bool IsSynchronisationActive()
            {
                if (IsSyncActiveMethod == null)
                    return true;
                try
                {
                    return (bool)IsSyncActiveMethod.Invoke(null, null);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            /// <summary>本机是否服务端：ConnectionStatus.IsHost() || !IsInSession()
            /// （主机或单机/离线）。反射失败按 true（单机场景为主）。</summary>
            public static bool IsServerMachine()
            {
                if (IsHostMethod == null || IsInSessionMethod == null)
                    return true;
                try
                {
                    return (bool)IsHostMethod.Invoke(null, null)
                        || !(bool)IsInSessionMethod.Invoke(null, null);
                }
                catch (Exception)
                {
                    return true;
                }
            }

            public static void RegisterSpawnable(GameObject spawner, GameObject prefab)
            {
                if (RegisterSpawnableMethod == null)
                    throw new InvalidOperationException("NetworkUtils.RegisterSpawnablePrefab 反射失败");
                RegisterSpawnableMethod.Invoke(null, new object[] { spawner, prefab });
            }

            public static void RegisterSpawnable(GameObject spawner, GameObject prefab, System.Delegate callback)
            {
                if (RegisterSpawnableCallbackMethod == null)
                    throw new InvalidOperationException("NetworkUtils.RegisterSpawnablePrefab(callback) 反射失败");
                RegisterSpawnableCallbackMethod.Invoke(null, new object[] { spawner, prefab, callback });
            }

            public static void SetItemPrefab(Component spawner, GameObject prefab)
            {
                if (spawner == null || ItemPrefabField == null)
                    return;
                ItemPrefabField.SetValue(spawner, prefab);
            }
        }
    }
}
