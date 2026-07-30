using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only walkability & death configuration for the floor layer.
/// - Walkable rectangles come from BoxColliders on the "Ground" layer (Col_Floor /
///   Col_Ice). Ice is detected via a PlayerPhysicsSurface component.
/// - The void death type (water / goo / fall) is derived from the scene's
///   GameSession.OnDeathEffectSO asset GUID.
/// </summary>
public static class SceneWalkabilityReader
{
    private const string WaterSplashGuid = "66a94ec69fed59240b93b7a666dfc2be";
    private const string AlienSplashGuid = "128828689ccfff044987de90cbf47363";

    private static readonly string[] DeathEffectPropertyNames =
        { "OnDeathEffectSO", "onDeathEffectSO", "m_OnDeathEffectSO", "onDeathEffect" };

    public static List<WalkableRectDto> ReadWalkable()
    {
        var rects = new List<WalkableRectDto>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return rects;

        foreach (var root in scene.GetRootGameObjects())
        {
            var colliders = root.GetComponentsInChildren<BoxCollider>(true);
            foreach (var col in colliders)
            {
                if (col == null)
                    continue;
                var go = col.gameObject;
                if (!IsGroundLayer(go))
                    continue;

                var b = col.bounds;
                if (b.size.x <= 0.001f || b.size.z <= 0.001f)
                    continue;

                rects.Add(new WalkableRectDto
                {
                    surfaceType = HasPlayerPhysicsSurface(go) ? "ice" : "solid",
                    cx = b.center.x,
                    cz = b.center.z,
                    sx = b.size.x,
                    sz = b.size.z,
                    sourcePath = LayoutEditorHierarchy.GetHierarchyPath(go.transform),
                });
            }
        }

        return rects;
    }

    public static DeathInfoDto ReadDeathInfo()
    {
        var info = new DeathInfoDto
        {
            deathType = "fall",
            deathEffectName = "",
            killPlanes = ReadKillPlanes(),
        };

        // Primary source: the PseudoPrefabManagerStub's OnDeathEffectSO (what the
        // death-theme editor writes, and the authoritative runtime binding).
        var stub = UnityEngine.Object.FindObjectOfType<LevelEditorStub.PseudoPrefabManagerStub>();
        if (stub != null)
        {
            var eff = stub.OnDeathEffectSO;
            if (eff != null)
            {
                var path = AssetDatabase.GetAssetPath(eff);
                var guid = AssetDatabase.AssetPathToGUID(path);
                info.deathEffectName = eff.name;
                info.deathType = ClassifyDeathGuid(guid, eff.name);
            }
            return info;
        }

        var gs = FindFirstComponent("GameSession");
        if (gs != null)
        {
            var so = new SerializedObject(gs);
            so.Update();
            foreach (var propName in DeathEffectPropertyNames)
            {
                var prop = so.FindProperty(propName);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                var obj = prop.objectReferenceValue;
                if (obj == null)
                {
                    info.deathType = "fall";
                    break;
                }

                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);
                info.deathEffectName = obj.name;
                info.deathType = ClassifyDeathGuid(guid, obj.name);
                break;
            }
        }

        return info;
    }

    public static KillPlaneInfoDto[] ReadKillPlanes()
    {
        var list = new List<KillPlaneInfoDto>();
        var found = FindAllComponents("RespawnCollider");
        foreach (var mb in found)
        {
            var so = new SerializedObject(mb);
            so.Update();
            var respawn = so.FindProperty("m_respawnType");
            if (respawn == null)
                respawn = so.FindProperty("respawnType");

            string typeLabel = "Drowning";
            if (respawn != null && respawn.propertyType == SerializedPropertyType.Enum)
                typeLabel = RespawnTypeLabel(respawn.enumValueIndex);
            else if (respawn != null && respawn.propertyType == SerializedPropertyType.Integer)
                typeLabel = RespawnTypeLabel(respawn.intValue);

            string effectName = "";
            foreach (var propName in DeathEffectPropertyNames)
            {
                var prop = so.FindProperty(propName);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (prop.objectReferenceValue != null)
                    effectName = prop.objectReferenceValue.name;
                break;
            }

            float kcx = 0f, kcz = 0f, ksx = 0f, ksz = 0f;
            var col = mb.GetComponent<Collider>();
            if (col != null)
            {
                var b = col.bounds;
                kcx = b.center.x;
                kcz = b.center.z;
                ksx = b.size.x;
                ksz = b.size.z;
            }

            list.Add(new KillPlaneInfoDto
            {
                hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(mb.transform),
                respawnType = typeLabel,
                deathEffectName = effectName,
                cx = kcx,
                cz = kcz,
                sx = ksx,
                sz = ksz,
            });
        }

        return list.ToArray();
    }

    private static string ClassifyDeathGuid(string guid, string assetName)
    {
        if (guid == WaterSplashGuid)
            return "water";
        if (guid == AlienSplashGuid)
            return "goo";
        if (!string.IsNullOrEmpty(assetName) && assetName.ToLowerInvariant().Contains("alien"))
            return "goo";
        if (!string.IsNullOrEmpty(guid))
            return "water";
        return "fall";
    }

    private static string RespawnTypeLabel(int value)
    {
        switch (value)
        {
            case 0: return "Hit";
            case 1: return "Drowning";
            case 2: return "FallDeath";
            case 3: return "Car";
            default: return value.ToString();
        }
    }

    private static bool IsGroundLayer(GameObject go)
    {
        if (go == null)
            return false;
        return go.layer == 9 || LayerMask.LayerToName(go.layer) == "Ground";
    }

    private static bool HasPlayerPhysicsSurface(GameObject go)
    {
        var comps = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null)
                continue;
            var n = comps[i].GetType().Name;
            if (n == "PlayerPhysicsSurface" || n.Contains("PhysicsSurface"))
                return true;
        }
        return false;
    }

    private static MonoBehaviour FindFirstComponent(string typeNameSubstring)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;
                if (mb.GetType().Name.Contains(typeNameSubstring))
                    return mb;
            }
        }
        return null;
    }

    private static List<MonoBehaviour> FindAllComponents(string typeNameSubstring)
    {
        var result = new List<MonoBehaviour>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return result;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;
                if (mb.GetType().Name.Contains(typeNameSubstring))
                    result.Add(mb);
            }
        }
        return result;
    }
}
