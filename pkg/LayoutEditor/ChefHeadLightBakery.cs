using System.Collections.Generic;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 厨师头灯：开启时对齐 s_oc1_story_4_2（Chefs/Player N Container + HeadLight），
/// 关闭时回退为 Chefs 直下 Player 占位。
/// </summary>
public static class ChefHeadLightBakery
{
    private const string ChefsRootName = "Chefs";
    private const string HeadLightName = "HeadLight";
    private const string ContainerSuffix = " Container";

    // s_oc1_story_4_2 HeadLight（Spot）
    private static readonly Color HeadLightColor = new Color(1f, 0.95101416f, 0.8455882f, 1f);
    private const float HeadLightIntensity = 11.5f;
    public const float DefaultHeadLightRange = 10f;
    private const float MinHeadLightRange = 0.01f;
    private const float MaxHeadLightRange = 100f;
    private const float HeadLightSpotAngle = 70.2f;
    private static readonly Vector3 HeadLightLocalPosition = new Vector3(0f, 3.12f, 0.55f);
    private static readonly Quaternion HeadLightLocalRotation =
        Quaternion.Euler(52.95f, 0f, 0f);

    /// <summary>场景 Chefs 下是否存在 HeadLight（导出推断开关）。</summary>
    public static bool DetectEnabledInScene(Scene scene)
    {
        var chefs = FindChefsTransform(scene);
        if (chefs == null)
            return false;

        var stack = new Stack<Transform>();
        stack.Push(chefs);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t.gameObject.name == HeadLightName)
                return true;
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }

        return false;
    }

    /// <summary>从 Chefs 下首个 HeadLight 读取 range；无头灯时返回默认 10。</summary>
    public static float ReadHeadLightRangeFromScene(Scene scene)
    {
        var chefs = FindChefsTransform(scene);
        if (chefs == null)
            return DefaultHeadLightRange;

        var stack = new Stack<Transform>();
        stack.Push(chefs);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t.gameObject.name == HeadLightName)
            {
                var light = t.GetComponent<Light>();
                if (light != null)
                    return ClampRange(light.range);
            }
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }

        return DefaultHeadLightRange;
    }

    public static float ClampRange(float range)
    {
        if (range <= 0f)
            return DefaultHeadLightRange;
        return Mathf.Clamp(range, MinHeadLightRange, MaxHeadLightRange);
    }

    public static string Sync(Scene scene, bool enabled, float range)
    {
        if (!scene.IsValid())
            return "无效场景，无法同步厨师头灯。";

        if (enabled)
            return Enable(scene, ClampRange(range));
        Disable(scene);
        return null;
    }

    public static string Enable(Scene scene, float range)
    {
        var chefs = FindChefsTransform(scene);
        if (chefs == null)
            return "未找到 Chefs 根，无法启用厨师头灯。";

        var spawns = CollectPlayerSpawns(chefs);
        if (spawns.Count == 0)
            return "Chefs 下未找到 Player 出生点，无法启用厨师头灯。";

        for (int i = 0; i < spawns.Count; i++)
        {
            var spawn = spawns[i];
            var container = GetOrCreateContainer(chefs, spawn, i);
            if (container == null)
                continue;
            EnsureHeadLight(container, spawn.stub, range);
        }

        return null;
    }

    public static void Disable(Scene scene)
    {
        var chefs = FindChefsTransform(scene);
        if (chefs == null)
            return;

        var toDestroy = new List<GameObject>();
        for (int i = 0; i < chefs.childCount; i++)
        {
            var child = chefs.GetChild(i);
            if (!IsPlayerContainer(child))
                continue;
            var playerRoot = FindPlayerPrefabRootUnder(child);
            if (playerRoot != null)
            {
                var worldPos = playerRoot.position;
                var worldRot = playerRoot.rotation;
                Undo.SetTransformParent(playerRoot.transform, chefs, "Layout Editor Chef Head Lights Off");
                playerRoot.position = worldPos;
                playerRoot.rotation = worldRot;
            }
            toDestroy.Add(child.gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
            Undo.DestroyObjectImmediate(toDestroy[i]);

        RemoveOrphanHeadLights(chefs);
    }

    private static Transform FindChefsTransform(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            var chefs = FindChildByName(roots[r].transform, ChefsRootName);
            if (chefs != null)
                return chefs;
        }
        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private sealed class PlayerSpawn
    {
        public Transform root;
        public PseudoPrefabPlayerStub stub;
    }

    private static List<PlayerSpawn> CollectPlayerSpawns(Transform chefs)
    {
        var list = new List<PlayerSpawn>();
        var stubs = chefs.GetComponentsInChildren<PseudoPrefabPlayerStub>(true);
        for (int i = 0; i < stubs.Length; i++)
        {
            var stub = stubs[i];
            if (stub == null)
                continue;
            var go = stub.gameObject;
            if (!IsPlayerPrefabInstanceRoot(go))
                continue;
            list.Add(new PlayerSpawn { root = go.transform, stub = stub });
        }
        return list;
    }

    private static bool IsPlayerPrefabInstanceRoot(GameObject go)
    {
        var prefabType = PrefabUtility.GetPrefabType(go);
        if (prefabType != PrefabType.PrefabInstance && prefabType != PrefabType.DisconnectedPrefabInstance)
            return false;
        var root = PrefabUtility.FindPrefabRoot(go);
        return root == go;
    }

    private static Transform GetOrCreateContainer(Transform chefs, PlayerSpawn spawn, int fallbackIndex)
    {
        var playerT = spawn.root;
        var parent = playerT.parent;
        if (parent != null && IsPlayerContainer(parent))
            return parent;

        var containerName = ContainerNameForStub(spawn.stub, fallbackIndex);
        var containerGo = new GameObject(containerName);
        Undo.RegisterCreatedObjectUndo(containerGo, "Layout Editor Chef Head Lights");
        var containerT = containerGo.transform;
        Undo.SetTransformParent(containerT, chefs, "Layout Editor Chef Head Lights");
        containerT.position = playerT.position;
        containerT.rotation = playerT.rotation;
        containerT.localScale = Vector3.one;
        var worldPos = playerT.position;
        var worldRot = playerT.rotation;
        Undo.SetTransformParent(playerT, containerT, "Layout Editor Chef Head Lights");
        playerT.position = worldPos;
        playerT.rotation = worldRot;
        return containerT;
    }

    private static string ContainerNameForStub(PseudoPrefabPlayerStub stub, int fallbackIndex)
    {
        if (stub != null)
        {
            var id = stub.playerID;
            if (id != PseudoPrefabPlayerStub.Player.Count)
            {
                int slot = (int)id + 1;
                return "Player " + slot + ContainerSuffix;
            }
        }
        return "Player " + (fallbackIndex + 1) + ContainerSuffix;
    }

    private static bool IsPlayerContainer(Transform t)
    {
        return t != null && t.name.EndsWith(ContainerSuffix, System.StringComparison.Ordinal);
    }

    private static Transform FindPlayerPrefabRootUnder(Transform container)
    {
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child.name == HeadLightName)
                continue;
            var stub = child.GetComponent<PseudoPrefabPlayerStub>();
            if (stub != null && IsPlayerPrefabInstanceRoot(child.gameObject))
                return child;
        }
        return null;
    }

    private static void EnsureHeadLight(Transform container, PseudoPrefabPlayerStub playerStub, float range)
    {
        Transform headT = null;
        for (int i = 0; i < container.childCount; i++)
        {
            var c = container.GetChild(i);
            if (c.name == HeadLightName)
            {
                headT = c;
                break;
            }
        }

        GameObject headGo;
        if (headT == null)
        {
            headGo = new GameObject(HeadLightName);
            Undo.RegisterCreatedObjectUndo(headGo, "Layout Editor Chef Head Light");
            headT = headGo.transform;
            Undo.SetTransformParent(headT, container, "Layout Editor Chef Head Light");
            Undo.AddComponent<Light>(headGo);
            Undo.AddComponent<FollowPseudoPlayer>(headGo);
            Undo.AddComponent<FollowPseudoPlayerStub>(headGo);
        }
        else
        {
            headGo = headT.gameObject;
        }

        headT.localPosition = HeadLightLocalPosition;
        headT.localRotation = HeadLightLocalRotation;
        headT.localScale = Vector3.one;

        var light = headGo.GetComponent<Light>();
        if (light == null)
            light = Undo.AddComponent<Light>(headGo);
        Undo.RecordObject(light, "Layout Editor Chef Head Light");
        light.type = LightType.Spot;
        light.color = HeadLightColor;
        light.intensity = HeadLightIntensity;
        light.range = ClampRange(range);
        light.spotAngle = HeadLightSpotAngle;
        light.shadows = LightShadows.Hard;
        light.renderMode = LightRenderMode.ForcePixel;
        light.enabled = true;

        var followStub = headGo.GetComponent<FollowPseudoPlayerStub>();
        if (followStub == null)
            followStub = Undo.AddComponent<FollowPseudoPlayerStub>(headGo);
        Undo.RecordObject(followStub, "Layout Editor Chef Head Light");
        followStub.playerStub = playerStub;

        if (headGo.GetComponent<FollowPseudoPlayer>() == null)
            Undo.AddComponent<FollowPseudoPlayer>(headGo);
    }

    private static void RemoveOrphanHeadLights(Transform chefs)
    {
        var stack = new Stack<Transform>();
        stack.Push(chefs);
        var orphans = new List<GameObject>();
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
            if (t.parent == chefs && t.gameObject.name == HeadLightName)
                orphans.Add(t.gameObject);
        }
        for (int i = 0; i < orphans.Count; i++)
            Undo.DestroyObjectImmediate(orphans[i]);
    }
}
