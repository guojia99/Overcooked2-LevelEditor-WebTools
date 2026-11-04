using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LayoutEditorAnimatorAudioRepair
{
    private static bool _armed;
    private static double _deadline;
    private static double _nextScan;

    static LayoutEditorAnimatorAudioRepair()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            _deadline = EditorApplication.timeSinceStartup + 120.0;
            _nextScan = 0.0;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Disarm();
        }
    }

    private static void Disarm()
    {
        _armed = false;
        EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (!_armed || !Application.isPlaying ||
            EditorApplication.timeSinceStartup > _deadline)
        {
            Disarm();
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextScan)
            return;
        _nextScan = EditorApplication.timeSinceStartup + 2.0;
        EnsureReceivers();
    }

    [MenuItem("Layout Editor/诊断 (Diagnostics)/补齐动画 AudioTrigger 接收器", false, 42)]
    private static void EnsureReceiversMenu()
    {
        var added = EnsureReceivers();
        EditorUtility.DisplayDialog("动画音频接收器",
            added > 0 ? "已补齐 " + added + " 个 AnimatorAudioComponent。" : "所有动画音频接收器均已就绪。", "好");
    }

    private static int EnsureReceivers()
    {
        var added = 0;
        var missing = new List<GameObject>();
        var animators = Resources.FindObjectsOfTypeAll<Animator>();
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            if (animator == null)
                continue;
            var go = animator.gameObject;
            if (go == null || !go.scene.IsValid())
                continue;
            if (go.GetComponent<AnimatorAudioComponent>() != null)
                continue;
            if (!ControllerNeedsAudioReceiver(animator.runtimeAnimatorController))
                continue;
            missing.Add(go);
        }
        for (int i = 0; i < missing.Count; i++)
        {
            if (missing[i] == null)
                continue;
            missing[i].AddComponent<AnimatorAudioComponent>();
            added++;
        }
        if (added > 0)
            LayoutEditorLog.Log("[AnimatorAudio] 已补齐 " + added + " 个 AnimatorAudioComponent（缺失音频接收器）");
        return added;
    }

    private static bool ControllerNeedsAudioReceiver(RuntimeAnimatorController controller)
    {
        if (controller == null)
            return false;
        var clips = controller.animationClips;
        if (clips == null)
            return false;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;
            var events = clip.events;
            if (events == null)
                continue;
            for (int j = 0; j < events.Length; j++)
            {
                var name = events[j].functionName;
                if (name == "AudioTrigger" || name == "AudioStart" || name == "AudioStop")
                    return true;
            }
        }
        return false;
    }
}
