using LevelEditorStub;
using System.Reflection;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// 可推动大火锅专用 PseudoPrefab（随游戏编译，写回时由 wrapper prefab 携带）。
    ///
    /// 背景：pushable_object.prefab 是纯逻辑载具（Rigidbody + 四向抓取点 + 碰撞，
    /// 无 Renderer/容器）；原版关卡里大锅作为独立道具放在载具上。宿主 PseudoPrefab
    /// 的 ClearChild 会销毁根下全部子物体，无法在 wrapper 里内嵌第二个 PseudoPrefab。
    ///
    /// 方案：重写 ResetChild，一次性实例化「载具 + 完整功能大锅」两份 bundle prefab：
    ///  - 载具（stub.pseudoPrefabSO → pushable_object）作为 childGameObject，
    ///    物理与玩家交互（拉住再推）由它独占；
    ///  - 大锅（m_potSO → utensil_large_pot_01，带 IngredientContainer/CookingHandler，
    ///    可丢食材、可在灶上加热）挂在根下，剥掉与载具冲突的组件
    ///    （Rigidbody/Collider/Interactable/AttachStation/EditorGridSnap）。
    /// 全部资源来自 bundle（与宿主同路径），无每帧动态查找。
    /// </summary>
    public class LayoutRuntimePushablePot : PseudoPrefab
    {
        /// <summary>大锅 SO（common03 静态大锅，bundle226）。wrapper prefab 内序列化引用。</summary>
        public PseudoPrefabSO m_potSO;

        /// <summary>web「锅具管理」填充的额外食材（运行时通过 bundle 路径加载节点；
        ///  与 m_allowedIngredientPaths 一一对应）。空 = 锅的默认许可表（DLC4 原版）。</summary>
        public string[] m_allowedIngredientBundles = new string[0];
        public string[] m_allowedIngredientPaths = new string[0];

        /// <summary>按 m_allowedIngredient* 重建锅 child 的 CookableContainer
        ///  m_approvedContentsList（仿宿主 CookingUtensil.Setup：保留原 lookup 的
        ///  prefab 映射，默认 prefab 取原表首项）。空配置时保持原版许可表。</summary>
        private void ApplyAllowedIngredients(GameObject pot)
        {
            if (m_allowedIngredientBundles == null || m_allowedIngredientBundles.Length == 0)
                return;
            var container = pot.GetComponent<CookableContainer>();
            if (container == null || container.m_approvedContentsList == null)
                return;
            var oldLookup = container.m_approvedContentsList;
            var oldArray = (OrderToPrefabLookup.ContentPrefabLookup[])typeof(OrderToPrefabLookup)
                .GetField("m_lookupArray", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(oldLookup);
            var defaultPrefab = oldArray != null && oldArray.Length > 0 ? oldArray[0].m_prefab : null;
            var entries = new System.Collections.Generic.List<OrderToPrefabLookup.ContentPrefabLookup>();
            for (int i = 0; i < m_allowedIngredientBundles.Length; i++)
            {
                var so = ScriptableObject.CreateInstance<PseudoPrefabSO>();
                so.bundleName = m_allowedIngredientBundles[i];
                so.assetPath = i < m_allowedIngredientPaths.Length ? m_allowedIngredientPaths[i] : null;
                OrderDefinitionNode node = null;
                try
                {
                    node = PseudoPrefabManager.LoadAsset<OrderDefinitionNode>(so);
                }
                catch
                {
                }
                if (node == null)
                    continue;
                GameObject prefab = defaultPrefab;
                if (oldArray != null)
                {
                    foreach (var e in oldArray)
                    {
                        if (e.m_content == node)
                        {
                            prefab = e.m_prefab;
                            break;
                        }
                    }
                }
                entries.Add(new OrderToPrefabLookup.ContentPrefabLookup
                {
                    m_content = node,
                    m_prefab = prefab,
                });
            }
            if (entries.Count == 0)
                return;
            var newLookup = ScriptableObject.CreateInstance<OrderToPrefabLookup>();
            newLookup.name = "Lookup_" + name;
            newLookup.GetType()
                .GetField("m_lookupArray", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(newLookup, entries.ToArray());
            container.m_approvedContentsList = newLookup;
        }

        public override void ResetChild()
        {
            if (stub == null)
                stub = GetComponent<PseudoPrefabStub>();

            // 先加载两份 prefab（LoadAsset 可能重入 ResetChild，遵循宿主「先加载后清场」次序）
            GameObject potPrefab = null;
            if (m_potSO != null)
                potPrefab = PseudoPrefabManager.LoadAsset(m_potSO);
            GameObject carrierPrefab = PseudoPrefabManager.LoadAsset(stub.pseudoPrefabSO);

            ClearChild();

            // 载具：childGameObject（保持宿主命名约定，供运行时识别）。零偏移——
            // 火锅类不需要 0.6 中心偏移修正，碰撞中心即载具原点。
            childGameObject = Instantiate(carrierPrefab, transform.position, transform.rotation, transform);
            childGameObject.name = stub.pseudoPrefabSO.prefabName;

            // 大锅：挂在载具下（随载具一起被拖动），局部零偏移——锅模型中心同样在自身
            // 局部 (-0.6, ·, +0.6)，载具反偏后两者中心都落在 wrapper 原点。
            if (potPrefab != null)
            {
                var pot = Instantiate(potPrefab, childGameObject.transform, false);
                pot.name = potPrefab.name;
                pot.transform.localPosition = Vector3.zero;
                pot.transform.localRotation = Quaternion.identity;
                // 剥冲突组件：Rigidbody（物理由载具独占）、Interactable/AttachStation/
                // EditorGridSnap（交互走载具）。Collider 保留——锅的食材投放区
                // （IngredientContainer trigger zone）靠它接收食材，且都是 trigger
                // （不参与行走碰撞，与载具 MeshCollider 无冲突）。
                var destroy = Application.isPlaying
                    ? (System.Action<Object>)Object.Destroy
                    : Object.DestroyImmediate;
                foreach (var rb in pot.GetComponentsInChildren<Rigidbody>(true))
                    destroy(rb);
                var interactable = pot.GetComponent<Interactable>();
                if (interactable != null) destroy(interactable);
                var snap = pot.GetComponent<EditorGridSnap>();
                if (snap != null) destroy(snap);
                var attach = pot.GetComponent<AttachStation>();
                if (attach != null) destroy(attach);

                // 额外食材配置（web「锅具管理」填充）：写回时由 ApplyStub 序列化到
                //  m_allowedIngredientBundles/m_allowedIngredientPaths；此处重建锅的
                //  CookableContainer.m_approvedContentsList（仿宿主 CookingUtensil.Setup）。
                ApplyAllowedIngredients(pot);
            }
        }
    }
}
