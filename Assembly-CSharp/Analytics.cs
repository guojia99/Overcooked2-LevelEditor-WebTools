#define ANALYTICS
using System;
using System.Diagnostics;
using Team17.Online;
using UnityEngine;

public class Analytics : Manager
{
	[Flags]
	public enum Flags
	{
		LevelName = 1,
		PlayerCount = 2
	}

	[SerializeField]
	private GameObject[] m_AnalyticsPrefabs;

	private const string m_googleAnalyticsPrefabName = "Analytics/GAv3_live";

	private GoogleAnalyticsV3 m_GoogleAnalytics;

	private void Awake()
	{
		StartupAnalytics();
		StartupT17Analytics();
	}

	private void OnDestroy()
	{
		ShutdownAnalytics();
	}

	private void StartupAnalytics()
	{
		if (string.IsNullOrEmpty("Analytics/GAv3_live"))
		{
			return;
		}
		GameObject gameObject = null;
		if (m_AnalyticsPrefabs != null)
		{
			int i = 0;
			for (int num = m_AnalyticsPrefabs.Length; i < num; i++)
			{
				if ("Analytics/GAv3_live".EndsWith(m_AnalyticsPrefabs[i].name))
				{
					gameObject = m_AnalyticsPrefabs[i];
					break;
				}
			}
		}
		else
		{
			gameObject = Resources.Load("Analytics/GAv3_live") as GameObject;
		}
		if (gameObject == null)
		{
			return;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
		if (gameObject2 == null)
		{
			return;
		}
		gameObject2.transform.parent = base.transform;
		m_GoogleAnalytics = gameObject2.GetComponent<GoogleAnalyticsV3>();
		if (m_GoogleAnalytics != null)
		{
			string text = "UNKNOWN";
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			if (onlinePlatformManager != null)
			{
				text = onlinePlatformManager.Name();
			}
			m_GoogleAnalytics.bundleVersion = text + "." + BuildVersion.m_VersionString;
			m_GoogleAnalytics.SetAppLevelOptOut(false);
			m_GoogleAnalytics.getUserData = GameUtils.DebugGetState;
			m_GoogleAnalytics.Initialize();
			m_GoogleAnalytics.StartSession();
		}
	}

	private void ShutdownAnalytics()
	{
		if (m_GoogleAnalytics != null)
		{
			m_GoogleAnalytics.StopSession();
			m_GoogleAnalytics = null;
		}
	}

	[Conditional("ANALYTICS")]
	public void LogAnException(string log, string stackTrace, string userData)
	{
	}

	[Conditional("ANALYTICS")]
	private void LogEventInternal(string category, string action, string label, long value)
	{
		if (m_GoogleAnalytics != null)
		{
			m_GoogleAnalytics.LogEvent(category, action, label, value);
		}
	}

	[Conditional("ANALYTICS")]
	public static void LogEvent(string category, string action, string label, long value)
	{
		Analytics analytics = GameUtils.RequestManager<Analytics>();
		if ((bool)analytics)
		{
			analytics.LogEventInternal(category, action, label, value);
		}
	}

	private static string GetCurrentLevelName()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = gameSession.LevelSettings.SceneDirectoryVarientEntry;
			if (sceneDirectoryVarientEntry != null)
			{
				return sceneDirectoryVarientEntry.SceneName;
			}
		}
		return "<Unknown Level>";
	}

	private static bool IsServer()
	{
		return ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession();
	}

	private static GameMode GetCurrentGameMode()
	{
		return (!IsServer()) ? ClientGameSetup.Mode : ServerGameSetup.Mode;
	}

	private static int GetCurrentPlayerCount()
	{
		return (!IsServer()) ? ClientUserSystem.m_Users.Count : ServerUserSystem.m_Users.Count;
	}

	private static void ProcessFlags(ref string category, ref string action, ref string label, ref long value, Flags flags)
	{
		if ((flags & Flags.LevelName) != 0)
		{
			action = ((!string.IsNullOrEmpty(action)) ? (action + " ") : string.Empty) + GetCurrentLevelName();
		}
		if ((flags & Flags.PlayerCount) != 0)
		{
			label = ((!string.IsNullOrEmpty(label)) ? (label + " ") : string.Empty) + GetCurrentPlayerCount() + " Player";
		}
	}

	[Conditional("ANALYTICS")]
	public static void LogEvent(string category, string action, long value = 0L, Flags flags = (Flags)0)
	{
		string label = ((!ConnectionStatus.IsInSession()) ? "Offline" : "Online");
		ProcessFlags(ref category, ref action, ref label, ref value, flags);
		LogEvent(category, action, label, value);
	}

	[Conditional("ANALYTICS")]
	public static void LogEvent(string action, long value = 0L, Flags flags = (Flags)0)
	{
		string category = GetCurrentGameMode().ToString();
		string label = ((!ConnectionStatus.IsInSession()) ? "Offline" : "Online");
		ProcessFlags(ref category, ref action, ref label, ref value, flags);
		LogEvent(category, action, label, value);
	}

	public static void StartupT17Analytics()
	{
		if (GameUtils.GetT17AnalyticsHelper() == null)
		{
			string text = Environment.MachineName;
			if (string.IsNullOrEmpty(text))
			{
				text = "noname";
			}
			GameUtils.InitialiseAnalytics("https://api.gameanalytics.com", "dce9689b8a79396089a774e5b0efdc89", "38fdf3b708b9f478dc890bd7566d23706a2ebea6", text, BuildVersion.m_VersionString);
			string text2 = "NotSet";
			text2 = "Steam";
			GameUtils.SendDiagnosticEvent(text2);
		}
	}

	public static void ShutdownT17Analytics()
	{
		GameUtils.ShutdownAnalytics();
	}
}
