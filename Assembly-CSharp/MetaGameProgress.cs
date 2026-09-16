using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaGameProgress : MonoBehaviour, IByteSerialization
{
	[Serializable]
	private class DLCSerializedAvatarDirectoryData : DLCSerializedData<AvatarDirectoryData>
	{
	}

	public enum MetaDialogType
	{
		PracticeMode = 0,
		HordeMode = 1,
		COUNT = 2
	}

	[SerializeField]
	private DLCSerializedAvatarDirectoryData m_AvatarDirectoryData = new DLCSerializedAvatarDirectoryData();

	private AvatarDirectoryData[] m_allAvatarDirectories = new AvatarDirectoryData[0];

	private AvatarDirectoryData m_combinedAvatarDirectory;

	[SerializeField]
	[ArrayIndex("m_AvatarDirectoryData.m_Data", "Avatars", SerializationUtils.RootType.Top)]
	private int[] m_initialAvatarUnlocks = new int[0];

	private GlobalSave m_saveData = new GlobalSave();

	private const string c_versionTag = "VERSION";

	[SerializeField]
	private OptionsData m_optionsData = new OptionsData();

	private DLCManager m_dlcManager;

	private OvercookedAchievementManager m_achievementManager;

	private const string AVATAR_UNLOCKS_KEY = "AvatarUnlocks";

	private const string LAST_SAVE_USED = "LastSaveUsed";

	private const string LAST_THEME_PLAYED = "LastThemePlayed";

	private const string META_DIALOG_SHOWN_TAG = "MetaDialogShown_";

	private const string DLC_SEEN_TAG = "DLC_";

	public uint SaveVersion
	{
		get
		{
			return 1u;
		}
	}

	public GlobalSave SaveData
	{
		get
		{
			return m_saveData;
		}
	}

	public OptionsData AccessOptionsData
	{
		get
		{
			return m_optionsData;
		}
	}

	public AvatarDirectoryData AvatarDirectory
	{
		get
		{
			return m_combinedAvatarDirectory;
		}
	}

	public int ByteSaveSize
	{
		get
		{
			return ByteSave().Length;
		}
	}

	private void Awake()
	{
		m_dlcManager = GameUtils.RequireManager<DLCManager>();
		m_achievementManager = GameUtils.RequireManager<OvercookedAchievementManager>();
		m_optionsData.OnAwake();
		m_saveData.Set("AvatarUnlocks", m_initialAvatarUnlocks);
		m_allAvatarDirectories = CollectAllAvatarDirectories();
		m_combinedAvatarDirectory = CombineAvatarDirectories(m_allAvatarDirectories);
	}

	private void Update()
	{
		m_optionsData.Update();
	}

	private AvatarDirectoryData[] CollectAllAvatarDirectories()
	{
		return m_AvatarDirectoryData.AllData.AllRemoved_Predicate((AvatarDirectoryData x) => x == null);
	}

	private AvatarDirectoryData CombineAvatarDirectories(AvatarDirectoryData[] avatarDirectories)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < avatarDirectories.Length; i++)
		{
			num += avatarDirectories[i].Avatars.Length;
			num2 += avatarDirectories[i].Colours.Length;
		}
		AvatarDirectoryData avatarDirectoryData = ScriptableObject.CreateInstance<AvatarDirectoryData>();
		avatarDirectoryData.Avatars = new ChefAvatarData[num];
		avatarDirectoryData.Colours = new ChefColourData[num2];
		int num3 = 0;
		for (int j = 0; j < avatarDirectories.Length; j++)
		{
			for (int k = 0; k < avatarDirectories[j].Avatars.Length; k++)
			{
				avatarDirectoryData.Avatars[num3 + k] = avatarDirectories[j].Avatars[k];
			}
			num3 += avatarDirectories[j].Avatars.Length;
		}
		List<ChefColourData> list = new List<ChefColourData>(5);
		foreach (AvatarDirectoryData avatarDirectoryData2 in avatarDirectories)
		{
			for (int m = 0; m < avatarDirectoryData2.Colours.Length; m++)
			{
				ChefColourData item = avatarDirectoryData2.Colours[m];
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		avatarDirectoryData.Colours = list.ToArray();
		return avatarDirectoryData;
	}

	public void SetLastPlayedTheme(SceneDirectoryData.LevelTheme _theme)
	{
		m_saveData.Set("LastThemePlayed", (int)_theme);
	}

	public SceneDirectoryData.LevelTheme GetLastPlayedTheme()
	{
		int value;
		m_saveData.Get("LastThemePlayed", out value, -1);
		if (value > 22)
		{
			return SceneDirectoryData.LevelTheme.Null;
		}
		return (SceneDirectoryData.LevelTheme)value;
	}

	public void SetMetaDialogShown(MetaDialogType _type)
	{
		m_saveData.Set("MetaDialogShown_" + (int)_type, true);
	}

	public bool HasShownMetaDialog(MetaDialogType _type)
	{
		bool value;
		m_saveData.Get("MetaDialogShown_" + (int)_type, out value, false);
		return value;
	}

	public void SetDLCSeen(DLCFrontendData _data)
	{
		m_saveData.Set("DLC_" + _data.m_DLCID, true);
	}

	public bool GetDLCSeen(DLCFrontendData _data)
	{
		bool value;
		m_saveData.Get("DLC_" + _data.m_DLCID, out value, false);
		return value;
	}

	private static string GetLastSaveSlotKey(int _dlcNum)
	{
		string result = "LastSaveUsed";
		if (_dlcNum != -1)
		{
			_dlcNum = Mathf.Max(0, _dlcNum);
			result = "LastSaveUsed_DLC" + _dlcNum;
		}
		return result;
	}

	public void SetLastSaveSlot(int _dlcNum, int _slotNum)
	{
		m_saveData.Set(GetLastSaveSlotKey(_dlcNum), _slotNum);
	}

	public int GetLastSaveSlot(int _dlcNum)
	{
		int value;
		m_saveData.Get(GetLastSaveSlotKey(_dlcNum), out value, -1);
		return value;
	}

	public ChefAvatarData[] GetUnlockedAvatars()
	{
		List<ChefAvatarData> list = new List<ChefAvatarData>();
		int[] value;
		m_saveData.Get("AvatarUnlocks", out value, new int[0]);
		for (int i = 0; i < m_allAvatarDirectories.Length; i++)
		{
			AvatarDirectoryData avatarDirectoryData = m_allAvatarDirectories[i];
			for (int j = 0; j < avatarDirectoryData.Avatars.Length; j++)
			{
				ChefAvatarData chefAvatarData = avatarDirectoryData.Avatars[j];
				if (chefAvatarData != null && chefAvatarData.ActuallyAllowed && chefAvatarData.IsAvailableOnThisPlatform())
				{
					bool flag = false;
					if (chefAvatarData.ForDlc != null)
					{
						flag = m_dlcManager.IsDLCAvailable(chefAvatarData.ForDlc) && (chefAvatarData.ForDlc.m_type == DLCType.Avatars || chefAvatarData.ForDlc.m_InstallUnlocksAvatars);
					}
					int storageId = GameProgress.UnlockData.AvatarDataType.GetStorageId(avatarDirectoryData, j);
					if (value.Contains(storageId) || flag || (DebugManager.Instance != null && DebugManager.Instance.GetOption("Unlock all chefs")))
					{
						list.Add(chefAvatarData);
					}
				}
			}
		}
		return list.ToArray();
	}

	public bool IsAvatarUnlocked(int _id)
	{
		int[] value;
		m_saveData.Get("AvatarUnlocks", out value, new int[0]);
		return value.Contains(_id);
	}

	public void UnlockAvatar(int _id)
	{
		int[] value;
		m_saveData.Get("AvatarUnlocks", out value, new int[0]);
		value = value.Union(new int[1] { _id });
		m_saveData.Set("AvatarUnlocks", value);
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		if (overcookedAchievementManager != null)
		{
			overcookedAchievementManager.AddIDStat(20, _id, ControlPadInput.PadNum.One);
		}
	}

	public static bool Validate(byte[] _bytes)
	{
		return new GlobalSave().ByteLoad(_bytes);
	}

	public IOption GetOption(OptionsData.OptionType _type)
	{
		return m_optionsData.GetOption(_type);
	}

	public int GetOptionsInCategory(OptionsData.Categories _category)
	{
		return m_optionsData.GetOptionsInCategory(_category);
	}

	public IEnumerable<IOption> IterateOverCategory(OptionsData.Categories _category)
	{
		return m_optionsData.IterateOverCategory(_category);
	}

	public void ConsoleReset()
	{
		m_optionsData.Unload();
	}

	public byte[] ByteSave()
	{
		m_optionsData.AddToSave();
		PCPadInputProvider.SaveBindings(m_saveData);
		m_saveData.Set("VERSION", SaveVersion);
		return m_saveData.ByteSave();
	}

	public bool ByteLoad(byte[] _data)
	{
		if (!m_saveData.ByteLoad(_data))
		{
			return false;
		}
		int value;
		m_saveData.Get("VERSION", out value, 1);
		if (value != (int)SaveVersion)
		{
			return false;
		}
		m_optionsData.LoadFromSave();
		PCPadInputProvider.LoadBindings(m_saveData);
		m_achievementManager.Init();
		return true;
	}
}
