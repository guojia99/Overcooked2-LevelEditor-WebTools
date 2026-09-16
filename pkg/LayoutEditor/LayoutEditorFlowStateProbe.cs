using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play 模式「游戏不开始」探针：进入 Play 后每 2s 采样一次厨房加载状态机，
/// 状态变化（或每 15s 心跳）写一条 [flow-probe] 日志（由 PlayModeLogCapture 落盘）。
///
/// 背景：s_jia_level_base 进 Play 后「有画面但没倒计时/没厨师」，Console 无任何
/// 异常——说明卡在某个状态机等待条件上。本探针把以下黑盒状态摊开：
///   - ServerKitchenLoader.m_State / ClientKitchenLoader.m_GameState（私有，反射读）
///   - 每个 User.GameState（AreAllUsersInGameState 的判定依据）
///   - PlayerIDProvider.s_AllProviders（厨师实体）及其 pseudo child 是否已生成
///   - EntitySerialisationRegistry 实体数、MultiplayerController/ComponentCacheRegistry.ScanActive
///   - KitchenLoaderManager 是否已 StartKitchen、ServerCampaignFlowController 是否存在
/// 全部 try/catch，绝不影响运行流程。菜单：Layout Editor → Play 流程状态探针。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorFlowStateProbe
{
    private const string EnabledPrefKey = "LayoutEditor.FlowStateProbe.Enabled";
    private const string ToggleMenuPath = "Layout Editor/Play 流程状态探针（[flow-probe]）";
    private const double SampleIntervalSeconds = 2.0;
    private const double HeartbeatSeconds = 15.0;

    private static bool _active;
    private static double _startTime;
    private static double _nextSample;
    private static double _nextHeartbeat;
    private static string _lastDigest = "";

    static LayoutEditorFlowStateProbe()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (IsEnabled() && EditorApplication.isPlayingOrWillChangePlaymode)
            Arm();
    }

    [MenuItem(ToggleMenuPath, false, 303)]
    private static void ToggleEnabled()
    {
        EditorPrefs.SetBool(EnabledPrefKey, !IsEnabled());
    }

    [MenuItem(ToggleMenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(ToggleMenuPath, IsEnabled());
        return true;
    }

    private static bool IsEnabled()
    {
        return EditorPrefs.GetBool(EnabledPrefKey, true);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (IsEnabled()) Arm();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode ||
                 state == PlayModeStateChange.EnteredEditMode)
        {
            Disarm();
        }
    }

    private static void Arm()
    {
        if (_active) return;
        _active = true;
        _startTime = EditorApplication.timeSinceStartup;
        _nextSample = 0;
        _nextHeartbeat = 0;
        _lastDigest = "";
        EditorApplication.update += OnUpdate;
    }

    private static void Disarm()
    {
        if (!_active) return;
        _active = false;
        EditorApplication.update -= OnUpdate;
    }

    private static void OnUpdate()
    {
        if (!_active || !Application.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextSample) return;
        _nextSample = now + SampleIntervalSeconds;
        try
        {
            string digest;
            string line = BuildSnapshot(now, out digest);
            bool changed = digest != _lastDigest;
            if (changed || now >= _nextHeartbeat)
            {
                _lastDigest = digest;
                _nextHeartbeat = now + HeartbeatSeconds;
                Debug.Log(line);
            }
        }
        catch (Exception e)
        {
            Debug.Log("[flow-probe] sample failed: " + e.GetType().Name + " " + e.Message);
        }
    }

    private static string ReadPrivateField(object obj, string field)
    {
        if (obj == null) return "(null)";
        try
        {
            FieldInfo f = obj.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return "(no-field)";
            object v = f.GetValue(obj);
            return v == null ? "(null)" : v.ToString();
        }
        catch (Exception) { return "(err)"; }
    }

    private static string BuildSnapshot(double now, out string digest)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append("[flow-probe] t=").Append((now - _startTime).ToString("0.0")).Append("s");

        // —— 加载状态机（两侧）——
        ServerKitchenLoader serverLoader = UnityEngine.Object.FindObjectOfType<ServerKitchenLoader>();
        ClientKitchenLoader clientLoader = UnityEngine.Object.FindObjectOfType<ClientKitchenLoader>();
        sb.Append(" | server=").Append(serverLoader == null ? "MISSING" : ReadPrivateField(serverLoader, "m_State"));
        sb.Append(" client=").Append(clientLoader == null ? "MISSING" : ReadPrivateField(clientLoader, "m_GameState"));

        // —— KitchenLoaderManager 是否已触发 StartKitchen ——
        KitchenLoaderManager klm = KitchenLoaderManager.s_Instance;
        sb.Append(" | loaderMgr=").Append(klm == null ? "MISSING" : "started=" + ReadPrivateField(klm, "m_bStarted"));

        // —— 连接进度（StartKitchen 的前置条件）——
        try
        {
            sb.Append(" conn=").Append(ConnectionModeSwitcher.GetStatus() == null
                ? "(null)" : ConnectionModeSwitcher.GetStatus().GetProgress().ToString());
        }
        catch (Exception) { sb.Append(" conn=(err)"); }

        // —— 每个用户的 GameState ——
        try
        {
            FastList<User> users = ServerUserSystem.m_Users;
            sb.Append(" | users=").Append(users == null ? -1 : users.Count).Append('[');
            if (users != null)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(users._items[i] == null ? "null" : users._items[i].GameState.ToString());
                }
            }
            sb.Append(']');
        }
        catch (Exception) { sb.Append(" | users=(err)"); }

        // —— 厨师实体 / pseudo child ——
        try
        {
            FastList<PlayerIDProvider> providers = PlayerIDProvider.s_AllProviders;
            int withChild = 0;
            int active = 0;
            if (providers != null)
            {
                for (int i = 0; i < providers.Count; i++)
                {
                    PlayerIDProvider p = providers._items[i];
                    if (p == null) continue;
                    LevelEditor.PseudoPrefab pseudo = p.GetComponentInParent<LevelEditor.PseudoPrefab>();
                    if (pseudo != null && pseudo.childGameObject != null) withChild++;
                    if (p.gameObject.activeInHierarchy) active++;
                }
            }
            sb.Append(" | chefs=").Append(providers == null ? -1 : providers.Count)
              .Append("(child=").Append(withChild).Append(",active=").Append(active).Append(')');
        }
        catch (Exception) { sb.Append(" | chefs=(err)"); }

        // —— 实体扫描状态 ——
        try
        {
            MultiplayerController mp = UnityEngine.Object.FindObjectOfType<MultiplayerController>();
            sb.Append(" | scan=").Append(mp != null && mp.ScanActive ? "busy" : "idle")
              .Append('/').Append(ComponentCacheRegistry.ScanActive ? "busy" : "idle");
        }
        catch (Exception) { sb.Append(" | scan=(err)"); }
        try
        {
            sb.Append(" entities=").Append(EntitySerialisationRegistry.m_EntitiesList == null
                ? -1 : EntitySerialisationRegistry.m_EntitiesList.Count);
        }
        catch (Exception) { sb.Append(" entities=(err)"); }

        // —— 流控制器 ——
        sb.Append(" | serverFlow=").Append(UnityEngine.Object.FindObjectOfType<ServerCampaignFlowController>() != null ? "ok" : "MISSING");
        sb.Append(" clientFlow=").Append(UnityEngine.Object.FindObjectOfType<CampaignFlowController>() != null ? "ok" : "MISSING");

        // —— GameSession / LevelSettings ——
        try
        {
            GameSession gs = UnityEngine.Object.FindObjectOfType<GameSession>();
            string sceneName = "(null)";
            if (gs != null && gs.LevelSettings != null && gs.LevelSettings.SceneDirectoryVarientEntry != null)
                sceneName = gs.LevelSettings.SceneDirectoryVarientEntry.SceneName ?? "(null)";
            sb.Append(" | session=").Append(gs == null ? "MISSING" : sceneName);
        }
        catch (Exception) { sb.Append(" | session=(err)"); }

        string line = sb.ToString();
        // digest 去掉时间前缀，用于变化检测
        int bar = line.IndexOf('|');
        digest = bar >= 0 ? line.Substring(bar) : line;
        return line;
    }
}
