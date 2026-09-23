using System;
using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes the native ConveyorStation button animation contract. A station must
/// stay one object: putting it in two regular animation groups reparents it
/// twice and invalidates the first group's hierarchy path on the next import.
/// </summary>
public static class ConveyorAnimationBakery
{
    private const string StartTrigger = "Animate";
    private const string FinishedTrigger = "AnimationFinished";

    public static void Sync(Scene scene, LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects)
    {
        // 【已退役 2026-09-24】传送带按钮旋转不再走「烘焙 Animator + controller + relay +
        // 宿主 TriggerAnimationOnConveyor」这条链——它依赖烘焙期伪预制子物体在场（常常不在），
        // 且宿主组件开局自动开播导致自动旋转/按钮失效。现由运行时 CustomStub.ConveyorDirectionSync
        // （tag 自愈，必定在场）用纯 Transform 旋转承担，见 conveyor.buttonControlled 写回路径。
        // 本烘焙器整体停用：不再烘焙任何 Animator / controller / relay / 宿主组件。
        return;
    }

    private static Component FindChildComponent(GameObject root, string typeName)
    {
        var direct = root.GetComponent(typeName);
        if (direct != null)
            return direct;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null && component.GetType().Name == typeName)
                return component;
        }
        return null;
    }

    private static GameObject Resolve(string id, Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(id)) return null;
        GameObject go;
        if (createdObjects != null && createdObjects.TryGetValue(id, out go)) return go;
        if (!id.StartsWith("u:", StringComparison.Ordinal)) return null;
        int iid;
        return int.TryParse(id.Substring(2), out iid)
            ? EditorUtility.InstanceIDToObject(iid) as GameObject
            : null;
    }

    private static AnimationClip BuildClip(string path, string name,
        Quaternion baseRotation, float delta)
    {
        var clip = new AnimationClip();
        clip.name = name;
        clip.legacy = false;
        clip.frameRate = 60f;
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();
        var w = new AnimationCurve();
        int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(delta) / 90f));
        for (int i = 0; i <= steps; i++)
        {
            float k = (float)i / steps;
            var q = baseRotation * Quaternion.AngleAxis(delta * k, Vector3.up);
            var t = k * 2f;
            x.AddKey(new Keyframe(t, q.x));
            y.AddKey(new Keyframe(t, q.y));
            z.AddKey(new Keyframe(t, q.z));
            w.AddKey(new Keyframe(t, q.w));
        }
        SetLinear(x); SetLinear(y); SetLinear(z); SetLinear(w);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.x"), x);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.y"), y);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.z"), z);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.w"), w);
        var evt = new AnimationEvent();
        evt.functionName = "OnTrigger";
        evt.stringParameter = FinishedTrigger;
        evt.time = 2f;
        AnimationUtility.SetAnimationEvents(clip, new[] { evt });
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController BuildController(string path, string name,
        AnimationClip clipA, AnimationClip clipB)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller == null) return null;
        controller.name = name;
        controller.AddParameter(StartTrigger, AnimatorControllerParameterType.Trigger);
        var sm = controller.layers[0].stateMachine;
        var idleA = sm.AddState("IdleA");
        var rotateA = sm.AddState("RotateA");
        var idleB = sm.AddState("IdleB");
        var rotateB = sm.AddState("RotateB");
        idleA.writeDefaultValues = false;
        idleB.writeDefaultValues = false;
        rotateA.writeDefaultValues = false;
        rotateB.writeDefaultValues = false;
        rotateA.motion = clipA;
        rotateB.motion = clipB;
        sm.defaultState = idleA;
        AddTriggerTransition(idleA, rotateA);
        AddTriggerTransition(idleB, rotateB);
        AddExitTransition(rotateA, idleB);
        AddExitTransition(rotateB, idleA);
        return controller;
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.hasFixedDuration = true;
        t.AddCondition(AnimatorConditionMode.If, 0f, StartTrigger);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
        t.hasFixedDuration = true;
    }

    private static void SetString(Component component, string name, string value)
    {
        var so = new SerializedObject(component);
        var prop = so.FindProperty(name);
        if (prop == null) return;
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Component component, string name, bool value)
    {
        var so = new SerializedObject(component);
        var prop = so.FindProperty(name);
        if (prop == null) return;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLinear(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            var key = curve[i];
            key.inTangent = 0f;
            key.outTangent = 0f;
            curve.MoveKey(i, key);
        }
    }
}
