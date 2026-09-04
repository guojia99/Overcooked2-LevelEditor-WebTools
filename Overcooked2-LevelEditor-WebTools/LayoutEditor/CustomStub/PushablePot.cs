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
        /// 资产路径；与 m_extraIngredientPaths 一一对应）。空 = 默认许可表（DLC4 原版）。</summary>
        public string[] m_extraIngredientBundles = new string[0];
        public string[] m_extraIngredientPaths = new string[0];

        /// <summary>tag 载体前缀。</summary>
        public const string TagPrefix = "PushablePot|";

        private static bool s_loggedSelfCheck;

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
                if (carrier.GetComponent<PushableVoidFallTarget>() != null)
                {
                    // 已装配（含同步启动后的防线）——不重建，只监视载具失效
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                var potPrefab = LoadPotPrefab();
                if (potPrefab == null)
                {
                    StubLog.LogWarn("[PushablePot] 大锅 prefab 暂不可用（bundle 未加载?），稍后重试: " + name);
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                Assemble(carrier, potPrefab);
                yield return new WaitForSeconds(0.5f);
            }
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
                return null;
            try
            {
                var bundle = GameApi.GetAssetBundle(bundleName);
                if (bundle == null)
                    return null;
                return bundle.LoadAsset<GameObject>(assetPath);
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 大锅加载异常 " + bundleName + "/" + assetPath + ": " + ex.Message);
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

                // 空洞/水面坠落检测 marker（PushableVoidFall 只处理带此标记的载具）
                if (carrier.GetComponent<PushableVoidFallTarget>() == null)
                    carrier.AddComponent<PushableVoidFallTarget>();

                StubLog.Log("[PushablePot] 装配完成: " + name + " → 载具 " + carrier.name
                    + " + 大锅 " + pot.name + "（额外食材 " + (m_extraIngredientBundles != null ? m_extraIngredientBundles.Length : 0) + "）");
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
        /// （仿宿主 CookingUtensil.Setup：保留原 lookup 的 prefab 映射，默认 prefab
        /// 取原表首项）。空配置时保持原版许可表。节点从 bundle 直读。</summary>
        private void ApplyAllowedIngredients(GameObject pot)
        {
            if (m_extraIngredientBundles == null || m_extraIngredientBundles.Length == 0)
                return;
            if (GameApi.CookableContainerType == null || GameApi.ApprovedContentsField == null
                || GameApi.OrderToPrefabLookupType == null || GameApi.ContentPrefabLookupType == null
                || GameApi.LookupArrayField == null)
                return;
            try
            {
                var container = GameApi.GetComponentInChildren(pot, GameApi.CookableContainerType);
                if (container == null)
                    return;
                var oldLookup = GameApi.ApprovedContentsField.GetValue(container);
                if (oldLookup == null)
                    return;
                var oldArray = GameApi.LookupArrayField.GetValue(oldLookup) as System.Array;
                GameObject defaultPrefab = null;
                if (oldArray != null && oldArray.Length > 0 && GameApi.LookupPrefabField != null)
                    defaultPrefab = GameApi.LookupPrefabField.GetValue(oldArray.GetValue(0)) as GameObject;

                var entries = new List<object>();
                for (int i = 0; i < m_extraIngredientBundles.Length; i++)
                {
                    var node = LoadNode(m_extraIngredientBundles[i],
                        i < m_extraIngredientPaths.Length ? m_extraIngredientPaths[i] : null);
                    if (node == null)
                        continue;
                    GameObject prefab = defaultPrefab;
                    if (oldArray != null && GameApi.LookupContentField != null && GameApi.LookupPrefabField != null)
                    {
                        for (int e = 0; e < oldArray.Length; e++)
                        {
                            var entry = oldArray.GetValue(e);
                            var content = GameApi.LookupContentField.GetValue(entry);
                            if (content != null && (UnityEngine.Object)content == (UnityEngine.Object)node)
                            {
                                prefab = GameApi.LookupPrefabField.GetValue(entry) as GameObject;
                                break;
                            }
                        }
                    }
                    var inst = System.Activator.CreateInstance(GameApi.ContentPrefabLookupType);
                    GameApi.LookupContentField.SetValue(inst, node);
                    GameApi.LookupPrefabField.SetValue(inst, prefab);
                    entries.Add(inst);
                }
                if (entries.Count == 0)
                    return;

                var newLookup = ScriptableObject.CreateInstance(GameApi.OrderToPrefabLookupType);
                var newArray = System.Array.CreateInstance(GameApi.ContentPrefabLookupType, entries.Count);
                for (int i = 0; i < entries.Count; i++)
                    newArray.SetValue(entries[i], i);
                GameApi.LookupArrayField.SetValue(newLookup, newArray);
                GameApi.ApprovedContentsField.SetValue(container, newLookup);
                StubLog.Log("[PushablePot] 食材许可表重建: " + name + "（" + entries.Count + " 项）");
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[PushablePot] 食材许可表重建失败: " + name + " " + ex.Message);
            }
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
                    return null;
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
