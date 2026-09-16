using System;
using System.Collections;
using System.Collections.Generic;
using GameModes;
using UnityEngine;

public class GameSession : MonoBehaviour
{
	public enum GameType
	{
		Cooperative = 0,
		Competitive = 1
	}

	[Serializable]
	public class GameTypeSettings
	{
		public GameType Type;

		[SceneName]
		public string WorldMapScene = "WorldMap";
	}

	[Serializable]
	public class GameLevelSettings
	{
		public SceneDirectoryData.PerPlayerCountDirectoryEntry SceneDirectoryVarientEntry;
	}

	[Serializable]
	public class SelectedChefData
	{
		public ChefAvatarData Character;

		public ChefColourData Colour;

		public SelectedChefData(ChefAvatarData _chefAvatarData, ChefColourData _colourData)
		{
			Character = _chefAvatarData;
			Colour = _colourData;
		}
	}

	[HideInInspector]
	[SerializeField]
	private uint m_DLCAppId;

	[HideInInspector]
	[SerializeField]
	private string m_DLCXB1ProductId = string.Empty;

	[SerializeField]
	private GameTypeSettings m_gameTypeSettings = new GameTypeSettings();

	[SerializeField]
	private int m_DLCId;

	private SaveManager m_saveManager;

	private GameProgress m_progress;

	private GameLevelSettings m_levelSettings = new GameLevelSettings();

	private int m_saveSlot;

	public const int c_invalidSaveSlot = -1;

	[NonSerialized]
	public bool[] m_shownMetaDialogs = new bool[2];

	private HighScoreRepository m_HighScoreRepository = new HighScoreRepository();

	public bool MarkedForDeath;

	public OnSessionConfigChanged OnGameModeSessionConfigChanged;

	private SessionConfig m_gameModeSessionConfig = new SessionConfig();

	private SessionConfig m_internalGameModeSessionConfig = new SessionConfig();

	public HighScoreRepository HighScoreRepository
	{
		get
		{
			return m_HighScoreRepository;
		}
	}

	public int DLC
	{
		get
		{
			return m_DLCId;
		}
	}

	public int SaveSlot
	{
		get
		{
			return m_saveSlot;
		}
		set
		{
			m_saveSlot = value;
		}
	}

	public GameProgress Progress
	{
		get
		{
			return m_progress;
		}
	}

	public GameLevelSettings LevelSettings
	{
		get
		{
			return m_levelSettings;
		}
		set
		{
			m_levelSettings = value;
		}
	}

	public GameTypeSettings TypeSettings
	{
		get
		{
			return m_gameTypeSettings;
		}
	}

	public bool PendingGameModeSessionConfigChanges { get; private set; }

	public SessionConfig GameModeSessionConfig
	{
		get
		{
			return m_gameModeSessionConfig;
		}
		set
		{
			PendingGameModeSessionConfigChanges = false;
			m_gameModeSessionConfig = value;
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				m_internalGameModeSessionConfig.Copy(m_gameModeSessionConfig);
				m_saveManager.RegisterOnIdle(delegate
				{
					SaveSession();
				});
			}
			if (OnGameModeSessionConfigChanged != null)
			{
				OnGameModeSessionConfigChanged(m_gameModeSessionConfig);
			}
		}
	}

	public Kind GameModeKind
	{
		get
		{
			return m_gameModeSessionConfig.m_kind;
		}
		set
		{
			PendingGameModeSessionConfigChanges = true;
			m_gameModeSessionConfig.m_kind = value;
		}
	}

	private void Awake()
	{
		m_saveManager = GameUtils.RequireManager<SaveManager>();
		m_progress = base.gameObject.RequireComponentRecursive<GameProgress>();
		m_progress.SetSession(this);
		m_HighScoreRepository.Initialise(m_DLCId);
	}

	private void OnDestroy()
	{
		m_HighScoreRepository.Shutdown();
	}

	private IEnumerator SaveSessionRoutine(SaveSystemCallback _callback = null)
	{
		m_internalGameModeSessionConfig.Save(m_saveManager.GetMetaGameProgress().SaveData);
		yield return m_saveManager.SaveData(GetSaveMode(), m_saveSlot, m_DLCId, _callback);
	}

	public void SaveSession(SaveSystemCallback _callback = null)
	{
		if (IsSaveable())
		{
			StartCoroutine(SaveSessionRoutine(_callback));
		}
		else if (_callback != null)
		{
			_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.NotSaveable));
		}
	}

	private bool IsLoadable()
	{
		return m_saveManager != null && m_saveManager.ProfileLoaded && m_saveSlot != -1 && m_gameTypeSettings.Type == GameType.Cooperative && ClientGameSetup.Mode != GameMode.Party && ClientGameSetup.Mode != GameMode.Versus;
	}

	private bool IsSaveable()
	{
		return m_saveManager != null && m_saveManager.ProfileLoaded && m_saveSlot != -1 && (m_progress.UseSlaveSlot || !ConnectionStatus.IsInSession() || ConnectionStatus.IsHost()) && m_gameTypeSettings.Type == GameType.Cooperative && ClientGameSetup.Mode != GameMode.Party && ClientGameSetup.Mode != GameMode.Versus;
	}

	private SaveMode GetSaveMode()
	{
		if (m_gameTypeSettings.Type == GameType.Cooperative)
		{
			return SaveMode.Main;
		}
		return SaveMode.Main;
	}

	public IEnumerator HasSaveFile(ReturnValue<SaveLoadResult> _return)
	{
		if (IsLoadable())
		{
			IEnumerator run = m_saveManager.HasSaveFile(GetSaveMode(), m_saveSlot, m_DLCId, _return);
			while (run.MoveNext())
			{
				yield return null;
			}
		}
		else
		{
			_return.Value = SaveLoadResult.NotSaveable;
		}
	}

	public void DeleteSave()
	{
		if (IsSaveable())
		{
			m_saveManager.DeleteSave(GetSaveMode(), m_saveSlot, m_DLCId);
		}
	}

	public IEnumerator<SaveLoadResult?> LoadSession()
	{
		if (IsLoadable())
		{
			IEnumerator<SaveLoadResult?> routine = m_saveManager.LoadSave(GetSaveMode(), m_saveSlot, m_DLCId);
			while (routine.MoveNext())
			{
				yield return null;
			}
			yield return routine.Current;
			m_internalGameModeSessionConfig.Load(m_saveManager.GetMetaGameProgress().SaveData);
			m_gameModeSessionConfig.Copy(m_internalGameModeSessionConfig);
		}
		else
		{
			yield return SaveLoadResult.Exists;
		}
	}

	public void CommitGameModeSessionConfig()
	{
		if (!PendingGameModeSessionConfigChanges)
		{
			return;
		}
		PendingGameModeSessionConfigChanges = false;
		m_internalGameModeSessionConfig.Copy(m_gameModeSessionConfig);
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_saveManager.RegisterOnIdle(delegate
			{
				SaveSession();
			});
			ServerMessenger.SendHostModeConfigChanged(m_internalGameModeSessionConfig);
			if (OnGameModeSessionConfigChanged != null)
			{
				OnGameModeSessionConfigChanged(m_internalGameModeSessionConfig);
			}
		}
	}

	public void RevertGameModeSessionConfig()
	{
		if (!PendingGameModeSessionConfigChanges)
		{
			return;
		}
		PendingGameModeSessionConfigChanges = false;
		m_gameModeSessionConfig.Copy(m_internalGameModeSessionConfig);
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_saveManager.RegisterOnIdle(delegate
			{
				SaveSession();
			});
		}
	}

	public bool GetGameModeSetting(SettingKind kind)
	{
		return m_gameModeSessionConfig.m_settings[(int)kind];
	}

	public bool SetGameModeSetting(SettingKind kind, bool value)
	{
		bool result = m_gameModeSessionConfig.m_settings[(int)kind];
		m_gameModeSessionConfig.m_settings[(int)kind] = value;
		PendingGameModeSessionConfigChanges = true;
		return result;
	}

	public IServerMode GetGameModeServer(KitchenLevelConfigBase levelConfig)
	{
		switch (GameModeKind)
		{
		case Kind.Campaign:
			return new ServerCampaignMode(levelConfig.m_campaignConfig);
		case Kind.Practice:
			return new ServerPracticeMode(levelConfig.m_practiceConfig);
		case Kind.Survival:
			return new ServerSurvivalMode(levelConfig.m_survivalConfig);
		default:
			return null;
		}
	}

	public IClientMode GetGameModeClient(KitchenLevelConfigBase levelConfig)
	{
		switch (GameModeKind)
		{
		case Kind.Campaign:
			return new ClientCampaignMode(levelConfig.m_campaignConfig);
		case Kind.Practice:
			return new ClientPracticeMode(levelConfig.m_practiceConfig);
		case Kind.Survival:
			return new ClientSurvivalMode(levelConfig.m_survivalConfig);
		default:
			return null;
		}
	}

	public void FillShownMetaDialogStatus()
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		for (int i = 0; i < 2; i++)
		{
			m_shownMetaDialogs[i] = metaGameProgress.HasShownMetaDialog((MetaGameProgress.MetaDialogType)i);
		}
	}

	public bool HasShownMetaDialog(MetaGameProgress.MetaDialogType _type)
	{
		return m_shownMetaDialogs[(int)_type];
	}

	public void SetMetaDialogShown(MetaGameProgress.MetaDialogType _type)
	{
		m_shownMetaDialogs[(int)_type] = true;
	}
}
