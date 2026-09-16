using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MultiChefPadUIController : UIControllerBase
{
	private enum State
	{
		NotThere = 0,
		LeftOnly = 1,
		Combined = 2,
		RightOnly = 3,
		Split = 4
	}

	[SerializeField]
	private ControlPadInput.PadNum m_padNum;

	[SerializeField]
	[AssignChildRecursive("BothNumber", Editorbility.NonEditable)]
	private Text m_bothNumber;

	[SerializeField]
	[AssignChildRecursive("LeftNumber", Editorbility.NonEditable)]
	private Text m_leftNumber;

	[SerializeField]
	[AssignChildRecursive("RightNumber", Editorbility.NonEditable)]
	private Text m_rightNumber;

	[SerializeField]
	[AssignChildRecursive("PadImage", Editorbility.NonEditable)]
	private Image m_noneImage;

	[SerializeField]
	[AssignChildRecursive("PadImageL", Editorbility.NonEditable)]
	private Image m_leftPadImage;

	[SerializeField]
	[AssignChildRecursive("PadImageR", Editorbility.NonEditable)]
	private Image m_rightPadImage;

	[SerializeField]
	[AssignChildRecursive("PadImageB", Editorbility.NonEditable)]
	private Image m_bothPadImage;

	[SerializeField]
	[AssignChildRecursive("NumberCircleL", Editorbility.NonEditable)]
	private Image m_leftNumberCircle;

	[SerializeField]
	[AssignChildRecursive("NumberCircleR", Editorbility.NonEditable)]
	private Image m_rightNumberCircle;

	[SerializeField]
	[AssignChildRecursive("NumberCircleB", Editorbility.NonEditable)]
	private Image m_bothNumberCircle;

	[SerializeField]
	[AssignResource("MainAvatarDirectory", Editorbility.NonEditable)]
	private AvatarDirectoryData m_avatarDirectory;

	[SerializeField]
	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_sidedMappingData;

	[SerializeField]
	[AssignChildRecursive("Arrows", Editorbility.NonEditable)]
	private Image m_arrows;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Animator m_animator;

	[SerializeField]
	private Sprite m_keyboardBothImage;

	[SerializeField]
	private Sprite m_keyboardLeftImage;

	[SerializeField]
	private Sprite m_keyboardRightImage;

	public Generic<bool, int> CanAddPlayerQuery;

	private PadSplitManager m_padSplitManager;

	private IPlayerManager m_playerManager;

	private ILogicalButton m_splitButtonL;

	private ILogicalButton m_splitButtonR;

	private Sprite m_padImageBoth;

	private Sprite m_padImageL;

	private Sprite m_padImageR;

	private bool m_padAtttached;

	private State m_state;

	private static readonly int m_iPadConnected = Animator.StringToHash("PadConnected");

	private static readonly int m_iHasSplitLeft = Animator.StringToHash("HasSplitLeft");

	private static readonly int m_iHasSplitRight = Animator.StringToHash("HasSplitRight");

	private static readonly int m_iMaxChefsWarning = Animator.StringToHash("MaxChefsWarning");

	public bool IsPadActive
	{
		get
		{
			return Slot == EngagementSlot.One || m_playerManager.GetUser(Slot) != null;
		}
	}

	private EngagementSlot Slot
	{
		get
		{
			return (EngagementSlot)m_padNum;
		}
	}

	public ControlPadInput.PadNum PadNum
	{
		get
		{
			return m_padNum;
		}
	}

	private int StateId
	{
		get
		{
			return (int)(m_state - 1);
		}
		set
		{
			int intervalMin = 1;
			int num = 4;
			State state = (State)MathUtils.Wrap(value + 1, intervalMin, num + 1);
			SetState(state);
		}
	}

	public event CallbackVoid OnSplitStateChange = delegate
	{
	};

	public void Reinitialise()
	{
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		GameInputConfig.ConfigEntry[] array = baseInputConfig.m_playerConfigs.FindAll((GameInputConfig.ConfigEntry x) => x.Pad == m_padNum);
		if (array.Length == 0 || !IsPadActive)
		{
			SetState(State.NotThere);
		}
		else if (array.Length == 1)
		{
			PadSide uIHandedness = array[0].UIHandedness;
			AssignPlayer(uIHandedness, array[0].Player);
			switch (uIHandedness)
			{
			case PadSide.Both:
				SetState(State.Combined);
				break;
			case PadSide.Left:
				SetState(State.LeftOnly);
				break;
			case PadSide.Right:
				SetState(State.RightOnly);
				break;
			}
		}
		else
		{
			foreach (GameInputConfig.ConfigEntry configEntry in array)
			{
				AssignPlayer(configEntry.Side, configEntry.Player);
			}
			SetState(State.Split);
		}
	}

	public void AssignPlayer(PadSide _side, PlayerInputLookup.Player _player)
	{
		GetTextForSide(_side).text = ((int)(_player + 1)/*cast due to .constrained prefix*/).ToString();
		ChefColourData chefColourData = m_avatarDirectory.Colours[(int)_player];
		GetPadImage(_side).color = chefColourData.PadUIColour;
		GetNumberCircle(_side).color = chefColourData.UIColour;
	}

	private Text GetTextForSide(PadSide _side)
	{
		switch (_side)
		{
		case PadSide.Both:
			return m_bothNumber;
		case PadSide.Left:
			return m_leftNumber;
		case PadSide.Right:
			return m_rightNumber;
		default:
			return null;
		}
	}

	private Image GetPadImage(PadSide _side)
	{
		switch (_side)
		{
		case PadSide.Left:
			return m_leftPadImage;
		case PadSide.Right:
			return m_rightPadImage;
		case PadSide.Both:
			return m_bothPadImage;
		default:
			return null;
		}
	}

	private Image GetNumberCircle(PadSide _side)
	{
		switch (_side)
		{
		case PadSide.Left:
			return m_leftNumberCircle;
		case PadSide.Right:
			return m_rightNumberCircle;
		case PadSide.Both:
			return m_bothNumberCircle;
		default:
			return null;
		}
	}

	private void Awake()
	{
		PlayerGameInput playerGameInput = new PlayerGameInput(m_padNum, PadSide.Both, m_sidedMappingData);
		m_splitButtonL = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UILeft, playerGameInput);
		m_splitButtonR = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UIRight, playerGameInput);
		m_playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_padSplitManager = GameUtils.RequireManager<PadSplitManager>();
	}

	private bool MonitorPadAttached()
	{
		bool isPadActive = IsPadActive;
		if (m_state != State.NotThere && !isPadActive)
		{
			SetState(State.NotThere);
		}
		if (isPadActive && m_state == State.NotThere)
		{
			if (CanAddPlayerQuery(1))
			{
				SetState(State.Combined);
			}
			else if (!m_padAtttached)
			{
				m_animator.SetTrigger(m_iMaxChefsWarning);
			}
		}
		m_padAtttached = isPadActive;
		return m_state != State.NotThere;
	}

	private bool IsKeyboard()
	{
		GamepadUser user = m_playerManager.GetUser(Slot);
		return user != null && user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
	}

	private void Update()
	{
		bool flag = IsKeyboard();
		bool flag2 = MonitorPadAttached();
		m_arrows.gameObject.SetActive(flag2);
		if (flag2)
		{
			if (m_splitButtonL.JustPressed())
			{
				MoveState(-1);
			}
			if (m_splitButtonR.JustPressed())
			{
				MoveState(1);
			}
		}
		if (m_padImageBoth == null)
		{
			m_padImageBoth = m_bothPadImage.sprite;
			m_padImageL = m_leftPadImage.sprite;
			m_padImageR = m_rightPadImage.sprite;
		}
		else
		{
			m_bothPadImage.sprite = ((!flag) ? m_padImageBoth : m_keyboardBothImage);
			m_leftPadImage.sprite = ((!flag) ? m_padImageL : m_keyboardLeftImage);
			m_rightPadImage.sprite = ((!flag) ? m_padImageR : m_keyboardRightImage);
			m_noneImage.enabled = !flag;
		}
	}

	private void MoveState(int _progression)
	{
		int intervalMin = 1;
		int num = 4;
		int num2 = MathUtils.Wrap((int)(m_state + _progression), intervalMin, num + 1);
		State state = (State)num2;
		if (state == State.Split && !CanAddPlayerQuery(1))
		{
			m_animator.SetTrigger(m_iMaxChefsWarning);
		}
		else
		{
			SetState(state);
		}
	}

	private void SetState(State _state)
	{
		m_animator.SetBool(m_iPadConnected, _state != State.NotThere);
		m_animator.SetBool(m_iHasSplitLeft, _state == State.LeftOnly || _state == State.Split);
		m_animator.SetBool(m_iHasSplitRight, _state == State.RightOnly || _state == State.Split);
		m_state = _state;
		this.OnSplitStateChange();
	}

	public IEnumerable IterateSides()
	{
		switch (m_state)
		{
		case State.NotThere:
			break;
		case State.LeftOnly:
			yield return PadSide.Left;
			break;
		case State.RightOnly:
			yield return PadSide.Right;
			break;
		case State.Split:
			yield return PadSide.Left;
			yield return PadSide.Right;
			break;
		case State.Combined:
			yield return PadSide.Both;
			break;
		}
	}
}
