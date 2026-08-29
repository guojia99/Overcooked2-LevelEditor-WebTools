using UnityEditor;
using UnityEngine;

/// <summary>
/// 移动组内空气地板的「岛」式层级，对齐 oc1_story 3-4（Island + Ground）：
///  Animator 驱动带 ObjectContainer 的容器，碰撞盒在子物体 Ground 上随父级移动。
/// </summary>
public static class AirFloorRig
{
    public const string WrapperName = "AirFloor";
    public const string GroundChildName = "Ground";

    public static bool IsColliderObject(GameObject go)
    {
        if (go == null)
            return false;
        if (SceneFloorExporter.IsAirFloorColliderName(go.name))
            return true;
        return go.name == GroundChildName
            && go.GetComponent<BoxCollider>() != null
            && go.transform.parent != null
            && IsWrapperName(go.transform.parent.name);
    }

    public static bool IsWrapperName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name == WrapperName
            || name.StartsWith(WrapperName + " (", System.StringComparison.Ordinal);
    }

    /// <summary>动画成员：包装后的 AirFloor 根，或未包装的 Col_AirFloor。</summary>
    public static GameObject GetAnimatedMember(GameObject go)
    {
        if (go == null)
            return null;
        if (go.name == GroundChildName && go.transform.parent != null
            && IsWrapperName(go.transform.parent.name))
            return go.transform.parent.gameObject;
        return go;
    }

    /// <summary>可行走碰撞物体（Ground 或 Col_AirFloor）。</summary>
    public static GameObject GetColliderObject(GameObject go)
    {
        if (go == null)
            return null;
        if (IsColliderObject(go))
            return go;
        if (IsWrapperName(go.name))
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var c = go.transform.GetChild(i);
                if (c != null && c.name == GroundChildName && c.GetComponent<BoxCollider>() != null)
                    return c.gameObject;
            }
        }
        return go;
    }

    /// <summary>把 Col_AirFloor 包成 AirFloor(ObjectContainer) / Ground(BoxCollider)。</summary>
    public static GameObject EnsureRig(GameObject go, Transform groupRoot)
    {
        if (go == null || groupRoot == null)
            return go;

        var animated = GetAnimatedMember(go);
        if (animated != null && IsWrapperName(animated.name)
            && animated.transform.parent == groupRoot)
        {
            if (animated.GetComponent<ObjectContainer>() == null)
                Undo.AddComponent<ObjectContainer>(animated);
            return animated;
        }

        if (!SceneFloorExporter.IsAirFloorColliderName(go.name))
            return go;

        var wrapper = new GameObject(WrapperName);
        Undo.RegisterCreatedObjectUndo(wrapper, "Layout Editor Air Floor Rig");
        var wt = wrapper.transform;
        if (go.transform.parent == groupRoot)
        {
            wt.SetParent(groupRoot, false);
            wt.localPosition = go.transform.localPosition;
            wt.localRotation = go.transform.localRotation;
        }
        else
        {
            Undo.SetTransformParent(wt, groupRoot, "Layout Editor Air Floor Rig");
            wt.position = go.transform.position;
            wt.rotation = go.transform.rotation;
        }
        wt.localScale = Vector3.one;

        Undo.SetTransformParent(go.transform, wt, "Layout Editor Air Floor Rig");
        go.name = GroundChildName;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var oc = go.GetComponent<ObjectContainer>();
        if (oc != null)
            Undo.DestroyObjectImmediate(oc);
        if (wrapper.GetComponent<ObjectContainer>() == null)
            Undo.AddComponent<ObjectContainer>(wrapper);

        return wrapper;
    }

    public static bool IsMoveGroupWalkCollider(GameObject go)
    {
        if (go == null)
            return false;
        if (IsColliderObject(go))
            return true;
        if (IsWrapperName(go.name))
            return true;
        return go.name == "Col_Floor"
            || go.name.StartsWith("Col_Floor (", System.StringComparison.Ordinal);
    }
}
