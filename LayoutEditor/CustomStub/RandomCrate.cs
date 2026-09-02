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
    /// 1. 等 PseudoPrefab.childGameObject（宿主 Init 实例化真实箱子）；
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

        private IEnumerator Start()
        {
            // 外层循环：宿主 ResetChild 重建 childGameObject 后（重开关卡等场景）
            // 重新走完整装配。pseudo 本体被删则彻底退出。
            while (true)
            {
                var pseudo = GetComponent(GameApi.PseudoPrefabType);
                if (pseudo == null)
                {
                    Debug.LogWarning("[RandomCrate] 宿主缺少 PseudoPrefab，组件退出: " + name);
                    yield break;
                }

                GameObject child = GameApi.GetChildGameObject(pseudo);
                var deadline = Time.realtimeSinceStartup + 20f;
                while (child == null)
                {
                    if (pseudo == null)
                        yield break;
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        Debug.LogWarning("[RandomCrate] 等待真实箱子 prefab 超时（20s），本轮跳过: " + name);
                        child = null;
                        break;
                    }
                    yield return null;
                    child = GameApi.GetChildGameObject(pseudo);
                }
                if (child == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // 载入全部食材 prefab：bundle 直读（镜像 LayoutEditorItemSwitcherPatch
                // 的 LoadBundleAsset，避开 PseudoPrefabManager.LoadAsset 对 null 结果
                // 触发的 DeInit/Init 全量重置连锁）。
                // 同 assetPath 的重复条目合并（配额相加），保证名字匹配无歧义。
                _prefabs = new List<GameObject>();
                _initial = new List<float>();
                var assetPaths = new List<string>();
                for (int i = 0; i < m_itemSOs.Length; i++)
                {
                    var so = m_itemSOs[i];
                    if (so == null || string.IsNullOrEmpty(so.bundleName) || string.IsNullOrEmpty(so.assetPath))
                    {
                        Debug.LogWarning("[RandomCrate] 跳过无效食材配置项 #" + i + ": " + name);
                        continue;
                    }
                    float w = m_weights != null && i < m_weights.Length && m_weights[i] >= 1f ? m_weights[i] : 5f;
                    try
                    {
                        var bundle = GameApi.GetAssetBundle(so.bundleName);
                        if (bundle == null)
                        {
                            Debug.LogWarning("[RandomCrate] bundle 未加载，跳过食材 " + so.prefabName + ": " + name);
                            continue;
                        }
                        var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
                        if (prefab == null)
                        {
                            Debug.LogWarning("[RandomCrate] 食材 prefab 加载失败 " + so.assetPath + ": " + name);
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
                        Debug.LogWarning("[RandomCrate] 食材加载异常（bundle 缺失?）" + so.bundleName + "/" + so.assetPath
                            + ": " + ex.Message);
                    }
                }
                if (_prefabs.Count == 0)
                {
                    Debug.LogWarning("[RandomCrate] 无可用食材，本轮跳过: " + name);
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                _remaining = new float[_prefabs.Count];
                for (int i = 0; i < _initial.Count; i++)
                    _remaining[i] = _initial[i];

                _spawner = child.GetComponent(GameApi.PickupItemSpawnerType);
                if (_spawner == null)
                {
                    Debug.LogWarning("[RandomCrate] 真实箱子缺少 PickupItemSpawner，本轮跳过: " + name);
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // 预画问号：childGameObject 就绪即显示，不等网络握手（开局不再先显示
                // 首食材图标）。同步握手时游戏 ClientItemCrateCosmeticDecisions 会重绘
                // 首食材图标，随后下面的权威重画再覆盖回问号。
                PaintQuestionMark(child, m_questionMarkTexture);

                // 等网络同步完成（见类头注释第 2 点）。同步是可玩局面的必要环节，
                // 不设超时；child/pseudo 失效则回外层重装配。
                while (!GameApi.IsSynchronisationActive())
                {
                    if (pseudo == null)
                        yield break;
                    if (child == null)
                        break;
                    yield return null;
                    child = GameApi.GetChildGameObject(pseudo);
                }
                if (pseudo == null)
                    yield break;
                if (child == null)
                {
                    yield return null;
                    continue;
                }

                // 服务端判定（游戏标准写法：主机 或 未在联机会话=单机）
                _isServer = GameApi.IsServerMachine();

                // 注册全部候选（服务端与客户端都要——客户端网络实例化按 spawner 的
                // 注册表解析 spawnableID；静态查表，同步后注册依然有效）。
                // 服务端注册带回调：取出事件（ServerSpawnPrefab 内同步触发）驱动配额递减。
                var callback = _isServer ? CreateSpawnCallback() : null;
                for (int i = 0; i < _prefabs.Count; i++)
                {
                    try
                    {
                        if (callback != null)
                            GameApi.RegisterSpawnable(child, _prefabs[i], callback);
                        else
                            GameApi.RegisterSpawnable(child, _prefabs[i]);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[RandomCrate] 注册生成 prefab 失败 " + _prefabs[i].name + ": " + ex.Message);
                    }
                }

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
                    Debug.LogWarning("[RandomCrate] 取出物品无法匹配候选列表: " + spawned.name);
                    return;
                }
                _remaining[index] -= 1f;
                if (_remaining[index] <= 0f)
                    _remaining[index] = _initial[index];
                RollNext();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RandomCrate] 取出回调异常: " + ex.Message);
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
                if (lid != null)
                {
                    renderer = lid.GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null)
                        renderer = lid.GetComponent<MeshRenderer>();
                }
                if (renderer == null)
                    renderer = child.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogWarning("[RandomCrate] 找不到箱盖渲染器: " + child.name);
                    return;
                }
                var matNumber = GameApi.MaterialNumberField != null ? (int)GameApi.MaterialNumberField.GetValue(decisions) : 0;
                var uvScale = GameApi.UvScaleField != null ? (Vector2)GameApi.UvScaleField.GetValue(decisions) : Vector2.one;
                var materials = renderer.sharedMaterials;
                if (materials == null || matNumber < 0 || matNumber >= materials.Length || materials[matNumber] == null)
                {
                    Debug.LogWarning("[RandomCrate] 箱盖材质数组无效: " + child.name);
                    return;
                }
                var material = new Material(materials[matNumber]);
                material.mainTexture = texture;
                // 问号贴图需左右翻转（盖子 UV 采样方向与独立贴图相反）：U 轴取负并平移一个周期
                material.mainTextureOffset = new Vector2(uvScale.x, 1f / uvScale.y);
                material.mainTextureScale = new Vector2(-uvScale.x, -uvScale.y);
                materials[matNumber] = material;
                renderer.sharedMaterials = materials;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RandomCrate] 绘制问号图标失败: " + ex.Message);
            }
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

        /// <summary>Assembly-CSharp（宿主程序集）类型的反射缓存。类型缺失时安全返回 null。</summary>
        private static class GameApi
        {
            public static readonly Type PseudoPrefabType = Find("LevelEditor.PseudoPrefab");
            public static readonly FieldInfo ChildGameObjectField = Field(PseudoPrefabType, "childGameObject");

            public static readonly Type PseudoPrefabManagerType = Find("LevelEditor.PseudoPrefabManager");
            public static readonly MethodInfo GetAssetBundleMethod = Method(
                PseudoPrefabManagerType, "GetAssetBundle", new[] { typeof(string) });

            public static readonly Type VoidGenericType = Find("VoidGeneric`1");
            public static readonly Type VoidGenericGameObjectType = VoidGenericType != null
                ? VoidGenericType.MakeGenericType(typeof(GameObject))
                : null;

            public static readonly Type NetworkUtilsType = Find("NetworkUtils");
            public static readonly MethodInfo RegisterSpawnableMethod = NetworkUtilsType != null
                ? NetworkUtilsType.GetMethod("RegisterSpawnablePrefab", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject), typeof(GameObject) }, null)
                : null;
            public static readonly MethodInfo RegisterSpawnableCallbackMethod = NetworkUtilsType != null
                ? NetworkUtilsType.GetMethod("RegisterSpawnablePrefab", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject), typeof(GameObject), VoidGenericGameObjectType }, null)
                : null;

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

            public static GameObject GetChildGameObject(Component pseudo)
            {
                if (pseudo == null || ChildGameObjectField == null)
                    return null;
                return ChildGameObjectField.GetValue(pseudo) as GameObject;
            }

            public static AssetBundle GetAssetBundle(string bundleName)
            {
                if (GetAssetBundleMethod == null)
                    return null;
                return GetAssetBundleMethod.Invoke(null, new object[] { bundleName }) as AssetBundle;
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
