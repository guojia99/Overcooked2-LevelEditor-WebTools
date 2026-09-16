using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GameProgress : MonoBehaviour
{
	[Serializable]
	public class UnlockData
	{
		[Serializable]
		public class Condition
		{
			public enum ConditionType
			{
				LevelComplete = 0,
				LevelStars = 1,
				TotalStars = 2
			}

			public ConditionType Type;

			[LevelIndex]
			public int RequiredLevel = -1;

			public int RequiredStars;

			public bool CheckPreviousLevels;
		}

		public enum UnlockType
		{
			Avatar = 0,
			CompetitiveLevel = 1
		}

		[Serializable]
		public class AvatarDataType
		{
			[ArrayIndex("m_avatarDirectory", "Avatars", SerializationUtils.RootType.Top)]
			[SerializeField]
			private int AvatarID;

			private const int StorageSegmentation = 100;

			public static int GetStorageId(AvatarDirectoryData _directory, int _avatarID)
			{
				return _avatarID + _directory.DirectoryID * 100;
			}

			public int GetStorageId(AvatarDirectoryData _directory)
			{
				return GetStorageId(_directory, AvatarID);
			}

			public ChefAvatarData GetAvatarData(AvatarDirectoryData _directory)
			{
				return _directory.Avatars[AvatarID];
			}
		}

		[Serializable]
		public class SceneDataType
		{
			[AssignResource("CompetitiveGameSceneDirectory", Editorbility.NonEditable)]
			public SceneDirectoryData m_competitiveSceneDirectory;

			[ArrayIndex("m_competitiveSceneDirectory", "Scenes")]
			public int SceneID;
		}

		public UnlockType Type;

		[HideInInspectorTest("Type", UnlockType.Avatar)]
		public AvatarDataType AvatarData = new AvatarDataType();

		[HideInInspectorTest("Type", UnlockType.CompetitiveLevel)]
		public SceneDataType SceneData = new SceneDataType();

		public Condition Requirement = new Condition();

		public bool IsUnlocked(GameProgress _progress)
		{
			MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
			switch (Type)
			{
			case UnlockType.Avatar:
				return metaGameProgress.IsAvatarUnlocked(AvatarData.GetStorageId(_progress.m_avatarDirectory));
			case UnlockType.CompetitiveLevel:
				return true;
			default:
				return false;
			}
		}

		public void Unlock(GameProgress _progress)
		{
			if (!IsUnlocked(_progress))
			{
				MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
				switch (Type)
				{
				case UnlockType.Avatar:
					metaGameProgress.UnlockAvatar(AvatarData.GetStorageId(_progress.m_avatarDirectory));
					break;
				}
			}
		}
	}

	[Serializable]
	public class GameProgressData : IByteSerialization
	{
		[Serializable]
		public class LevelProgress
		{
			public const int MaxStars = 4;

			public const int InvalidScore = 65535;

			public const int MaxScore = 65534;

			public const int InvalidTime = 0;

			public const int MaxTime = 5999;

			public int LevelId = -1;

			public bool Completed;

			public bool Purchased;

			public bool Revealed;

			public int HighScore = int.MinValue;

			public int SurvivalModeTime;

			public int ScoreStars;

			public bool ObjectivesCompleted;

			public bool NGPEnabled;

			public Dictionary<string, string> GetSaveableData()
			{
				Dictionary<string, string> dictionary = new Dictionary<string, string>();
				dictionary.Add("LevelID", LevelId.ToString());
				dictionary.Add("Completed", Completed.ToString());
				dictionary.Add("Purchased", Purchased.ToString());
				dictionary.Add("Revealed", Revealed.ToString());
				dictionary.Add("HighScore", HighScore.ToString());
				dictionary.Add("SurvivalModeTime", SurvivalModeTime.ToString());
				dictionary.Add("ScoreStars", ScoreStars.ToString());
				dictionary.Add("ObjectivesCompleted", ObjectivesCompleted.ToString());
				dictionary.Add("NGPEnabled", NGPEnabled.ToString());
				return dictionary;
			}

			public bool SetData(Dictionary<string, string> _data)
			{
				int.TryParse(_data.SafeGet("LevelID", (-1).ToString()), out LevelId);
				bool.TryParse(_data.SafeGet("Completed", false.ToString()), out Completed);
				bool.TryParse(_data.SafeGet("Purchased", false.ToString()), out Purchased);
				bool.TryParse(_data.SafeGet("Revealed", false.ToString()), out Revealed);
				int.TryParse(_data.SafeGet("HighScore", 65535.ToString()), out HighScore);
				int.TryParse(_data.SafeGet("SurvivalModeTime", 0.ToString()), out SurvivalModeTime);
				int.TryParse(_data.SafeGet("ScoreStars", false.ToString()), out ScoreStars);
				bool.TryParse(_data.SafeGet("ObjectivesCompleted", false.ToString()), out ObjectivesCompleted);
				bool.TryParse(_data.SafeGet("NGPEnabled", false.ToString()), out NGPEnabled);
				return true;
			}
		}

		[Serializable]
		public class SwitchState
		{
			public int SwitchId = -1;

			public bool Activated;

			public void SetSwitchState(int iSwitchID, bool bActivated)
			{
				SwitchId = iSwitchID;
				Activated = bActivated;
			}
		}

		[Serializable]
		public class TeleportalState
		{
			public SceneDirectoryData.World World = SceneDirectoryData.World.COUNT;

			public Dictionary<string, string> GetSaveableData()
			{
				return new Dictionary<string, string>();
			}

			public bool SetData(Dictionary<string, string> _data)
			{
				return true;
			}
		}

		public LevelProgress[] Levels = new LevelProgress[0];

		public SwitchState[] Switches = new SwitchState[0];

		public TeleportalState[] Teleportals = new TeleportalState[0];

		[LevelIndex]
		public int LastLevelEntered = -1;

		public bool NewGamePlusEnabled;

		public bool NewGamePlusDialogShown;

		private HashSet<int> m_lvlUnlockedVisited;

		private const string c_versionTag = "VERSION";

		public uint SaveVersion
		{
			get
			{
				return 1u;
			}
		}

		public int ByteSaveSize
		{
			get
			{
				byte[] array = ByteSave();
				return array.Length;
			}
		}

		public LevelProgress GetLevelProgress(int _id)
		{
			int index = Levels.FindIndex_Predicate((LevelProgress x) => x.LevelId == _id);
			LevelProgress levelProgress = Levels.TryAtIndex(index);
			if (levelProgress != null)
			{
				return levelProgress;
			}
			return new LevelProgress();
		}

		public int GetStarTotal()
		{
			int num = 0;
			for (int i = 0; i < Levels.Length; i++)
			{
				LevelProgress levelProgress = Levels[i];
				int b = ((!levelProgress.NGPEnabled || !NewGamePlusDialogShown) ? 3 : 4);
				if (levelProgress.Completed)
				{
					num += Mathf.Min(levelProgress.ScoreStars, b);
				}
			}
			return num;
		}

		public int FarthestProgressedLevel(bool _requireComplete = true, bool _excludeHidden = true)
		{
			SceneDirectoryData sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
			int num = -1;
			for (int i = 0; i < Levels.Length; i++)
			{
				LevelProgress levelProgress = Levels[i];
				if (levelProgress.LevelId < sceneDirectory.Scenes.Length)
				{
					SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelProgress.LevelId];
					if ((!_requireComplete || levelProgress.Completed) && num < levelProgress.LevelId && IsLevelUnlocked(levelProgress.LevelId) && sceneDirectoryEntry.HasScoreBoundaries && (!_excludeHidden || !sceneDirectoryEntry.IsHidden))
					{
						num = levelProgress.LevelId;
					}
				}
			}
			return num;
		}

		private bool IsLevelChainComplete(int _levelIndex, bool _rootCompleteCheck, bool? _hiddenParent, ref HashSet<int> _visited)
		{
			SceneDirectoryData sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes.TryAtIndex(_levelIndex);
			LevelProgress levelProgress = GetLevelProgress(_levelIndex);
			_visited.Add(_levelIndex);
			if (_hiddenParent.HasValue)
			{
				if (!levelProgress.Completed)
				{
					return false;
				}
				if (_hiddenParent.Value && !levelProgress.ObjectivesCompleted)
				{
					return false;
				}
			}
			else if (_rootCompleteCheck && levelProgress.Completed)
			{
				return true;
			}
			int[] previousEntriesToUnlock = sceneDirectoryEntry.PreviousEntriesToUnlock;
			bool flag = false;
			bool flag2 = true;
			foreach (int num in previousEntriesToUnlock)
			{
				if (num != -1)
				{
					if (!_visited.Contains(num))
					{
						flag |= IsLevelChainComplete(num, _rootCompleteCheck, sceneDirectoryEntry.IsHidden, ref _visited);
					}
					flag2 = false;
				}
			}
			return flag || flag2;
		}

		public bool IsLevelUnlocked(int _levelIndex, bool _rootCompleteCheck = true)
		{
			if (m_lvlUnlockedVisited == null)
			{
				m_lvlUnlockedVisited = new HashSet<int>();
			}
			bool result = IsLevelChainComplete(_levelIndex, _rootCompleteCheck, null, ref m_lvlUnlockedVisited);
			m_lvlUnlockedVisited.Clear();
			return result;
		}

		public bool IsLevelComplete(int _levelIndex)
		{
			int num = Levels.FindIndex_Predicate((LevelProgress x) => x.LevelId == _levelIndex);
			if (num == -1 || !Levels[num].Completed)
			{
				return false;
			}
			return true;
		}

		public bool IsNGPEnabledForLevel(int _levelIndex)
		{
			int num = Levels.FindIndex_Predicate((LevelProgress x) => x.LevelId == _levelIndex);
			if (num == -1 || !Levels[num].NGPEnabled)
			{
				return false;
			}
			return true;
		}

		public bool IsNGPEnabledForAnyLevel()
		{
			for (int i = 0; i < Levels.Length; i++)
			{
				if (Levels[i].NGPEnabled)
				{
					return true;
				}
			}
			return false;
		}

		public bool HasStarsForLevel(int _levelIndex, int _stars, SceneDirectoryData _sceneDirectory)
		{
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = _sceneDirectory.Scenes.TryAtIndex(_levelIndex);
			if (sceneDirectoryEntry.HasScoreBoundaries)
			{
				int num = Levels.FindIndex_Predicate((LevelProgress x) => x.LevelId == _levelIndex);
				if (num == -1 || Levels[num].ScoreStars < _stars)
				{
					return false;
				}
			}
			else if (!IsLevelComplete(_levelIndex))
			{
				return false;
			}
			return true;
		}

		public bool IsTrueForLevelsRecursive(int _levelIndex, SceneDirectoryData _sceneDirectory, Generic<bool, int> _condition)
		{
			if (!_condition(_levelIndex))
			{
				return false;
			}
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = _sceneDirectory.Scenes.TryAtIndex(_levelIndex);
			int[] previousEntriesToUnlock = sceneDirectoryEntry.PreviousEntriesToUnlock;
			foreach (int num in previousEntriesToUnlock)
			{
				if (num != -1 && !IsTrueForLevelsRecursive(num, _sceneDirectory, _condition))
				{
					return false;
				}
			}
			return true;
		}

		public SwitchState GetSwitchState(int _id)
		{
			int index = Switches.FindIndex_Predicate((SwitchState x) => x.SwitchId == _id);
			SwitchState switchState = Switches.TryAtIndex(index);
			if (switchState != null)
			{
				return switchState;
			}
			return new SwitchState();
		}

		public TeleportalState GetTeleportalState(SceneDirectoryData.World _world)
		{
			int index = Teleportals.FindIndex_Predicate((TeleportalState x) => x.World == _world);
			TeleportalState teleportalState = Teleportals.TryAtIndex(index);
			if (teleportalState != null)
			{
				return teleportalState;
			}
			return new TeleportalState();
		}

		public byte[] ByteSave()
		{
			GlobalSave globalSave = new GlobalSave();
			globalSave.Set("VERSION", SaveVersion);
			globalSave.Set("Level_Count", Levels.Length);
			for (int i = 0; i < Levels.Length; i++)
			{
				globalSave.Set("Level_" + Levels[i].LevelId, Levels[i].GetSaveableData());
			}
			if (Switches.Length > 0)
			{
				int[] array = new int[Switches.Length];
				for (int j = 0; j < Switches.Length; j++)
				{
					SwitchState switchState = Switches[j];
					if (switchState != null)
					{
						array[j] = switchState.SwitchId;
						globalSave.Set("Switch_" + switchState.SwitchId, switchState.Activated);
					}
				}
				globalSave.Set("Switches_Revealed", array);
			}
			if (Teleportals.Length > 0)
			{
				int[] array2 = new int[Teleportals.Length];
				for (int k = 0; k < Teleportals.Length; k++)
				{
					TeleportalState teleportalState = Teleportals[k];
					if (teleportalState != null)
					{
						array2[k] = (int)teleportalState.World;
						globalSave.Set("Teleportal_" + teleportalState.World, teleportalState.GetSaveableData());
					}
				}
				globalSave.Set("Teleportals_Revealed", array2);
			}
			globalSave.Set("LastLevelEntered", LastLevelEntered);
			globalSave.Set("NewGamePlusEnabled", NewGamePlusEnabled);
			globalSave.Set("NewGamePlusDialogShown", NewGamePlusDialogShown);
			return globalSave.ByteSave();
		}

		public void FillOut(SceneDirectoryData _sceneDirectory)
		{
			int i;
			for (i = 0; i < _sceneDirectory.Scenes.Length; i++)
			{
				if (Levels.FindIndex_Predicate((LevelProgress x) => x.LevelId == i) == -1)
				{
					LevelProgress levelProgress = new LevelProgress();
					levelProgress.LevelId = i;
					levelProgress.Purchased = _sceneDirectory.Scenes[i].StarCost == 0;
					ArrayUtils.PushBack(ref Levels, levelProgress);
				}
			}
		}

		public bool ByteLoad(byte[] _data)
		{
			GlobalSave globalSave = new GlobalSave();
			if (!globalSave.ByteLoad(_data))
			{
				return false;
			}
			int value;
			globalSave.Get("VERSION", out value, 1);
			if (value != (int)SaveVersion)
			{
				return false;
			}
			int value2;
			globalSave.Get("Level_Count", out value2, 0);
			Levels = new LevelProgress[value2];
			for (int i = 0; i < value2; i++)
			{
				Dictionary<string, string> value3;
				globalSave.Get("Level_" + i, out value3, new Dictionary<string, string>());
				Levels[i] = new LevelProgress();
				if (!Levels[i].SetData(value3))
				{
					return false;
				}
			}
			int[] value4 = null;
			if (globalSave.Get("Switches_Revealed", out value4, (int[])null))
			{
				int num = value4.Length;
				Switches = new SwitchState[num];
				for (int j = 0; j < num; j++)
				{
					Switches[j] = new SwitchState();
					bool value5;
					globalSave.Get("Switch_" + value4[j], out value5, false);
					Switches[j].SetSwitchState(value4[j], value5);
				}
			}
			int[] value6 = null;
			if (globalSave.Get("Teleportals_Revealed", out value6, (int[])null))
			{
				int num2 = value6.Length;
				Teleportals = new TeleportalState[num2];
				for (int k = 0; k < num2; k++)
				{
					SceneDirectoryData.World world = (SceneDirectoryData.World)value6[k];
					Dictionary<string, string> value7;
					globalSave.Get("Teleportal_" + world, out value7, new Dictionary<string, string>());
					TeleportalState teleportalState = new TeleportalState();
					teleportalState.World = world;
					if (!teleportalState.SetData(value7))
					{
						return false;
					}
					Teleportals[k] = teleportalState;
				}
			}
			globalSave.Get("LastLevelEntered", out LastLevelEntered, -1);
			globalSave.Get("NewGamePlusEnabled", out NewGamePlusEnabled, false);
			globalSave.Get("NewGamePlusDialogShown", out NewGamePlusDialogShown, false);
			return true;
		}

		public static bool Validate(byte[] _data)
		{
			return new GlobalSave().ByteLoad(_data);
		}
	}

	public class HighScores
	{
		public class Score
		{
			public int iLevelID;

			public int iHighScore;

			public int iSurvivalModeTime;
		}

		public List<Score> Scores = new List<Score>();

		public HighScores Copy()
		{
			HighScores highScores = new HighScores();
			for (int i = 0; i < Scores.Count; i++)
			{
				Score score = Scores[i];
				highScores.Scores.Add(new Score
				{
					iLevelID = score.iLevelID,
					iHighScore = score.iHighScore,
					iSurvivalModeTime = score.iSurvivalModeTime
				});
			}
			return highScores;
		}
	}

	[SerializeField]
	private SceneDirectoryData m_sceneDirectory;

	[SerializeField]
	private AvatarDirectoryData m_avatarDirectory;

	[SerializeField]
	[FormerlySerializedAs("m_hasTutorialLevel")]
	private bool m_loadFirstScene;

	[SerializeField]
	[LevelIndex]
	[FormerlySerializedAs("m_tutorialLevel")]
	private int m_firstLevel = -1;

	[SerializeField]
	private UnlockData[] m_unlockData = new UnlockData[0];

	private GameSession m_session;

	private GameProgressData m_localSaveData = new GameProgressData();

	private GameProgressData m_remoteSaveData = new GameProgressData();

	private bool m_useSlaveSlot;

	private int[] m_levelChainEnds;

	public bool LoadFirstScene
	{
		get
		{
			return m_loadFirstScene;
		}
	}

	public int FirstSceneIndex
	{
		get
		{
			return m_firstLevel;
		}
	}

	public bool UseSlaveSlot
	{
		get
		{
			return m_useSlaveSlot;
		}
		set
		{
			m_useSlaveSlot = value;
		}
	}

	public int[] LevelChainEnds
	{
		get
		{
			return m_levelChainEnds;
		}
	}

	public GameProgressData SaveData
	{
		get
		{
			if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
			{
				return m_remoteSaveData;
			}
			return m_localSaveData;
		}
	}

	public GameProgressData SaveableData
	{
		get
		{
			return m_localSaveData;
		}
	}

	public void SetSession(GameSession _session)
	{
		m_session = _session;
	}

	private void Awake()
	{
		m_localSaveData.FillOut(m_sceneDirectory);
		SetupLevelChainEnds();
	}

	private void SetupLevelChainEnds()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < m_sceneDirectory.Scenes.Length; i++)
		{
			if (m_sceneDirectory.Scenes[i].LevelChainEnd)
			{
				list.Add(i);
			}
		}
		m_levelChainEnds = list.ToArray();
	}

	private int GetSceneIndex(string _sceneName)
	{
		return m_sceneDirectory.Scenes.FindIndex_Predicate((SceneDirectoryData.SceneDirectoryEntry x) => x.SceneVarients.FindIndex_Predicate((SceneDirectoryData.PerPlayerCountDirectoryEntry y) => y.SceneName == _sceneName) != -1);
	}

	public GameProgressData.LevelProgress GetProgress(string _sceneName)
	{
		GameProgressData saveData = SaveData;
		int sceneIndex = GetSceneIndex(_sceneName);
		return saveData.GetLevelProgress(sceneIndex);
	}

	public GameProgressData.LevelProgress GetProgress(int _levelId)
	{
		GameProgressData saveData = SaveData;
		return saveData.GetLevelProgress(_levelId);
	}

	public GameProgressData.SwitchState GetSwitchState(int _switchId)
	{
		GameProgressData saveData = SaveData;
		return saveData.GetSwitchState(_switchId);
	}

	public GameProgressData.TeleportalState GetTeleportalState(SceneDirectoryData.World _world)
	{
		GameProgressData saveData = SaveData;
		return saveData.GetTeleportalState(_world);
	}

	public SceneDirectoryData GetSceneDirectory()
	{
		return m_sceneDirectory;
	}

	public AvatarDirectoryData GetAvatarDirectory()
	{
		return m_avatarDirectory;
	}

	public void SetLastLevelEntered(int _levelIndex)
	{
		SaveData.LastLevelEntered = _levelIndex;
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			m_localSaveData.LastLevelEntered = _levelIndex;
		}
	}

	public void RecordLevelScore(HighScores.Score score)
	{
		GameProgressData.LevelProgress levelProgress = SaveableData.GetLevelProgress(score.iLevelID);
		GameUtils.GetGameSession().HighScoreRepository.LevelProgress(score);
		levelProgress.HighScore = Mathf.Max(levelProgress.HighScore, score.iHighScore);
		levelProgress.SurvivalModeTime = Mathf.Max(levelProgress.SurvivalModeTime, score.iSurvivalModeTime);
	}

	public void RecordLevelProgress(int _levelIndex, int _starRating, ref UnlockData[] _unlocks)
	{
		bool flag = _starRating > 0 || !m_sceneDirectory.Scenes[_levelIndex].HasScoreBoundaries;
		if (ClientGameSetup.Mode == GameMode.Campaign)
		{
			ApplyLevelProgress(SaveData, _levelIndex, _starRating, flag);
			if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
			{
				ApplyLevelProgress(m_localSaveData, _levelIndex, _starRating, flag);
			}
			_unlocks = FindNewUnlocks();
			for (int i = 0; i < _unlocks.Length; i++)
			{
				_unlocks[i].Unlock(this);
			}
			if (CanUnlockNewGamePlus(SaveData))
			{
				UnlockNewGamePlus(SaveData);
			}
		}
		if (!flag)
		{
			return;
		}
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_sceneDirectory.Scenes[_levelIndex];
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		if (overcookedAchievementManager != null && ClientGameSetup.Mode == GameMode.Campaign && sceneDirectoryEntry.World != SceneDirectoryData.World.Invalid)
		{
			if (sceneDirectoryEntry.World == SceneDirectoryData.World.Tutorial)
			{
				overcookedAchievementManager.AddIDStat(50, 1, ControlPadInput.PadNum.One);
			}
			else if (sceneDirectoryEntry.World >= SceneDirectoryData.World.One && sceneDirectoryEntry.World < SceneDirectoryData.World.Seven)
			{
				overcookedAchievementManager.AddIDStat(23, _levelIndex, ControlPadInput.PadNum.One);
			}
			int num = 3;
			if (_starRating >= num)
			{
				overcookedAchievementManager.AddIDStat((int)(50 + sceneDirectoryEntry.World), _levelIndex, ControlPadInput.PadNum.One);
			}
		}
	}

	private void ApplyLevelProgress(GameProgressData _saveData, int _levelIndex, int _starRating, bool _complete)
	{
		GameProgressData.LevelProgress levelProgress = _saveData.GetLevelProgress(_levelIndex);
		if (levelProgress == null || levelProgress.LevelId == -1)
		{
			levelProgress = new GameProgressData.LevelProgress();
			levelProgress.LevelId = _levelIndex;
			ArrayUtils.PushBack(ref _saveData.Levels, levelProgress);
		}
		levelProgress.Completed |= _complete;
		if (_complete)
		{
			levelProgress.Purchased = true;
			levelProgress.Revealed = true;
		}
		int num = _starRating;
		if (num > 3 && !levelProgress.NGPEnabled)
		{
			num = 3;
		}
		levelProgress.ScoreStars = Mathf.Max(levelProgress.ScoreStars, num);
		ObjectivesManager objectivesManager = GameUtils.RequestManager<ObjectivesManager>();
		if (!(objectivesManager != null))
		{
			return;
		}
		if (objectivesManager.HasObjectives())
		{
			if (objectivesManager.AllObjectivesComplete())
			{
				levelProgress.ObjectivesCompleted = true;
			}
		}
		else
		{
			levelProgress.ObjectivesCompleted = true;
		}
	}

	public void RecordSwitchRevealed(int _switchId)
	{
		ApplySwitchRevealed(SaveData, _switchId);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			ApplySwitchRevealed(m_localSaveData, _switchId);
		}
		if (ConnectionStatus.IsHost())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.FillShownMetaDialogStatus();
			ServerMessenger.GameProgressData(SaveData, gameSession.m_shownMetaDialogs);
		}
	}

	private void ApplySwitchRevealed(GameProgressData _saveData, int _switchId)
	{
		GameProgressData.SwitchState switchState = _saveData.GetSwitchState(_switchId);
		if (switchState == null || switchState.SwitchId == -1)
		{
			switchState = new GameProgressData.SwitchState();
			switchState.SwitchId = _switchId;
			ArrayUtils.PushBack(ref _saveData.Switches, switchState);
		}
	}

	public void RecordSwitchActivated(int _switchId)
	{
		ApplySwitchActivated(SaveData, _switchId);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			ApplySwitchActivated(m_localSaveData, _switchId);
		}
		if (ConnectionStatus.IsHost())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.FillShownMetaDialogStatus();
			ServerMessenger.GameProgressData(SaveData, gameSession.m_shownMetaDialogs);
		}
	}

	private void ApplySwitchActivated(GameProgressData _saveData, int _switchId)
	{
		GameProgressData.SwitchState switchState = _saveData.GetSwitchState(_switchId);
		if (switchState == null || switchState.SwitchId == -1)
		{
			switchState = new GameProgressData.SwitchState();
			switchState.SwitchId = _switchId;
			ArrayUtils.PushBack(ref _saveData.Switches, switchState);
		}
		switchState.Activated = true;
	}

	public void RecordTeleportalRevealed(SceneDirectoryData.World _world)
	{
		ApplyTeleportalRevealed(SaveData, _world);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			ApplyTeleportalRevealed(m_localSaveData, _world);
		}
		if (ConnectionStatus.IsHost())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.FillShownMetaDialogStatus();
			ServerMessenger.GameProgressData(SaveData, gameSession.m_shownMetaDialogs);
		}
	}

	private void ApplyTeleportalRevealed(GameProgressData _saveData, SceneDirectoryData.World _world)
	{
		GameProgressData.TeleportalState teleportalState = _saveData.GetTeleportalState(_world);
		if (teleportalState == null || teleportalState.World == SceneDirectoryData.World.COUNT)
		{
			teleportalState = new GameProgressData.TeleportalState();
			teleportalState.World = _world;
			ArrayUtils.PushBack(ref _saveData.Teleportals, teleportalState);
		}
	}

	public int GetStarTotal()
	{
		GameProgressData saveData = SaveData;
		return saveData.GetStarTotal();
	}

	public int GetCompletedLevelsCount()
	{
		GameProgressData saveData = SaveData;
		int num = 0;
		for (int i = 0; i < saveData.Levels.Length; i++)
		{
			if (saveData.Levels[i].Completed)
			{
				num++;
			}
		}
		return num;
	}

	public void GetLocalScores(ref HighScores highScores)
	{
		highScores.Scores.Clear();
		GameProgressData localSaveData = m_localSaveData;
		for (int i = 0; i < localSaveData.Levels.Length; i++)
		{
			GameProgressData.LevelProgress levelProgress = localSaveData.Levels[i];
			int iHighScore = ((levelProgress.HighScore == int.MinValue) ? 65535 : levelProgress.HighScore);
			highScores.Scores.Add(new HighScores.Score
			{
				iLevelID = levelProgress.LevelId,
				iHighScore = iHighScore,
				iSurvivalModeTime = levelProgress.SurvivalModeTime
			});
		}
	}

	public void Load(GameProgressData _data)
	{
		m_localSaveData = _data;
		m_localSaveData.FillOut(m_sceneDirectory);
	}

	public void LoadFromNetwork(GameProgressData _data)
	{
		m_remoteSaveData = _data;
	}

	public bool Load(byte[] _data)
	{
		GameProgressData gameProgressData = new GameProgressData();
		if (!gameProgressData.ByteLoad(_data))
		{
			return false;
		}
		Load(gameProgressData);
		return true;
	}

	private UnlockData[] FindNewUnlocks()
	{
		GameDebugConfig debugConfig = GameUtils.GetDebugConfig();
		int totalStars = GetStarTotal();
		Predicate<UnlockData> match = delegate(UnlockData _unlockData)
		{
			if (_unlockData.Type == UnlockData.UnlockType.CompetitiveLevel && debugConfig.m_supressCompetitveMode)
			{
				return false;
			}
			if (_unlockData.IsUnlocked(this))
			{
				return false;
			}
			UnlockData.Condition condition = _unlockData.Requirement;
			switch (condition.Type)
			{
			case UnlockData.Condition.ConditionType.LevelComplete:
				if (condition.RequiredLevel != -1)
				{
					if (!SaveableData.IsLevelComplete(condition.RequiredLevel))
					{
						return false;
					}
					if (condition.CheckPreviousLevels && !SaveableData.IsTrueForLevelsRecursive(condition.RequiredLevel, m_sceneDirectory, SaveableData.IsLevelComplete))
					{
						return false;
					}
				}
				break;
			case UnlockData.Condition.ConditionType.LevelStars:
				if (condition.RequiredLevel != -1)
				{
					if (!SaveableData.HasStarsForLevel(condition.RequiredLevel, condition.RequiredStars, m_sceneDirectory))
					{
						return false;
					}
					if (condition.CheckPreviousLevels && !SaveableData.IsTrueForLevelsRecursive(condition.RequiredLevel, m_sceneDirectory, (int x) => SaveableData.HasStarsForLevel(x, condition.RequiredStars, m_sceneDirectory)))
					{
						return false;
					}
				}
				break;
			case UnlockData.Condition.ConditionType.TotalStars:
				if (totalStars < condition.RequiredStars)
				{
					return false;
				}
				break;
			}
			return true;
		};
		return m_unlockData.FindAll(match);
	}

	public bool CanUnlockNewGamePlus(GameProgressData _saveData)
	{
		for (int i = 0; i < m_levelChainEnds.Length; i++)
		{
			if (CanUnlockNewGamePlusForChainEnd(_saveData, m_levelChainEnds[i]))
			{
				return true;
			}
		}
		return false;
	}

	private bool CanUnlockNewGamePlusForChainEnd(GameProgressData _saveData, int _levelID)
	{
		GameProgressData.LevelProgress levelProgress = _saveData.GetLevelProgress(_levelID);
		if (!levelProgress.NGPEnabled && _saveData.IsLevelComplete(_levelID) && _saveData.IsLevelUnlocked(_levelID, false))
		{
			return true;
		}
		return false;
	}

	public void UnlockNewGamePlus(GameProgressData _saveData)
	{
		HashSet<int> _visited = new HashSet<int>();
		for (int i = 0; i < m_levelChainEnds.Length; i++)
		{
			int levelID = m_levelChainEnds[i];
			if (CanUnlockNewGamePlusForChainEnd(_saveData, levelID))
			{
				UnlockNewGamePlusChain(_saveData, levelID, ref _visited);
				_visited.Clear();
			}
		}
		bool flag = true;
		for (int j = 0; j < m_levelChainEnds.Length; j++)
		{
			int id = m_levelChainEnds[j];
			GameProgressData.LevelProgress levelProgress = _saveData.GetLevelProgress(id);
			if (!levelProgress.NGPEnabled)
			{
				flag = false;
				break;
			}
		}
		_saveData.NewGamePlusEnabled |= flag;
	}

	private void UnlockNewGamePlusChain(GameProgressData _saveData, int _levelID, ref HashSet<int> _visited)
	{
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_sceneDirectory.Scenes.TryAtIndex(_levelID);
		GameProgressData.LevelProgress levelProgress = _saveData.GetLevelProgress(_levelID);
		levelProgress.NGPEnabled = true;
		_visited.Add(_levelID);
		for (int i = 0; i < m_sceneDirectory.Scenes.Length; i++)
		{
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry2 = m_sceneDirectory.Scenes[i];
			if (sceneDirectoryEntry2.PreviousEntriesToUnlock.Contains(_levelID) && sceneDirectoryEntry2.IsHidden && !_visited.Contains(i))
			{
				_saveData.GetLevelProgress(i).NGPEnabled = true;
				_visited.Add(i);
			}
		}
		int[] previousEntriesToUnlock = sceneDirectoryEntry.PreviousEntriesToUnlock;
		foreach (int num in previousEntriesToUnlock)
		{
			if (num != -1 && !_visited.Contains(num))
			{
				UnlockNewGamePlusChain(_saveData, num, ref _visited);
			}
		}
	}

	public bool HasShownNGPlusDialog(GameProgressData _saveData)
	{
		return _saveData.NewGamePlusDialogShown;
	}

	public void SetNGPlusDialogShown(GameProgressData _saveData)
	{
		_saveData.NewGamePlusDialogShown = true;
	}

	public GameProgressData GetRemoteProgress()
	{
		return m_remoteSaveData;
	}
}
