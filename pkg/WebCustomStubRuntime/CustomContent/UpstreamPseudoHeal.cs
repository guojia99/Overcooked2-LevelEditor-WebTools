using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// 上游 PseudoPrefab 新标签/组件的真机等价自愈（LevelEditor.PseudoPrefab 只存在于
    /// 编辑器宿主编译；游戏侧没有 LevelEditor.*，因此这里用运行时反射重放最小行为）：
    ///   TriggerOnChild  → TriggerOnObject.m_targetObject = 子物体
    ///   Bellows         → 给 m_animTransform 补 Animator 并赋 m_animController（幂等）
    ///   cobble          → 复制材质并清 _Detail 贴图（按材质名缓存）
    ///   DestroyComponentOfChildStub → 删除子物体上指定类型组件
    /// 只在场景里实际存在对应 tag/组件时做事；普通关卡零开销。
    /// </summary>
    internal static class UpstreamPseudoHeal
    {
        private static readonly Dictionary<string, Material> s_cobbleMaterials =
            new Dictionary<string, Material>(StringComparer.Ordinal);
        private static readonly HashSet<string> s_warned = new HashSet<string>(StringComparer.Ordinal);
        private static bool s_bellowsPatched;

        /// <summary>上游 ClientBellowsCosmeticDecisions 补丁：若编辑器/自愈已给
        /// m_animTransform 挂了 Animator，复用现有实例，避免二次 AddComponent 触发
        /// StateMachineBehaviour 反序列化失败（上游 commit ef5dab4cc）。</summary>
        private static void EnsureBellowsPatch()
        {
            if (s_bellowsPatched)
                return;
            s_bellowsPatched = true;
            try
            {
                var type = GameApi.Find("ClientBellowsCosmeticDecisions");
                if (type == null)
                    return;
                var target = type.GetMethod("AddAnimator",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (target == null)
                    return;
                var prefix = typeof(UpstreamPseudoHeal).GetMethod("AddAnimatorPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (prefix == null)
                    return;
                new Harmony("oc2.customstub.bellows").Patch(target, new HarmonyMethod(prefix));
                StubLog.Log("[CustomStub] ClientBellowsCosmeticDecisions.AddAnimator 复用补丁已装");
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] Bellows Animator 补丁安装失败: " + ex.Message);
            }
        }

        private static bool AddAnimatorPrefix(object __instance)
        {
            try
            {
                if (__instance == null)
                    return true;
                var type = __instance.GetType();
                var cosmeticsField = type.GetField("m_cosmetics",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var animatorField = type.GetField("m_animator",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (cosmeticsField == null || animatorField == null)
                    return true;
                var cosmetics = cosmeticsField.GetValue(__instance);
                if (cosmetics == null)
                    return true;
                var transformField = cosmetics.GetType().GetField("m_animTransform",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (transformField == null)
                    return true;
                var animTransform = transformField.GetValue(cosmetics) as Transform;
                if (animTransform == null)
                    return true;
                var existing = animTransform.gameObject.GetComponent<Animator>();
                if (existing == null)
                    return true;
                animatorField.SetValue(__instance, existing);
                return false;
            }
            catch (Exception ex)
            {
                WarnOnce("bellowsPrefix", "[CustomStub] Bellows AddAnimator 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        internal static void OnSceneChanged()
        {
            s_cobbleMaterials.Clear();
        }

        internal static void Apply(Scene scene)
        {
            if (!scene.isLoaded)
                return;
            try
            {
                var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                        continue;
                    if (tag.prefabTag != "TriggerOnChild"
                        && tag.prefabTag != "Bellows"
                        && tag.prefabTag != "cobble")
                        continue;
                    var child = ChildOf(tag.gameObject);
                    if (child == null)
                        continue;
                    if (tag.prefabTag == "TriggerOnChild")
                        ApplyTriggerOnChild(tag.gameObject, child);
                    else if (tag.prefabTag == "Bellows")
                    {
                        EnsureBellowsPatch();
                        ApplyBellows(child);
                    }
                    else
                        ApplyCobble(child);
                }

                // DestroyComponentOfChildStub 是上游新类型：用反射/类型名扫描，兼容旧
                // LevelEditorStub.dll（直接 FindObjectsOfType<T> 会在程序集加载期缺类型）。
                var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                for (int i = 0; i < all.Length; i++)
                {
                    var d = all[i];
                    if (d == null || d.GetType().Name != "DestroyComponentOfChildStub")
                        continue;
                    var namesField = d.GetType().GetField("componentNames",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var names = namesField != null ? namesField.GetValue(d) as string[] : null;
                    if (names == null || names.Length == 0)
                        continue;
                    var child = ChildOf(d.gameObject);
                    if (child == null)
                        continue;
                    for (int j = 0; j < names.Length; j++)
                        DestroyNamedComponent(child, names[j]);
                }
            }
            catch (Exception ex)
            {
                WarnOnce("apply", "[CustomStub] 上游伪 prefab 标签自愈失败: " + ex.Message);
            }
        }

        private static GameObject ChildOf(GameObject go)
        {
            if (go == null)
                return null;
            var t = go.transform;
            if (t.childCount == 0)
                return go;
            return t.GetChild(0).gameObject;
        }

        private static void ApplyTriggerOnChild(GameObject root, GameObject child)
        {
            var type = GameApi.Find("TriggerOnObject");
            if (type == null)
                return;
            var field = type.GetField("m_targetObject",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return;
            var comps = root.GetComponents(type);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    field.SetValue(comps[i], child);
            }
        }

        private static void ApplyBellows(GameObject child)
        {
            var type = GameApi.Find("BellowsCosmeticDecisions");
            if (type == null)
                return;
            var comp = child.GetComponent(type);
            if (comp == null)
                return;
            var transformField = type.GetField("m_animTransform",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var controllerField = type.GetField("m_animController",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (transformField == null || controllerField == null)
                return;
            var animTransform = transformField.GetValue(comp) as Transform;
            var controller = controllerField.GetValue(comp) as RuntimeAnimatorController;
            if (animTransform == null || controller == null)
                return;
            var animator = animTransform.gameObject.GetComponent<Animator>();
            if (animator == null)
                animator = animTransform.gameObject.AddComponent<Animator>();
            if (animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = controller;
        }

        private static void ApplyCobble(GameObject child)
        {
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;
                var source = renderer.sharedMaterial;
                if (!source.HasProperty("_Detail"))
                    continue;
                Material copy;
                if (!s_cobbleMaterials.TryGetValue(source.name, out copy))
                {
                    copy = new Material(source);
                    copy.SetTexture("_Detail", null);
                    s_cobbleMaterials[source.name] = copy;
                }
                renderer.sharedMaterial = copy;
            }
        }

        private static void DestroyNamedComponent(GameObject child, string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return;
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && type == null; i++)
                    type = assemblies[i].GetType(typeName);
            }
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                WarnOnce("type:" + typeName, "[CustomStub] DestroyComponentOfChildStub 找不到组件类型: " + typeName);
                return;
            }
            var comps = child.GetComponents(type);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    UnityEngine.Object.Destroy(comps[i]);
            }
        }

        private static void WarnOnce(string key, string msg)
        {
            if (s_warned.Add(key))
                StubLog.LogWarn(msg);
        }
    }
}
