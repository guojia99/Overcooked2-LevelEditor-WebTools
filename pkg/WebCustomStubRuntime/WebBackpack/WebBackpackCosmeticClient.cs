using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 背上背包后显示【锅具同款】内容宫格：实例化 bundle47 的原版
    /// IngredientsContentsUI prefab（IngredientContentsUIContainer 自带
    /// 网格布局/食材图标/空位精灵），驱动 SetMinimumSize(容量) +
    /// SetOrder(内容聚合节点)，并隐藏原版单物品 Hover。宫格为 vanilla
    /// 渲染，图标直接取节点的 m_iconSprite（无 lookup 失败路径）。
    /// </summary>
    public class WebBackpackCosmeticClient : MonoBehaviour
    {
        /// <summary>m_contentsUiSO 缺失时的硬兜底（bundle47 原版路径）。</summary>
        private const string FallbackUiBundle = "bundle47";
        private const string FallbackUiPath =
            "assets/prefabs/overcooked_legacy/gui/ingredientscontentsui.prefab";

        private WebBackpackStorage _storage;
        private object _clientIngredient;
        private GameObject _vanillaContentsHoverIcon;
        private GameObject _uiInstance;
        private Component _uiContainer;
        private bool _wasWorn;
        private string _lastSignature = "";
        private bool _uiFailed;

        public static void EnsureOn(GameObject backpackGo, WebBackpackStorage storage)
        {
            if (backpackGo == null || storage == null)
                return;
            var existing = backpackGo.GetComponent<WebBackpackCosmeticClient>();
            if (existing != null)
            {
                existing._storage = storage;
                return;
            }
            var c = backpackGo.AddComponent<WebBackpackCosmeticClient>();
            c._storage = storage;
        }

        private void Start()
        {
            EnsureClientIngredient();
            CacheVanillaHoverIcon();
        }

        private void EnsureClientIngredient()
        {
            if (_clientIngredient != null)
                return;
            var cicType = GameApi.ClientIngredientContainerType;
            if (cicType != null)
                _clientIngredient = GetComponent(cicType);
        }

        private void CacheVanillaHoverIcon()
        {
            if (_vanillaContentsHoverIcon != null)
                return;
            var syncType = GameApi.Find("ClientBackpackCosmeticDecisions");
            if (syncType == null)
                return;
            var sync = GetComponent(syncType) ?? GetComponentInChildren(syncType, true);
            if (sync == null)
                return;
            var field = syncType.GetField("m_contentsHoverIcon",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return;
            _vanillaContentsHoverIcon = field.GetValue(sync) as GameObject;
        }

        private void OnDestroy()
        {
            TeardownUi();
        }

        private void LateUpdate()
        {
            if (_storage == null)
                _storage = GetComponentInParent<WebBackpackStorage>();
            if (_storage == null)
                return;
            bool worn;
            try
            {
                worn = _storage.IsWorn();
            }
            catch (Exception)
            {
                return;
            }
            if (worn != _wasWorn)
            {
                _wasWorn = worn;
                StubLog.Log("[WebBackpack] 宫格: worn=" + worn + "（" + name + "）");
                if (worn)
                {
                    if (_vanillaContentsHoverIcon != null)
                        _vanillaContentsHoverIcon.SetActive(false);
                    EnsureUiBuilt();
                    if (_uiInstance != null)
                        RefreshOrder(true);
                }
                else
                {
                    TeardownUi();
                    _lastSignature = "";
                }
            }
            else if (worn)
            {
                if (_vanillaContentsHoverIcon != null && _vanillaContentsHoverIcon.activeSelf)
                    _vanillaContentsHoverIcon.SetActive(false);
                if (_uiInstance != null)
                    RefreshOrder(false);
            }
        }

        // -------------------------------------------------------------
        // UI 实例生命周期
        // -------------------------------------------------------------

        private void EnsureUiBuilt()
        {
            if (_uiInstance != null || _uiFailed)
                return;
            try
            {
                var prefab = LoadContentsUiPrefab();
                if (prefab == null)
                {
                    UiFailed("prefab 加载失败（m_contentsUiSO 与 bundle47 兜底均为空）");
                    return;
                }
                var follow = ResolveFollowTarget();
                if (follow == null)
                {
                    UiFailed("follow 目标解析失败");
                    return;
                }
                int cap = WebBackpackStorage.NormalizeCapacity(_storage.m_capacity);
                // 位置与尺寸：原版 follow 偏移偏低会遮挡玩家；大容量整体缩小
                Vector3 offset = new Vector3(0f, OffsetYForCap(cap), 0f);
                float scale = UiScaleForCap(cap);
                var spawn = SpawnHoverIcon(prefab, follow, offset);
                if (spawn == null)
                {
                    UiFailed("InstantiateHoverIconUIController 返回空（HoverIconCanvas 缺失？）");
                    return;
                }
                spawn.name = "WebBackpackContentsUI";
                if (scale < 1f)
                    spawn.transform.localScale = Vector3.one * scale;
                _uiContainer = GameApi.IngredientContentsUIContainerType != null
                    ? spawn.GetComponent(GameApi.IngredientContentsUIContainerType)
                    : null;
                if (_uiContainer == null)
                {
                    UiFailed("实例上无 IngredientContentsUIContainer");
                    return;
                }
                _uiInstance = spawn;
                ApplyGridLayout(cap);
                if (GameApi.UiContainerSetMinimumSizeMethod != null)
                {
                    GameApi.UiContainerSetMinimumSizeMethod.Invoke(_uiContainer,
                        new object[] { cap });
                }
                StubLog.Log("[WebBackpack] 宫格构建: 原版 IngredientsContentsUI 实例化成功（cap="
                    + cap + "，列数=" + _storage.GridCols() + "，scale=" + scale + "）");
            }
            catch (Exception ex)
            {
                UiFailed("构建异常: " + ex.Message);
            }
        }

        /// <summary>按容量重设宫格布局：原版 UI 序列化为固定 2 列（16 格会排成
        /// 2×8 且偏下遮挡）——改为 FixedColumnCount=GridCols(cap)（列多行少），
        /// 并按容量收窄格宽。</summary>
        private void ApplyGridLayout(int cap)
        {
            var layoutType = GameApi.AnchorGridLayoutGroupType;
            if (layoutType == null || _uiInstance == null)
                return;
            try
            {
                var layouts = _uiInstance.GetComponentsInChildren(layoutType, true);
                if (layouts == null || layouts.Length == 0)
                    return;
                var layout = layouts[0] as Component;
                if (layout == null)
                    return;
                int cols = _storage != null ? _storage.GridCols() : 2;
                float cell = CellSizeForCap(cap);
                if (GameApi.GridConstraintField != null)
                    GameApi.GridConstraintField.SetValue(layout, 1); // FixedColumnCount
                if (GameApi.GridConstraintCountField != null)
                    GameApi.GridConstraintCountField.SetValue(layout, cols);
                if (GameApi.GridCellSizeField != null)
                    GameApi.GridCellSizeField.SetValue(layout, new Vector2(cell, cell));
                StubLog.Log("[WebBackpack] 宫格布局: " + cols + " 列 × cell=" + cell);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[WebBackpack] 宫格布局设置异常（沿用原版 2 列）: " + ex.Message);
            }
        }

        /// <summary>格宽（锚点单位）随容量收窄：≤4: 0.5 / 6: 0.42 / 9: 0.36 /
        /// 12: 0.33 / 16: 0.30 / 20: 0.28 / 25: 0.26 / 30: 0.24。</summary>
        internal static float CellSizeForCap(int cap)
        {
            switch (cap)
            {
                case 1:
                case 2:
                case 4:
                    return 0.5f;
                case 6:
                    return 0.42f;
                case 9:
                    return 0.36f;
                case 12:
                    return 0.33f;
                case 16:
                    return 0.3f;
                case 20:
                    return 0.28f;
                case 25:
                    return 0.26f;
                default:
                    return 0.24f; // 30
            }
        }

        private static float UiScaleForCap(int cap)
        {
            if (cap >= 30) return 0.78f;
            if (cap >= 25) return 0.8f;
            if (cap >= 20) return 0.82f;
            if (cap >= 16) return 0.85f;
            if (cap >= 12) return 0.88f;
            if (cap >= 9) return 0.9f;
            return 1f;
        }

        private static float OffsetYForCap(int cap)
        {
            if (cap >= 25) return 1.8f;
            if (cap >= 9) return 1.7f;
            return 1.55f;
        }

        private void UiFailed(string reason)
        {
            if (_uiFailed)
                return;
            _uiFailed = true;
            StubLog.LogWarn("[WebBackpack] 宫格构建失败（本次穿戴不再重试）: " + reason);
        }

        private void TeardownUi()
        {
            _uiContainer = null;
            if (_uiInstance != null)
                Destroy(_uiInstance);
            _uiInstance = null;
            _uiFailed = false;
        }

        private GameObject LoadContentsUiPrefab()
        {
            if (_storage != null && _storage.m_contentsUiSO != null)
            {
                var prefab = WebBackpackStorage.LoadPrefabFromPseudoSo(_storage.m_contentsUiSO);
                if (prefab != null)
                    return prefab;
            }
            try
            {
                var bundle = GameApi.GetAssetBundle(FallbackUiBundle);
                if (bundle == null)
                    return null;
                return bundle.LoadAsset<GameObject>(FallbackUiPath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Transform ResolveFollowTarget()
        {
            if (GameApi.NetworkUtilsType != null)
            {
                var findRoot = GameApi.NetworkUtilsType.GetMethod("FindVisualRoot",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null);
                if (findRoot != null)
                {
                    var root = findRoot.Invoke(null, new object[] { gameObject }) as Transform;
                    if (root != null)
                        return root;
                }
            }
            return transform;
        }

        private static GameObject SpawnHoverIcon(GameObject prefab, Transform follow, Vector3 offset)
        {
            if (prefab == null || follow == null || GameApi.GameUtilsType == null)
                return null;
            var methods = GameApi.GameUtilsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != "InstantiateHoverIconUIController" || !m.IsPublic)
                    continue;
                var ps = m.GetParameters();
                if (ps.Length != 4 || ps[0].ParameterType != typeof(GameObject)
                    || ps[1].ParameterType != typeof(Transform))
                    continue;
                return m.Invoke(null, new object[] { prefab, follow, "HoverIconCanvas", offset }) as GameObject;
            }
            return null;
        }

        // -------------------------------------------------------------
        // 内容驱动
        // -------------------------------------------------------------

        private void RefreshOrder(bool force)
        {
            if (_uiContainer == null || GameApi.UiContainerSetOrderMethod == null)
                return;
            var contents = GetClientContents();
            int count = contents != null ? contents.Length : 0;
            var sig = BuildSignature(contents);
            if (!force && sig == _lastSignature)
                return;
            bool changed = sig != _lastSignature;
            _lastSignature = sig;
            if (changed || force)
                StubLog.Log("[WebBackpack] 宫格内容: " + count + "/"
                    + WebBackpackStorage.NormalizeCapacity(_storage.m_capacity) + " 格");
            var aggregate = BuildAggregate(contents);
            if (aggregate == null)
                return;
            try
            {
                GameApi.UiContainerSetOrderMethod.Invoke(_uiContainer, new object[] { aggregate });
                // 内容变化：驱动客户端 spawnable 注册（运行期放入的食材）
                if (changed)
                    _storage.OnClientContentsChangedForRegistration(contents);
            }
            catch (Exception ex)
            {
                UiFailed("SetOrder 异常: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private Array GetClientContents()
        {
            EnsureClientIngredient();
            if (_clientIngredient == null || GameApi.GetContentsMethod == null)
                return null;
            try
            {
                return GameApi.GetContentsMethod.Invoke(_clientIngredient, null) as Array;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string BuildSignature(Array contents)
        {
            if (contents == null)
                return "empty";
            var parts = new List<string>(contents.Length);
            for (int i = 0; i < contents.Length; i++)
            {
                var node = contents.GetValue(i);
                parts.Add(node != null ? node.GetHashCode().ToString() : "n");
            }
            return string.Join("|", parts.ToArray());
        }

        /// <summary>内容 → CompositeAssembledNode（vanilla 容器遍历 composition
        /// 取各节点的 m_iconSprite；空包 = 空组合 → 全空位格）。</summary>
        private static object BuildAggregate(Array contents)
        {
            var compositeType = GameApi.CompositeAssembledType;
            var nodeType = GameApi.AssembledDefinitionNodeType;
            if (compositeType == null || nodeType == null)
                return null;
            try
            {
                var aggregate = Activator.CreateInstance(compositeType);
                if (aggregate == null)
                    return null;
                int n = contents != null ? contents.Length : 0;
                var composition = Array.CreateInstance(nodeType, n);
                for (int i = 0; i < n; i++)
                    composition.SetValue(contents.GetValue(i), i);
                GameApi.CompositeCompositionField.SetValue(aggregate, composition);
                var optional = Array.CreateInstance(nodeType, 0);
                GameApi.CompositeOptionalField.SetValue(aggregate, optional);
                return aggregate;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[WebBackpack] 聚合节点构建异常: " + ex.Message);
                return null;
            }
        }
    }
}
