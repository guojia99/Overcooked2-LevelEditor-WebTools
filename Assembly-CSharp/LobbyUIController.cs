using System;
using System.Collections.Generic;
using UnityEngine.UI;

[ExecutionDependency(typeof(PlayerSelectCardUIController))]
public class LobbyUIController : UIControllerBase
{
	public class AvatarCardData
	{
		public PlayerGameInput PlayerInput;

		public GameSession.SelectedChefData Chef;

		public AvatarCardData(PlayerGameInput _playerInput, GameSession.SelectedChefData _chef)
		{
			PlayerInput = _playerInput;
			Chef = _chef;
		}
	}

	private ILogicalButton m_playerStartButton;

	private IPlayerManager m_playerManager;

	private CallbackVoid m_onCompleted = delegate
	{
	};

	private CallbackVoid m_onCancelled = delegate
	{
	};

	private Image m_readyButton;

	private int m_colourOptions;

	protected PlayerSelectCardUIController[] m_playerSelectCards = new PlayerSelectCardUIController[0];

	protected SceneDirectoryData.SceneDirectoryEntry m_sceneDirectoryEntry;

	protected GameProgress.GameProgressData.LevelProgress m_progressData;

	public static bool IsOpen;

	public static event VoidGeneric<bool> OpenCloseCallback;

	protected void OnEnable()
	{
		IsOpen = true;
		LobbyUIController.OpenCloseCallback(IsOpen);
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(UpdateLobbyForEngagement));
	}

	protected void OnDisable()
	{
		IsOpen = false;
		LobbyUIController.OpenCloseCallback(IsOpen);
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(UpdateLobbyForEngagement));
	}

	public virtual void SetSceneData(GameSession.GameType _gameType, SceneDirectoryData.SceneDirectoryEntry _sceneDirectoryEntry, GameProgress.GameProgressData.LevelProgress _levelProgress)
	{
		m_sceneDirectoryEntry = _sceneDirectoryEntry;
		m_progressData = _levelProgress;
	}

	public void RegisterForCompletedMessage(CallbackVoid _callback)
	{
		m_onCompleted = (CallbackVoid)Delegate.Combine(m_onCompleted, _callback);
	}

	public void RegisterForCanceledMessage(CallbackVoid _callback)
	{
		m_onCancelled = (CallbackVoid)Delegate.Combine(m_onCancelled, _callback);
	}

	public void SetInitialAvatars(Dictionary<PlayerInputLookup.Player, AvatarCardData> _cardData)
	{
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			PlayerGameInput assignedInput = m_playerSelectCards[i].GetAssignedInput();
			if (assignedInput != null && assignedInput.Pad == ControlPadInput.PadNum.One)
			{
				m_playerSelectCards[i].SetDeactivationOverride(Exit);
			}
			else
			{
				m_playerSelectCards[i].SetDeactivationOverride(False);
			}
			if (_cardData.ContainsKey((PlayerInputLookup.Player)i) && m_playerSelectCards[i].IsActive())
			{
				AvatarCardData avatarCardData = _cardData[(PlayerInputLookup.Player)i];
				m_playerSelectCards[i].SetChefSelection(avatarCardData.Chef);
			}
		}
	}

	public Dictionary<PlayerInputLookup.Player, AvatarCardData> GetSelectedAvatars()
	{
		Dictionary<PlayerInputLookup.Player, AvatarCardData> dictionary = new Dictionary<PlayerInputLookup.Player, AvatarCardData>();
		int num = 0;
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			GameSession.SelectedChefData fullSelection = m_playerSelectCards[i].GetFullSelection();
			PlayerGameInput assignedInput = m_playerSelectCards[i].GetAssignedInput();
			if (fullSelection != null)
			{
				dictionary.Add((PlayerInputLookup.Player)num, new AvatarCardData(assignedInput, fullSelection));
				num++;
			}
		}
		return dictionary;
	}

	private PlayerGameInput GetInputForID(int _id)
	{
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		return baseInputConfig.GetInputData((PlayerInputLookup.Player)_id);
	}

	protected virtual void Awake()
	{
		if (GameUtils.GetGameSession().TypeSettings.Type == GameSession.GameType.Competitive)
		{
			m_colourOptions = 2;
		}
		else
		{
			m_colourOptions = 0;
		}
		m_playerSelectCards = base.gameObject.RequestComponentsRecursive<PlayerSelectCardUIController>();
		Array.Sort(m_playerSelectCards, (PlayerSelectCardUIController x, PlayerSelectCardUIController y) => x.GetActualPlayer().CompareTo(y.GetActualPlayer()));
		for (int num = 0; num < 4; num++)
		{
		}
		m_readyButton = base.transform.FindChildRecursive("ReadyButton").gameObject.RequireComponent<Image>();
		m_readyButton.gameObject.SetActive(false);
		m_playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		UpdateLobbyForEngagement();
		m_playerManager.EngagementChangeCallback += OnEngagementChange;
		m_playerStartButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.UISelect, PlayerInputLookup.Player.One);
	}

	private void OnDestroy()
	{
		m_playerManager.EngagementChangeCallback -= OnEngagementChange;
	}

	private void OnEngagementChange(EngagementSlot _slot, GamepadUser _prev, GamepadUser _new)
	{
		if (_new == null && _prev != null)
		{
			DeactivateEntriesForPad((ControlPadInput.PadNum)_slot);
		}
		UpdateLobbyForEngagement();
	}

	private void DeactivateEntriesForPad(ControlPadInput.PadNum _pad)
	{
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			if (m_playerSelectCards[i].IsActive())
			{
				PlayerGameInput assignedInput = m_playerSelectCards[i].GetAssignedInput();
				if (assignedInput.Pad == _pad)
				{
					m_playerSelectCards[i].Deactivate();
				}
			}
		}
	}

	public static PlayerGameInput GetEngagedPlayerGameInput(IPlayerManager _iPlayerManager, int _index)
	{
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		if (baseInputConfig != null)
		{
			GameInputConfig.ConfigEntry configEntry = baseInputConfig.m_playerConfigs.TryAtIndex(_index, null);
			if (configEntry != null)
			{
				EngagementSlot pad = (EngagementSlot)configEntry.Pad;
				if (_iPlayerManager.GetUser(pad) != null)
				{
					return new PlayerGameInput(configEntry.Pad, configEntry.Side, configEntry.AmbiControlsMapping);
				}
			}
		}
		return null;
	}

	private bool Matching(PlayerGameInput _c1, PlayerGameInput _c2)
	{
		if (_c1 != null && _c2 != null)
		{
			return _c1.Pad == _c2.Pad && _c1.Side == _c2.Side;
		}
		return _c1 == _c2;
	}

	private void UpdateLobbyForEngagement()
	{
		AvatarCardData[] array = new AvatarCardData[m_playerSelectCards.Length];
		PlayerSelectCardUIController.State[] array2 = new PlayerSelectCardUIController.State[array.Length];
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			if (m_playerSelectCards[i].IsActive())
			{
				array[i] = new AvatarCardData(m_playerSelectCards[i].GetAssignedInput(), m_playerSelectCards[i].GetCurrentSelection());
				array2[i] = m_playerSelectCards[i].GetState();
			}
			else
			{
				array[i] = null;
			}
		}
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		for (int j = 0; j < m_playerSelectCards.Length; j++)
		{
			PlayerGameInput newInputPlayer = GetEngagedPlayerGameInput(m_playerManager, j);
			if (m_playerSelectCards[j].IsActive() && !Matching(m_playerSelectCards[j].GetAssignedInput(), newInputPlayer))
			{
				m_playerSelectCards[j].Deactivate();
			}
			if (newInputPlayer != null && !m_playerSelectCards[j].IsActive())
			{
				int num = array.FindIndex_Predicate((AvatarCardData x) => x != null && Matching(x.PlayerInput, newInputPlayer));
				if (num == -1)
				{
					m_playerSelectCards[j].Activate(newInputPlayer, m_colourOptions);
					continue;
				}
				AvatarCardData avatarCardData = array[num];
				m_playerSelectCards[j].Activate(newInputPlayer, m_colourOptions, avatarCardData.Chef, array2[num]);
			}
		}
	}

	private bool IsReadyForStart()
	{
		GameSession.SelectedChefData[] chefData;
		if (!AllCardsReady(out chefData))
		{
			return false;
		}
		if (GameUtils.GetGameSession().TypeSettings.Type == GameSession.GameType.Competitive)
		{
			if (chefData.Length != 2 && chefData.Length != 4)
			{
				return false;
			}
			ChefColourData[] colourData = chefData.ConvertAll((GameSession.SelectedChefData x) => x.Colour);
			int[] array = colourData.ConvertAll((ChefColourData x) => colourData.FindAll(x.Equals).Length);
			return array.FindIndex_Predicate((int x) => x != chefData.Length / 2) == -1;
		}
		return m_sceneDirectoryEntry.GetSceneVarient(chefData.Length) != null;
	}

	protected virtual void Update()
	{
		bool flag = IsReadyForStart();
		if (flag && !m_readyButton.isActiveAndEnabled)
		{
			m_playerStartButton.ClaimPressEvent();
			m_playerStartButton.ClaimReleaseEvent();
		}
		m_readyButton.gameObject.SetActive(flag);
		if (m_playerStartButton.JustPressed() && m_readyButton.isActiveAndEnabled)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, base.gameObject.layer);
			m_onCompleted();
		}
	}

	private bool False()
	{
		return false;
	}

	private bool Exit()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIBack, base.gameObject.layer);
		m_onCancelled();
		return true;
	}

	private bool IsAssignedToCard(PlayerGameInput _player)
	{
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			PlayerSelectCardUIController playerSelectCardUIController = m_playerSelectCards[i];
			PlayerGameInput assignedInput = playerSelectCardUIController.GetAssignedInput();
			if (assignedInput != null && assignedInput.Pad == _player.Pad)
			{
				if (assignedInput.Side == PadSide.Both)
				{
					return true;
				}
				if (assignedInput.Side == _player.Side)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool AllCardsReady(out GameSession.SelectedChefData[] o_chefData)
	{
		o_chefData = new GameSession.SelectedChefData[0];
		for (int i = 0; i < m_playerSelectCards.Length; i++)
		{
			if (m_playerSelectCards[i].GetAssignedInput() != null)
			{
				GameSession.SelectedChefData fullSelection = m_playerSelectCards[i].GetFullSelection();
				if (fullSelection == null)
				{
					return false;
				}
				ArrayUtils.PushBack(ref o_chefData, fullSelection);
			}
		}
		return true;
	}

	static LobbyUIController()
	{
		LobbyUIController.OpenCloseCallback = delegate
		{
		};
		IsOpen = false;
	}
}
