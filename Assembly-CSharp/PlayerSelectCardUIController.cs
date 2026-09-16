using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectCardUIController : UIControllerBase
{
	public enum State
	{
		PressStart = 0,
		SelectChef = 1,
		SelectColour = 2,
		Confirmed = 3
	}

	[SerializeField]
	private PlayerInputLookup.Player m_actualPlayer;

	[SerializeField]
	[AssignChild("Chef", Editorbility.NonEditable)]
	private Image m_chef;

	[SerializeField]
	[AssignChild("ChefArrows", Editorbility.NonEditable)]
	private Image m_chefArrows;

	[SerializeField]
	[AssignChild("ChefAButton", Editorbility.NonEditable)]
	private Image m_chefAButton;

	[SerializeField]
	[AssignChild("Image", Editorbility.NonEditable)]
	private GameObject m_backgroundImage;

	[SerializeField]
	[AssignChild("ColourArrows", Editorbility.NonEditable)]
	private Image m_colourArrows;

	[SerializeField]
	[AssignChild("ColourAButton", Editorbility.NonEditable)]
	private Image m_colourAButton;

	[SerializeField]
	[AssignChild("PressStart", Editorbility.NonEditable)]
	private GameObject m_pressStart;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private ButtonImage[] m_buttonImages;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private PlayerGameInput m_assignedInput;

	private AvatarDirectoryData m_avatarDirectory;

	private Generic<bool> m_deactivationOverride;

	private PadSplitManager m_padSplitManager;

	private ILogicalButton m_leftButton;

	private ILogicalButton m_rightButton;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_cancelButton;

	private State m_state;

	private ChefAvatarData[] m_avatars;

	private int m_colourCount;

	private int m_chefSelection = -1;

	private int m_colourSelection = -1;

	private bool m_skipColourSelection;

	private void OnDestroy()
	{
		m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, true);
	}

	public State GetState()
	{
		return m_state;
	}

	public void SetState(State _state)
	{
		switch (_state)
		{
		case State.PressStart:
			m_chef.enabled = false;
			m_chefArrows.enabled = false;
			m_chefAButton.gameObject.SetActive(false);
			m_colourArrows.enabled = false;
			m_colourAButton.gameObject.SetActive(false);
			m_backgroundImage.SetActive(false);
			m_pressStart.SetActive(true);
			m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, true);
			break;
		case State.SelectChef:
			m_chef.enabled = true;
			m_chefArrows.enabled = true;
			m_chefAButton.gameObject.SetActive(true);
			m_colourArrows.enabled = false;
			m_colourAButton.gameObject.SetActive(false);
			m_backgroundImage.SetActive(m_skipColourSelection);
			m_pressStart.SetActive(false);
			m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, true);
			break;
		case State.SelectColour:
			m_chef.enabled = true;
			m_chefArrows.enabled = false;
			m_chefAButton.gameObject.SetActive(false);
			m_colourArrows.enabled = true;
			m_colourAButton.gameObject.SetActive(true);
			m_backgroundImage.SetActive(true);
			m_pressStart.SetActive(false);
			m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, false);
			break;
		case State.Confirmed:
			m_chef.enabled = true;
			m_chefArrows.enabled = false;
			m_chefAButton.gameObject.SetActive(false);
			m_colourArrows.enabled = false;
			m_colourAButton.gameObject.SetActive(false);
			m_backgroundImage.SetActive(true);
			m_pressStart.SetActive(false);
			m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, false);
			break;
		}
		m_state = _state;
	}

	public void SetDeactivationOverride(Generic<bool> _callback)
	{
		m_deactivationOverride = _callback;
	}

	public bool IsActive()
	{
		return m_assignedInput != null;
	}

	private bool IsKeyBoard(PlayerGameInput _input)
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser((EngagementSlot)_input.Pad);
		if (user != null)
		{
			return user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
		}
		return false;
	}

	public void Activate(PlayerGameInput _input, int _colourOptions, GameSession.SelectedChefData _default = null, State _state = State.SelectChef)
	{
		m_assignedInput = _input;
		m_leftButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UILeft, _input);
		m_rightButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UIRight, _input);
		m_selectButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UISelect, _input);
		m_cancelButton = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UICancel, _input);
		ControlPadInput.Button? controlPadButton = PlayerButtonImage.GetControlPadButton<ControlPadInput.Button>(m_selectButton, ControllerIconLookup.DeviceContext.Pad);
		bool flag = IsKeyBoard(_input);
		for (int i = 0; i < m_buttonImages.Length; i++)
		{
			if (controlPadButton.HasValue)
			{
				m_buttonImages[i].enabled = true;
				m_buttonImages[i].SetData(controlPadButton.Value, (!flag) ? ControllerIconLookup.DeviceContext.Pad : ControllerIconLookup.DeviceContext.Keyboard);
			}
			else
			{
				m_buttonImages[i].enabled = false;
			}
		}
		if (_colourOptions <= 1)
		{
			m_colourSelection = (int)m_actualPlayer;
			m_skipColourSelection = true;
			m_chefArrows.color = Color.white;
		}
		else
		{
			m_colourSelection = (int)m_actualPlayer % _colourOptions;
			m_skipColourSelection = false;
		}
		m_colourCount = _colourOptions;
		SetColourSelection(m_colourSelection);
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		m_avatars = metaGameProgress.GetUnlockedAvatars();
		if (_default != null)
		{
			int num = m_avatars.FindIndex_Predicate((ChefAvatarData x) => x == _default.Character);
			if (num != -1)
			{
				SetChefSelection(num);
			}
		}
		if (m_chefSelection == -1)
		{
			SetChefSelection(Random.Range(0, m_avatars.Length));
		}
		SetState(_state);
		base.enabled = true;
	}

	public void SetChefSelection(GameSession.SelectedChefData _data)
	{
		if (m_state == State.SelectChef)
		{
			int num = m_avatars.FindIndex_Predicate((ChefAvatarData x) => x == _data.Character);
			if (num != -1)
			{
				SetChefSelection(num);
			}
		}
	}

	public void Deactivate()
	{
		m_gamepadEngagementManager.SetCanDisconnect((EngagementSlot)m_actualPlayer, true);
		m_assignedInput = null;
		SetState(State.PressStart);
		base.enabled = false;
	}

	public GameSession.SelectedChefData GetFullSelection()
	{
		if (m_state == State.Confirmed)
		{
			return GetCurrentSelection();
		}
		return null;
	}

	public GameSession.SelectedChefData GetCurrentSelection()
	{
		if (m_avatars.Length > 0)
		{
			ChefAvatarData chefAvatarData = m_avatars[m_chefSelection];
			ChefColourData colourData = m_avatarDirectory.Colours[m_colourSelection];
			return new GameSession.SelectedChefData(chefAvatarData, colourData);
		}
		return null;
	}

	public PlayerInputLookup.Player GetActualPlayer()
	{
		return m_actualPlayer;
	}

	public PlayerGameInput GetAssignedInput()
	{
		return m_assignedInput;
	}

	private void Awake()
	{
		m_avatarDirectory = GameUtils.GetGameSession().Progress.GetAvatarDirectory();
		m_padSplitManager = GameUtils.RequireManager<PadSplitManager>();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
		Deactivate();
	}

	private void Update()
	{
		if (TimeManager.IsPaused(base.gameObject) || m_padSplitManager.IsUIOpen())
		{
			m_leftButton.ClaimPressEvent();
			m_rightButton.ClaimPressEvent();
			m_cancelButton.ClaimPressEvent();
			m_selectButton.ClaimPressEvent();
			return;
		}
		switch (m_state)
		{
		case State.SelectChef:
			UpdateChefSelect();
			break;
		case State.SelectColour:
			UpdateColourSelect();
			break;
		case State.Confirmed:
			UpdateConfirmed();
			break;
		}
	}

	private void UpdateChefSelect()
	{
		if (m_leftButton.JustPressed())
		{
			SetChefSelection(m_chefSelection - 1);
		}
		if (m_rightButton.JustPressed())
		{
			SetChefSelection(m_chefSelection + 1);
		}
		if (m_selectButton.JustPressed())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, base.gameObject.layer);
			if (m_skipColourSelection)
			{
				SetState(State.Confirmed);
			}
			else
			{
				SetState(State.SelectColour);
			}
		}
		if (!m_cancelButton.JustPressed())
		{
			return;
		}
		if (m_deactivationOverride != null)
		{
			if (m_deactivationOverride())
			{
				base.enabled = false;
			}
		}
		else
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UIBack, base.gameObject.layer);
			Deactivate();
		}
	}

	private void UpdateColourSelect()
	{
		if (m_leftButton.JustPressed())
		{
			SetColourSelection(m_colourSelection - 1);
		}
		if (m_rightButton.JustPressed())
		{
			SetColourSelection(m_colourSelection + 1);
		}
		if (m_selectButton.JustPressed())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, base.gameObject.layer);
			SetState(State.Confirmed);
		}
		if (m_cancelButton.JustPressed())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UIBack, base.gameObject.layer);
			SetState(State.SelectChef);
		}
	}

	private void UpdateConfirmed()
	{
		if (m_cancelButton.JustPressed())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UIBack, base.gameObject.layer);
			if (m_skipColourSelection)
			{
				SetState(State.SelectChef);
			}
			else
			{
				SetState(State.SelectColour);
			}
		}
	}

	private void SetColourSelection(int _selection)
	{
		if (m_colourCount > 0)
		{
			m_colourSelection = MathUtils.Wrap(_selection, 0, m_colourCount);
		}
		else
		{
			m_colourSelection = _selection;
		}
		ChefColourData chefColourData = m_avatarDirectory.Colours[m_colourSelection];
		Image[] array = m_backgroundImage.RequestComponentsRecursive<Image>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].color = chefColourData.UIColour;
		}
		m_colourArrows.color = chefColourData.UIColour;
	}

	private void SetChefSelection(int _selection)
	{
		if (m_avatars.Length > 0)
		{
			m_chefSelection = MathUtils.Wrap(_selection, 0, m_avatars.Length);
			ChefAvatarData chefAvatarData = m_avatars[m_chefSelection];
		}
	}
}
