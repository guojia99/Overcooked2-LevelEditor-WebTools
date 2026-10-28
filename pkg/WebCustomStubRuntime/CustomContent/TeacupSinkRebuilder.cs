using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 茶杯水槽重建器：挂在 web_sink_teacup（薄 wrapper，PseudoPrefab → 原版水槽 bundle）根上。
    /// 原版水槽从游戏 bundle 实例化之后执行「茶杯化」：
    ///   ① 烘干位 PlateReturnStation.m_stackPrefab → 净茶杯堆模板（茶杯回杯/干净杯供应）；
    ///   ② WashingStation.m_dirtyPlates 槽位里的脏盘子模型 → 脏茶杯视觉克隆
    ///     （剥掉脚本/物理/碰撞，只留网格渲染；槽位 transform 与按数量显隐逻辑
    ///     （ClientWashingStation.UpdateCosmetics）完全沿用原版）；
    ///   ③ 水体/艺术体全部保留原版自带（水面在槽内、3D 质感、材质与换肤随皮肤 SO）。
    /// Play 模式与编辑器编辑模式（摆放预览）都会执行；编辑模式下槽位茶杯保持隐藏
    /// （对齐原版 ClientWashingStation 初始隐藏行为）。
    /// 序列化字段直引 prefab，与 DirtyTeacupMergeWatcher.m_stackPrefab 同款约定。
    /// </summary>
    [ExecuteInEditMode]
    public class TeacupSinkRebuilder : MonoBehaviour
    {
        /// <summary>净茶杯堆模板（web_teacup_stack_clean）。</summary>
        public GameObject m_cleanStackPrefab;

        /// <summary>脏茶杯视觉模板（web_teacup_dirty）。</summary>
        public GameObject m_teacupVisualPrefab;

        [Header("脏杯摆放（编辑模式改数值即时重排，可直接在场景里调）")]
        /// <summary>相邻茶杯间距（米）；三个杯子按此间距沿槽位主方向排开。&lt;=0 时退回原版槽位。</summary>
        public float m_slotPitch = 0.34f;

        /// <summary>茶杯模型缩放倍数（乘在原槽位缩放 0.70 上）。</summary>
        public float m_cupScale = 0.8f;

        /// <summary>茶杯相对槽位平面的高度偏移（米，正值上浮）。</summary>
        public float m_cupLift = 0.02f;

        /// <summary>
        /// 整行脏杯相对原版槽位质心的平移（水槽根局部 x/z，米）。
        /// 本地 +x 指向盆内（远离沥水架/水龙头那一侧），用来把杯子贴到水槽里侧边缘而不是浮在盆中间。
        /// </summary>
        public Vector2 m_rowShift = new Vector2(0.18f, 0f);

        private const string VanillaSinkNamePrefix = "workstation_sink_01";
        private const float WaitSeconds = 10f;

        /// <summary>已完成茶杯化的水槽子体（伪预制体热重载后引用变化会触发重建）。</summary>
        private Transform m_sinkChild;

        private void OnEnable()
        {
            m_sinkChild = null;
        }

        /// <summary>Inspector 里调间距/缩放后清完成标记，编辑模式 Update 轮询立即按新参数重排。</summary>
        private void OnValidate()
        {
            m_sinkChild = null;
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(RebuildLoop());
            }
            else
            {
                TryRebuild();
            }
        }

        /// <summary>编辑模式没有协程，靠 Update（场景变更时触发）轮询子体生成。</summary>
        private void Update()
        {
            if (Application.isPlaying)
                return;
            TryRebuild();
        }

        private IEnumerator RebuildLoop()
        {
            float deadline = Time.time + WaitSeconds;
            while (Time.time < deadline)
            {
                if (TryRebuild())
                    yield break;
                yield return null;
            }
            StubLog.LogWarn("[TeacupSinkRebuilder] 等待原版水槽子体超时，茶杯化未执行");
        }

        /// <summary>子体已生成且未茶杯化时执行；返回是否已处于完成状态。</summary>
        private bool TryRebuild()
        {
            Transform sink = FindSinkChild();
            if (sink == null)
                return false;
            if (sink == m_sinkChild)
                return true;

            int slots = SwapDirtyPlates(sink);
            bool stackOk = SwapDryingStack(sink);
            m_sinkChild = sink;
            StubLog.Log("[TeacupSinkRebuilder] 茶杯水槽茶杯化完成：脏杯槽位 " + slots +
                " 个，烘干杯堆模板" + (stackOk ? "已接线" : "未接线"));
            return true;
        }

        private Transform FindSinkChild()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith(VanillaSinkNamePrefix, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        /// <summary>烘干位堆模板换成净茶杯堆（服务端镜像启动前完成即可）。</summary>
        private bool SwapDryingStack(Transform sinkRoot)
        {
            Component drying = FindComponentByTypeName(sinkRoot, "PlateReturnStation");
            if (drying == null || m_cleanStackPrefab == null)
                return false;
            FieldInfo field = FindField(drying.GetType(), "m_stackPrefab");
            if (field == null)
                return false;
            field.SetValue(drying, m_cleanStackPrefab);
            return true;
        }

        /// <summary>把 WashingStation.m_dirtyPlates 槽位模型换成茶杯视觉克隆。</summary>
        private int SwapDirtyPlates(Transform sinkRoot)
        {
            Component washing = FindComponentByTypeName(sinkRoot, "WashingStation");
            if (washing == null || m_teacupVisualPrefab == null)
                return 0;
            FieldInfo field = FindField(washing.GetType(), "m_dirtyPlates");
            GameObject[] slots = field == null ? null : field.GetValue(washing) as GameObject[];
            if (slots == null || slots.Length == 0)
                return 0;

            // 第一遍：记录原版槽位 TRS（原版把脏盘子叠在盆里，三个槽位挤在 0.2~0.3m 内，
            // 换成茶杯后会互相穿插，所以先按槽位自己标出的位置重排成一行）。
            int count = slots.Length;
            Transform[] parents = new Transform[count];
            Vector3[] positions = new Vector3[count];
            Quaternion[] rotations = new Quaternion[count];
            Vector3[] scales = new Vector3[count];
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                GameObject slot = slots[i];
                if (slot == null)
                    continue;
                parents[i] = slot.transform.parent;
                positions[i] = slot.transform.localPosition;
                rotations[i] = slot.transform.localRotation;
                scales[i] = slot.transform.localScale;
                names[i] = slot.name;
            }

            float pitch = m_slotPitch > 0f ? m_slotPitch : 0f;
            if (pitch > 0f)
                SpreadSlots(sinkRoot, parents, positions, pitch, m_cupLift, m_rowShift);
            else
                LiftOnly(positions, m_cupLift);

            // 第二遍：克隆茶杯视觉体（只留网格渲染），按槽位父体/朝向摆放。
            GameObject[] cups = new GameObject[count];
            string sizeLog = "";
            for (int i = 0; i < count; i++)
            {
                if (slots[i] == null)
                    continue;

                GameObject cup = MakeVisualCopy(names[i]);
                cup.transform.SetParent(parents[i], false);
                cup.transform.localPosition = positions[i];
                cup.transform.localRotation = rotations[i];
                cup.transform.localScale = scales[i] * Mathf.Max(0.05f, m_cupScale);
                cup.SetActive(false);
                cups[i] = cup;

                // 槽位几何诊断：若某槽位 scale/mesh 异常，几个杯子会重叠成一个。
                sizeLog += " [" + i + "]" + names[i] + " pos=" + positions[i].ToString("0.00")
                    + " scale=" + cup.transform.localScale.ToString("0.00");
                MeshFilter mf = cup.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 b = mf.sharedMesh.bounds.size;
                    sizeLog += " mesh=" + b.ToString("0.00");
                }

                SafeDestroy(slots[i]);
            }
            field.SetValue(washing, cups);
            StubLog.Log("[TeacupSinkRebuilder] 脏杯槽位几何（间距=" + pitch.ToString("0.00") +
                " 缩放=" + m_cupScale.ToString("0.00") + "）:" + sizeLog);
            return cups.Length;
        }

        /// <summary>
        /// 同一父体下的槽位按「质心 + 主方向」重排成等距一行：
        /// 主方向取组内最远两点连线（投影到水平面），高度取组内最高槽位再上浮；
        /// 最后整行按 rowShift（水槽根局部 x/z）平移到水槽里侧边缘。
        /// </summary>
        private static void SpreadSlots(Transform sinkRoot, Transform[] parents, Vector3[] positions,
            float pitch, float lift, Vector2 rowShift)
        {
            bool[] handled = new bool[positions.Length];
            List<int> group = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (handled[i] || parents[i] == null)
                    continue;

                group.Clear();
                for (int j = 0; j < positions.Length; j++)
                {
                    if (parents[j] != parents[i])
                        continue;
                    handled[j] = true;
                    group.Add(j);
                }
                if (group.Count < 2)
                {
                    positions[group[0]] += Vector3.up * lift;
                    continue;
                }

                Vector3 center = Vector3.zero;
                for (int k = 0; k < group.Count; k++)
                    center += positions[group[k]];
                center /= group.Count;

                Vector3 dir = Vector3.right;
                float widest = -1f;
                for (int a = 0; a < group.Count; a++)
                {
                    for (int b = a + 1; b < group.Count; b++)
                    {
                        Vector3 d = positions[group[b]] - positions[group[a]];
                        d.y = 0f;
                        if (d.sqrMagnitude > widest)
                        {
                            widest = d.sqrMagnitude;
                            if (d.sqrMagnitude > 1e-6f)
                                dir = d.normalized;
                        }
                    }
                }

                group.Sort(delegate (int l, int r)
                {
                    return Vector3.Dot(positions[l] - center, dir)
                        .CompareTo(Vector3.Dot(positions[r] - center, dir));
                });

                float top = float.MinValue;
                for (int k = 0; k < group.Count; k++)
                    top = Mathf.Max(top, positions[group[k]].y);

                // 整行平移：方向先在水槽根空间取，再换算到槽位父体的局部空间。
                Vector3 shift = Vector3.zero;
                if (rowShift.sqrMagnitude > 1e-6f && sinkRoot != null)
                {
                    Vector3 worldDir = sinkRoot.TransformDirection(new Vector3(rowShift.x, 0f, rowShift.y));
                    shift = parents[group[0]].InverseTransformDirection(worldDir);
                }

                // 有平移时把「第 1 只杯」钉在靠边的那一头，后面的杯子朝中间延伸
                // （而不是整行居中：那样只放 1 只时又回到了盆中央）。
                if (shift.sqrMagnitude > 1e-6f && Vector3.Dot(dir, shift) < 0f)
                    dir = -dir;

                float half = (group.Count - 1) * 0.5f;
                for (int k = 0; k < group.Count; k++)
                {
                    Vector3 p = center + shift + dir * (half - k) * pitch;
                    p.y = top + lift;
                    positions[group[k]] = p;
                }
            }
        }

        private static void LiftOnly(Vector3[] positions, float lift)
        {
            for (int i = 0; i < positions.Length; i++)
                positions[i] += Vector3.up * lift;
        }

        /// <summary>
        /// 只克隆模板的渲染层级（Transform/MeshFilter/MeshRenderer 与共享 mesh/材质），
        /// 不实例化脚本/物理组件，也无需销毁组件（避开 RequireComponent 依赖链）。
        /// 根节点 TRS 由槽位覆盖（调用方设置）。
        /// </summary>
        private GameObject MakeVisualCopy(string slotName)
        {
            GameObject cup = new GameObject(slotName);
            CloneVisualHierarchy(m_teacupVisualPrefab.transform, cup.transform);
            return cup;
        }

        private static void CloneVisualHierarchy(Transform src, Transform dst)
        {
            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;

            MeshFilter meshFilter = src.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = src.GetComponent<MeshRenderer>();
            if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null)
            {
                MeshFilter newFilter = dst.gameObject.AddComponent<MeshFilter>();
                newFilter.sharedMesh = meshFilter.sharedMesh;
                MeshRenderer newRenderer = dst.gameObject.AddComponent<MeshRenderer>();
                newRenderer.sharedMaterials = meshRenderer.sharedMaterials;
            }

            foreach (Transform child in src)
            {
                GameObject childCopy = new GameObject(child.name);
                childCopy.transform.SetParent(dst, false);
                CloneVisualHierarchy(child, childCopy.transform);
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private static Component FindComponentByTypeName(Transform root, string typeName)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().Name == typeName)
                    return c;
            }
            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            return null;
        }
    }
}
