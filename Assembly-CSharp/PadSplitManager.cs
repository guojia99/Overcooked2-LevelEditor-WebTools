using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PadSplitManager : Manager
{
	private class MenuSession
	{
		public MultiChefUIController MultiChefUI;

		public MenuSession(MultiChefUIController _multiChefUI)
		{
			MultiChefUI = _multiChefUI;
		}
	}

	[SerializeField]
	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_sidedMappingData;

	[SerializeField]
	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_unsidedMappingData;

	[SerializeField]
	private Animator m_padSplitPromptUI;

	[SerializeField]
	private MultiChefUIController m_multiChefUIPrefab;

	private GameDebugConfig m_gameDebugConfig;

	private MenuSession m_menuSession;

	private PlayerManager m_playerManager;

	private MetaGameProgress m_metaGame;

	private ILogicalButton[] m_padSplitOpenButtons;

	private IEnumerator m_padSplitCoroutine;

	private static int m_Open = Animator.StringToHash("Open");

	public bool IsUIOpen()
	{
		return m_padSplitCoroutine != null;
	}

	private void Awake()
	{
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		m_metaGame = GameUtils.GetMetaGameProgress();
		m_gameDebugConfig = GameUtils.GetDebugConfig();
	}

	private void Start()
	{
		m_padSplitOpenButtons = new ILogicalButton[4];
		for (int i = 0; i < m_padSplitOpenButtons.Length; i++)
		{
			ControlPadInput.PadNum pad = (ControlPadInput.PadNum)i;
			PlayerGameInput playerGameInput = new PlayerGameInput(pad, PadSide.Both, m_unsidedMappingData);
			ILogicalButton fixedButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.Curse, playerGameInput);
			m_padSplitOpenButtons[i] = fixedButton;
		}
	}

	private bool InWorldMap(string _sceneName)
	{
		if (_sceneName.Contains("_Map"))
		{
			return true;
		}
		GameSession gameSession = GameUtils.GetGameSession();
		return gameSession != null && _sceneName.Equals(gameSession.TypeSettings.WorldMapScene);
	}

	private bool CanOpenPadSplit()
	{
		return false;
	}

	private bool ShouldClosePadSplit()
	{
		string sceneName = SceneManager.GetActiveScene().name;
		return AnyOpenPressed() || !InWorldMap(sceneName);
	}

	private void Update()
	{
		if (m_padSplitCoroutine == null)
		{
			bool flag = CanOpenPadSplit();
			m_padSplitPromptUI.SetBool(m_Open, flag);
			if (flag && AnyOpenPressed())
			{
				m_padSplitCoroutine = RunPadSplit();
			}
		}
		else
		{
			m_padSplitPromptUI.SetBool(m_Open, false);
			m_padSplitCoroutine.MoveNext();
			if (ShouldClosePadSplit())
			{
				EndPadSplit();
			}
		}
	}

	private bool AnyOpenPressed()
	{
		for (int i = 0; i < 4; i++)
		{
			ILogicalButton logicalButton = m_padSplitOpenButtons[i];
			if (logicalButton.JustPressed())
			{
				EngagementSlot slot = (EngagementSlot)i;
				GamepadUser user = m_playerManager.GetUser(slot);
				if (user != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	private IEnumerator RunPadSplit()
	{
		TimeManager timeManager = GameUtils.RequireManager<TimeManager>();
		timeManager.SetPaused(TimeManager.PauseLayer.Main, true, this);
		GameObject multiChefUI = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_multiChefUIPrefab.gameObject);
		MultiChefUIController multiChefUIController = multiChefUI.RequireComponent<MultiChefUIController>();
		m_menuSession = new MenuSession(multiChefUIController);
		multiChefUIController.OnExitCallback += EndPadSplit;
		while (true)
		{
			yield return null;
		}
	}

	private void EndPadSplit()
	{
		if (m_menuSession != null)
		{
			if (m_menuSession.MultiChefUI != null)
			{
				Object.Destroy(m_menuSession.MultiChefUI.gameObject);
			}
			m_menuSession = null;
		}
		TimeManager timeManager = GameUtils.RequireManager<TimeManager>();
		timeManager.SetPaused(TimeManager.PauseLayer.Main, false, this);
		m_padSplitCoroutine = null;
		GameUtils.RequireManager<SaveManager>().SaveMetaProgress();
	}

	private void OnEngagementChanged(EngagementSlot _slot, GamepadUser _userBefore, GamepadUser _userAfter)
	{
		if (_userAfter == null && _userBefore != null && !_userBefore.StickyEngagement)
		{
			ClearSplitForSlot(_slot);
		}
	}

	private void ClearSplitForSlot(EngagementSlot _slot)
	{
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		GameInputConfig.ConfigEntry[] collection = baseInputConfig.m_playerConfigs.AllRemoved_Predicate((GameInputConfig.ConfigEntry x) => x.Pad == (ControlPadInput.PadNum)_slot);
		List<GameInputConfig.ConfigEntry> list = new List<GameInputConfig.ConfigEntry>(collection);
		FixupConfigList(list, m_unsidedMappingData);
		GameInputConfig baseInputConfig2 = new GameInputConfig(list.ToArray());
		PlayerInputLookup.SetBaseInputConfig(baseInputConfig2);
	}

	public static void FixupConfigList(List<GameInputConfig.ConfigEntry> _configList, AmbiControlsMappingData _unsidedMappingData)
	{
		int i;
		for (i = 0; i < 4; i++)
		{
			if (_configList.FindIndex((GameInputConfig.ConfigEntry x) => x.Player == (PlayerInputLookup.Player)i) != -1)
			{
				continue;
			}
			bool flag = false;
			int j;
			for (j = 0; j < 4; j++)
			{
				if (_configList.FindIndex((GameInputConfig.ConfigEntry x) => x.Pad == (ControlPadInput.PadNum)j) == -1)
				{
					PlayerInputLookup.Player player = (PlayerInputLookup.Player)i;
					ControlPadInput.PadNum pad = (ControlPadInput.PadNum)j;
					User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
					_configList.Add(new GameInputConfig.ConfigEntry(player, pad, PadSide.Both, s_LocalMachineId, _unsidedMappingData));
					flag = true;
					break;
				}
			}
		}
		int p;
		for (p = 0; p < 4; p++)
		{
			List<GameInputConfig.ConfigEntry> list = _configList.FindAll((GameInputConfig.ConfigEntry x) => x.Pad == (ControlPadInput.PadNum)p);
			if (list.Count == 1)
			{
				list[0].UIHandedness = list[0].Side;
			}
		}
	}
}
