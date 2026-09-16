using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 控制终端运行时补丁（Play 期）。
///
/// 背景：宿主 PseudoPrefabTerminal.Setup 执行
///   child.GetComponent&lt;Terminal&gt;().m_pilotableObject =
///     stub.pilotableObject.GetComponent&lt;PilotMovement&gt;();
/// 大炮等伪 prefab 的 PilotMovement 在 bundle child 上，stub 若指向伪根则拿到 null
/// → ClientTerminalCosmeticDecisions.Update 每帧对 m_pilotableObject.HasMoved() 抛 NRE。
/// 写回侧（LayoutEditorStubIO.RedirectPilotableToChild）已把 stub 指向 child，
/// 但历史场景/旧写回的 stub 可能仍指伪根，Setup 已把 null 烧进 child 的 Terminal。
///
/// 本补丁在 Play 期对每个 Terminal 校正：
///   1) m_pilotableObject 为 null 时，从所在伪根的 PseudoPrefabTerminalStub 重新解析
///      （伪根 → bundle child 重定向）并回填 child Terminal / TerminalCosmeticDecisions；
///   2) 仍解析不到时禁用 ClientTerminalCosmeticDecisions（纯装饰组件），
///      阻止每帧 NRE 刷屏——终端本身不可用属于配置缺失，不应崩溃编辑器。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorTerminalPatch
{
    private static bool _armed;

    static LayoutEditorTerminalPatch()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _armed = true;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    private static void Tick()
    {
        if (!_armed || !Application.isPlaying)
        {
            _armed = false;
            EditorApplication.update -= Tick;
            return;
        }
        var terminals = UnityEngine.Object.FindObjectsOfType<Terminal>();
        if (terminals == null || terminals.Length == 0)
        {
            _armed = false;
            EditorApplication.update -= Tick;
            return;
        }
        bool allOk = true;
        for (int i = 0; i < terminals.Length; i++)
        {
            var terminal = terminals[i];
            if (terminal == null)
                continue;
            try
            {
                if (!PatchTerminal(terminal))
                    allOk = false; // 有未修复的终端，继续轮询（等 child/stub 就绪）
            }
            catch (Exception)
            {
                // 对象销毁/同步未完成，下一帧再试
                allOk = false;
            }
        }
        if (allOk)
        {
            // 全部终端就绪（或已禁用装饰兜底），停止轮询
            _armed = false;
            EditorApplication.update -= Tick;
        }
    }

    /// <summary>返回 false = 需要下一帧继续校正（child 未生成等）。</summary>
    private static bool PatchTerminal(Terminal terminal)
    {
        if (terminal.m_pilotableObject != null)
            return true; // 宿主已正确接线

        // 从 child 所在伪根重新解析 stub（child 是 bundle 实例，伪根 = 宿主对象）
        var root = terminal.transform.parent;
        var stub = root != null ? root.GetComponent<LevelEditorStub.PseudoPrefabTerminalStub>() : null;
        if (stub == null || stub.pilotableObject == null)
        {
            // 无 stub 可解析：禁用装饰组件兜底，阻止每帧 NRE
            DisableCosmetics(terminal);
            return true;
        }

        var target = stub.pilotableObject;
        var pilot = target.GetComponent<PilotMovement>();
        if (pilot == null)
        {
            // stub 指向伪根 → 重定向到携带 PilotMovement 的 bundle child
            var pp = target.GetComponent<LevelEditor.PseudoPrefab>();
            if (pp == null || pp.childGameObject == null)
                return false; // child 尚未生成，下一帧再试
            pilot = pp.childGameObject.GetComponent<PilotMovement>();
            if (pilot != null)
                stub.pilotableObject = pp.childGameObject; // 顺带修正 stub，落盘后不再复发
        }
        if (pilot == null)
        {
            DisableCosmetics(terminal);
            return true;
        }

        terminal.m_pilotableObject = pilot;
        return true;
    }

    private static void DisableCosmetics(Terminal terminal)
    {
        var cosmetic = terminal.GetComponent<ClientTerminalCosmeticDecisions>();
        if (cosmetic != null && cosmetic.enabled)
        {
            cosmetic.enabled = false;
            LayoutEditorLog.LogWarning("[LayoutEditor] 终端无可操控对象（pilotableObject 为空），" +
                "已禁用其装饰组件防止 NRE: " + terminal.gameObject.name);
        }
    }
}
