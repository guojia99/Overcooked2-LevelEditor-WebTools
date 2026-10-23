using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>上游 RecipeWidgetTileFollow 的真机等价：5+ 菜品格入场动画时把
    /// tile[0] 的内部 Rect 复制到 tile[4..n-2]，避免只动前 4 格。由
    /// RecipeWidgetUIController.OnCreateSubObjects 后缀自动挂到同一 GameObject。</summary>
    internal class RecipeWidgetTileFollowRuntime : MonoBehaviour
    {
        private List<RectTransform> _tiles;
        private Animator _animator;
        private static readonly int NothingHash = Animator.StringToHash("Nothing");

        internal void Init(IList tiles, Animator animator)
        {
            _tiles = new List<RectTransform>();
            if (tiles != null)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    var c = tiles[i] as Component;
                    if (c != null)
                        _tiles.Add(c.transform as RectTransform);
                }
            }
            _animator = animator;
        }

        private void LateUpdate()
        {
            if (_tiles == null || _tiles.Count <= 5 || _animator == null)
            {
                enabled = false;
                return;
            }
            CopyRect();
            var state = _animator.GetCurrentAnimatorStateInfo(1);
            if (state.shortNameHash == NothingHash && !_animator.IsInTransition(1))
            {
                CopyRect();
                enabled = false;
            }
        }

        private void CopyRect()
        {
            var t0 = _tiles[0];
            if (t0 == null || t0.childCount < 2)
                return;
            var p0 = t0.GetChild(0) as RectTransform;
            if (p0 == null || p0.childCount < 2)
                return;
            var c00 = p0.GetChild(0) as RectTransform;
            var c01 = p0.GetChild(1) as RectTransform;
            for (int i = 4; i < _tiles.Count - 1; i++)
            {
                var ti = _tiles[i];
                if (ti == null || ti.childCount < 2)
                    continue;
                var pi = ti.GetChild(0) as RectTransform;
                if (pi == null || pi.childCount < 2)
                    continue;
                var ci0 = pi.GetChild(0) as RectTransform;
                var ci1 = pi.GetChild(1) as RectTransform;
                pi.anchoredPosition = p0.anchoredPosition;
                pi.sizeDelta = p0.sizeDelta;
                if (c00 != null && ci0 != null)
                {
                    ci0.anchoredPosition = c00.anchoredPosition;
                    ci0.sizeDelta = c00.sizeDelta;
                }
                if (c01 != null && ci1 != null)
                {
                    ci1.anchoredPosition = c01.anchoredPosition;
                    ci1.sizeDelta = c01.sizeDelta;
                }
            }
        }
    }

    internal static class RecipeWidgetTileFollowInstaller
    {
        private static bool s_patched;

        internal static void Install()
        {
            if (s_patched)
                return;
            s_patched = true;
            try
            {
                var type = GameApi.Find("RecipeWidgetUIController");
                if (type == null)
                    return;
                var target = type.GetMethod("OnCreateSubObjects",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(GameObject) }, null);
                if (target == null)
                    return;
                var postfix = typeof(RecipeWidgetTileFollowInstaller).GetMethod("Postfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (postfix == null)
                    return;
                new Harmony("oc2.customstub.recipewidgetfollow").Patch(
                    target, null, new HarmonyMethod(postfix));
                StubLog.Log("[CustomStub] RecipeWidgetTileFollow 运行时补丁已装（5+ 菜品格入场动画）");
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] RecipeWidgetTileFollow 补丁安装失败: " + ex.Message);
            }
        }

        private static void Postfix(object __instance, GameObject _container)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return;
                var type = comp.GetType();
                var tilesField = type.GetField("m_recipeTiles",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var animField = type.GetField("m_widgetAnimator",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (tilesField == null || animField == null)
                    return;
                var tiles = tilesField.GetValue(comp) as IList;
                if (tiles == null || tiles.Count <= 5)
                    return;
                var follow = comp.gameObject.GetComponent<RecipeWidgetTileFollowRuntime>();
                if (follow == null)
                    follow = comp.gameObject.AddComponent<RecipeWidgetTileFollowRuntime>();
                follow.Init(tiles, animField.GetValue(comp) as Animator);
            }
            catch
            {
            }
        }
    }
}
