using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 完整 prefab（common04 custom_content 克隆，如 web_teacup_stack_clean）的
    /// CleanPlateStack 初始化器。
    ///
    /// 背景：这类 prefab 本身是完整游戏对象（SetupCustomUtensilStub + CleanPlateStack +
    /// Stack），没有 common01 包装 prefab 的 PseudoPrefabStub。旧写回仍按 wrapper 路径
    /// 加 PseudoPrefabCleanPlateStack；基类 PseudoPrefab.Start 去 LoadAsset(null) →
    /// NullReferenceException。本组件：
    ///  - 编辑态：按 CleanPlateStack.m_platePrefab 实例化盘/杯预览（HideFlags.DontSave）；
    ///  - 真机/Play：等同步激活后在本机为服务端逐个 AddToStack（客户端由同步复制）。
    /// 不再依赖 PseudoPrefab / PseudoPrefabSO 容器。
    /// </summary>
    [ExecuteInEditMode]
    public class CleanPlateStackSeeder : MonoBehaviour
    {
        public int plateCount = 5;
        public PseudoPrefabSO platePseudoPrefabSO;
        // 只给“关卡里摆好的”堆补杯：合并脏杯、洗杯完成时运行时生成的堆也会跑本组件，
        // 不区分的话每个新生成的堆都会被补到 plateCount 只（凭空多出杯子）。
        // 判据是物体被创建的时刻（Awake 时的关卡时间），不是补杯的时刻——
        // 这样联机同步激活慢也不会漏补。
        public float seedWindowSeconds = 2f;

        private readonly List<GameObject> m_previews = new List<GameObject>();
        private bool m_seedStarted;
        private float m_bornAt;
        // 原版 AutoDestructEmptyPlateStack：堆一空就在下一帧 NetworkUtils.DestroyObjectsRecursive
        // 把整堆删掉。关卡里摆好的堆在服务端补杯之前本来就是空的，必须先压住它，
        // 否则补杯协程还没跑到，堆已经被删了（= 开局杯堆消失）。
        private Component m_autoDestruct;
        // 关卡场景是**加塞**加载的（LoadingScreenFlow → AssetBundleManager.LoadLevel("loading")
        // 单场景，再 additive 载入关卡），Time.timeSinceLevelLoad 从 Loading 场景就开始计时，
        // 关卡本体 Awake 时通常已经 1~4s。原来的“timeSinceLevelLoad <= seedWindowSeconds”
        // 判据因此把**关卡摆放的杯堆**也当成运行时生成的，直接 return → 不补杯 → 空堆被
        // 原版 AutoDestructEmptyPlateStack 删除（= 开局杯堆消失）。
        // 改成“以本场景第一个 seeder 的 Awake 时刻为锚点”：运行时生成的堆（合并/洗净产物）
        // 在同一场景内晚得多，仍能区分；换关卡（场景句柄变化）自动重新锚定。
        private static UnityEngine.SceneManagement.Scene s_anchorScene;
        private static float s_anchorTime = -1f;

        private void Awake()
        {
            float now = Time.timeSinceLevelLoad;
            UnityEngine.SceneManagement.Scene scene = gameObject.scene;
            if (s_anchorTime < 0f || scene != s_anchorScene)
            {
                s_anchorScene = scene;
                s_anchorTime = now;
            }
            m_bornAt = now;
            m_autoDestruct = GetComponent("AutoDestructEmptyPlateStack");
        }

        /// <summary>压住/放行原版空堆自毁（AutoDestructEmptyPlateStack.enabled）。</summary>
        private void SuppressAutoDestruct(bool suppress)
        {
            if (m_autoDestruct == null)
                m_autoDestruct = GetComponent("AutoDestructEmptyPlateStack");
            Behaviour b = m_autoDestruct as Behaviour;
            if (b != null && b.enabled == suppress)
                b.enabled = !suppress;
        }

        /// <summary>本物体是否关卡加载时就在场景里（而不是运行中生成/合并出来的）。</summary>
        private bool IsPlacedInLevel()
        {
            return s_anchorTime < 0f || (m_bornAt - s_anchorTime) <= seedWindowSeconds;
        }

        /// <summary>运行时回收台/水槽烘干位生成的堆（父链含 PlateReturnStation）：
        /// 数量由 ReturnPlate/AddToStack 决定，绝不能再按 plateCount 补成 5 只。
        /// 尤其旧时间锚点逻辑在“关卡没摆任何堆、第一个堆由回收台生成”时会把
        /// 它误判为关卡摆放物。</summary>
        private bool IsUnderPlateReturnStation()
        {
            Transform t = transform.parent;
            while (t != null)
            {
                if (t.GetComponent("PlateReturnStation") != null)
                    return true;
                t = t.parent;
            }
            return false;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                DestroyPreviews();
                return;
            }
            BuildPreview();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (IsUnderPlateReturnStation())
                {
                    StubLog.Dbg("[CleanPlateStackSeeder] 跳过回收台/烘干位运行时堆补杯: " + name);
                    return;
                }
                if (!IsPlacedInLevel())
                    return;
                // 关卡摆放的堆：先压住空堆自毁，补到第一只杯子后再放行。
                SuppressAutoDestruct(true);
                StubLog.Log("[CleanPlateStackSeeder] " + name + " 识别为关卡摆放杯堆（bornAt="
                    + m_bornAt.ToString("F2") + "s, anchor=" + s_anchorTime.ToString("F2") + "s），开始补杯");
                // 编辑器 Play 同样跑完整本地回环联机链路（同步约 13s 才激活）：
                // 先铺本地可视预览（纯展示，已摘 tag，不会进 ServerStack.m_stackItems），
                // SeedLocalWhenNoSync 等到同步激活后改走服务端补杯（真实体）。
                // 真机走 SeedServerWhenReady（ServerPlateStackBase）。
                if (Application.isEditor)
                {
                    // 先立刻补一份本地可视盘堆：同步未激活时旧逻辑要等满 3s 才补，
                    // 进场瞬间看到的是空台面/空堆（“茶杯堆在 play 里没显示”观感来源）。
                    if (m_previews.Count == 0)
                        BuildPreview();
                    StartCoroutine(SeedLocalWhenNoSync());
                    return;
                }
                DestroyPreviews();
                if (!m_seedStarted)
                {
                    m_seedStarted = true;
                    StartCoroutine(SeedServerWhenReady());
                }
                return;
            }
            if (m_previews.Count == 0)
                BuildPreview();
        }

        /// <summary>编辑器 Play：等同步激活（本地回环约 13s 就绪）后改走服务端补杯；
        /// 20s 仍无同步（没跑联机链路的场景）才保留预览作纯可视化（预览已摘 tag，
        /// ServerStack 收不到它们，拾取链路碰不到，不会留失效引用）。</summary>
        private IEnumerator SeedLocalWhenNoSync()
        {
            float waited = 0f;
            while (waited < 20f)
            {
                if (GameApi.IsSynchronisationActive())
                {
                    // 有同步会话（编辑器里跑完整联机链路）：改走服务端补盘。
                    // 预览先留着——服务端补杯成功后再销毁，避免中途空一堆（观感“杯堆消失”）。
                    if (!m_seedStarted)
                    {
                        m_seedStarted = true;
                        StartCoroutine(SeedServerWhenReady());
                    }
                    yield break;
                }
                waited += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }
            // 20s 无同步：会话没跑联机链路，预览留作纯可视化（已摘 tag，仅展示用）
            if (m_previews.Count == 0)
                BuildPreview();
            StubLog.Log("[CleanPlateStackSeeder] " + name + " 等待 20s 同步未激活，编辑器 Play 保留本地可视化预览 "
                + m_previews.Count + " 个");
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            DestroyPreviews();
        }

        private void OnDestroy()
        {
            DestroyPreviews();
        }

        /// <summary>写回 SaveScene 前清掉预览（避免被 Unity 当作 prefab 修改保存）。</summary>
        public void ClearPreview()
        {
            DestroyPreviews();
        }

        /// <summary>编辑器写回设置 plateCount/SO 后强制刷新预览。</summary>
        public void Rebuild()
        {
            DestroyPreviews();
            if (!Application.isPlaying)
                BuildPreview();
        }

        private void BuildPreview()
        {
            if (m_previews.Count > 0)
                return;
            if (!HostManagerReady())
                return;
            GameObject platePrefab = ResolvePlatePrefab();
            if (platePrefab == null)
                return;
            Component stack = GetComponent("Stack");
            if (stack == null)
                return;
            BoxCollider box = GetComponent<BoxCollider>();
            int count = Mathf.Max(0, plateCount);
            List<GameObject> plates = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                GameObject plate = Instantiate(platePrefab, transform);
                plate.name = platePrefab.name + "_preview";
                // 预览只是可视化，绝不能被当成堆内容物：ServerStack.StartSynchronising
                // 按 tag == m_itemTag("Plate") 把直接子物体收进 m_stackItems。预览不是
                // 注册实体，实体启动后会被清理销毁，一旦留在 m_stackItems 里就是
                // 失效引用 → 拾取 MissingReferenceException。摘掉 tag 让 ServerStack
                // 永远看不到预览（GetSize 也只反映真实补杯数）。
                plate.tag = "Untagged";
                SetHideFlagsRecursive(plate, HideFlags.DontSave);
                Component grid = plate.GetComponent("EditorGridSnap");
                Behaviour gridBehaviour = grid as Behaviour;
                if (gridBehaviour != null)
                    gridBehaviour.enabled = false;
                Component container = plate.GetComponent("IngredientContainer");
                if (container != null)
                {
                    FieldInfo cap = container.GetType().GetField(
                        "m_capacity", BindingFlags.Public | BindingFlags.Instance);
                    if (cap != null)
                        cap.SetValue(container, 100);
                }
                plates.Add(plate);
                m_previews.Add(plate);
            }
            InvokeStack(stack, "RefreshStackTransforms", new object[] { plates });
            if (box != null)
                InvokeStack(stack, "RefreshStackCollider", new object[] { box, count });
        }

        private static bool HostManagerReady()
        {
            // 编辑态预览 Instantiate 盘子 prefab 时，其 SetupCustomPrefab.Awake 会访问
            // LevelEditor.PseudoPrefabManager.Instance；场景打开早期（OnEnable 先于
            // 管理器 Awake）或无管理器的场景里为 null → NRE。此处反射检查，未就绪
            // 则跳过（Start 会按 m_previews.Count == 0 重试）。
            Type t = Type.GetType("LevelEditor.PseudoPrefabManager, Assembly-CSharp");
            if (t == null)
                return true;
            FieldInfo f = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            if (f == null)
                return true;
            return f.GetValue(null) != null;
        }

        private GameObject ResolvePlatePrefab()
        {
            // 完整 prefab 的正确来源：PlateStackBase.m_platePrefab（干净堆
            // CleanPlateStack.m_platePrefab → web_teacup；脏堆 DirtyPlateStack.
            // m_platePrefab → web_teacup_dirty）。
            Component cleanStack = GetComponent("CleanPlateStack");
            if (cleanStack == null)
                cleanStack = GetComponent("DirtyPlateStack");
            if (cleanStack != null)
            {
                FieldInfo f = cleanStack.GetType().GetField(
                    "m_platePrefab", BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                {
                    GameObject prefab = f.GetValue(cleanStack) as GameObject;
                    if (prefab != null)
                        return prefab;
                }
            }
            if (platePseudoPrefabSO != null)
                return GameApi.LoadPseudoPrefabObject(platePseudoPrefabSO);
            return null;
        }

        private IEnumerator SeedServerWhenReady()
        {
            // 运行中生成的堆（合并产物/洗净产物）不补杯
            if (!IsPlacedInLevel())
                yield break;
            float waited = 0f;
            bool logged = false;
            while (waited <= 20f)
            {
                if (GameApi.IsServerMachine())
                {
                    Component server = FindServerSnapshot();
                    if (server != null)
                    {
                        int count = Mathf.Max(0, plateCount);
                        int size = InvokeInt(server, "GetSize");
                        if (size < count)
                        {
                            for (int i = size; i < count; i++)
                            {
                                if (!TryAddToStack(server))
                                    break;
                            }
                            int after = InvokeInt(server, "GetSize");
                            if (!logged)
                            {
                                logged = true;
                                StubLog.Log("[CleanPlateStackSeeder] " + name
                                    + " 服务端补杯 " + after + "/" + count);
                            }
                            size = after;
                        }
                        if (size > 0)
                        {
                            DestroyPreviews();
                            SuppressAutoDestruct(false);
                            yield break;
                        }
                    }
                }
                waited += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }
            // 20s 都没补上：编辑器 Play 回落本地可视化预览（保持可见，自毁继续压着）；
            // 真机放行原版自毁，避免永远留一只空堆。
            if (Application.isEditor)
            {
                if (m_previews.Count == 0)
                    BuildPreview();
                StubLog.Log("[CleanPlateStackSeeder] " + name + " 服务端补杯未生效，编辑器 Play 回落本地可视化 "
                    + m_previews.Count + " 个");
            }
            else
            {
                SuppressAutoDestruct(false);
            }
        }

        /// <summary>干净堆 ServerCleanPlateStack / 脏堆 ServerDirtyPlateStack
        /// （两者都是 ServerPlateStackBase，AddToStack 按 m_platePrefab 出杯）。</summary>
        private Component FindServerSnapshot()
        {
            Component server = GetComponent("ServerCleanPlateStack");
            if (server == null)
                server = GetComponent("ServerDirtyPlateStack");
            return server;
        }

        private static bool TryAddToStack(Component server)
        {
            try
            {
                MethodInfo add = server.GetType().GetMethod(
                    "AddToStack", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                if (add == null)
                    return false;
                add.Invoke(server, null);
                return true;
            }
            catch (Exception)
            {
                // 同步尚未完全建立（NetworkUtils.ServerSpawnPrefab 要求服务端会话）：
                // 交给外层 0.25s 轮询重试，不要停。
                return false;
            }
        }

        private static int InvokeInt(Component target, string methodName)
        {
            try
            {
                MethodInfo mi = target.GetType().GetMethod(
                    methodName, BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                if (mi == null)
                    return 0;
                object v = mi.Invoke(target, null);
                return (v is int) ? (int)v : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void InvokeStack(Component stack, string methodName, object[] args)
        {
            try
            {
                MethodInfo mi = stack.GetType().GetMethod(
                    methodName, BindingFlags.Public | BindingFlags.Instance);
                if (mi != null)
                    mi.Invoke(stack, args);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CleanPlateStackSeeder] " + methodName + " 失败: " + ex.Message);
            }
        }

        private void DestroyPreviews()
        {
            for (int i = 0; i < m_previews.Count; i++)
            {
                GameObject plate = m_previews[i];
                if (plate == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(plate);
                else
                    DestroyImmediate(plate);
            }
            m_previews.Clear();
        }

        private static void SetHideFlagsRecursive(GameObject go, HideFlags flags)
        {
            if (go == null)
                return;
            go.hideFlags = flags;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetHideFlagsRecursive(t.GetChild(i).gameObject, flags);
        }
    }
}
