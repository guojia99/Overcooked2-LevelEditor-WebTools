using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

[ExecutionDependency(typeof(MetaEnvironmentFactory))]
[ExecutionDependency(typeof(BootstrapManager))]
[AddComponentMenu("Scripts/Game/Input/PlayerInputLookup")]
public class PlayerInputLookup : MonoBehaviour
{
	public enum LogicalButtonID
	{
		None = 0,
		PickupAndDrop = 1,
		Dash = 2,
		WorkstationInteract = 3,
		ResetButton = 4,
		QuitButton = 5,
		UIUp = 6,
		UIDown = 7,
		UILeft = 8,
		UIRight = 9,
		UISelect = 10,
		UISelectNotStart = 11,
		UICancel = 12,
		Pause = 13,
		PlayerSwitch = 14,
		Curse = 15,
		UIStick = 16,
		UIMultiChefMenu = 17,
		DebugMenu = 18,
		UIChangeMode = 19,
		UIResultsToggleProfile = 20,
		Horn = 21,
		UILeftPlayerSpecific = 22,
		UIRightPlayerSpecific = 23,
		UIRestartLevel = 24,
		UISkip = 1000
	}

	public enum LogicalValueID
	{
		None = 0,
		MovementX = 1,
		MovementY = 2
	}

	public enum Player
	{
		One = 0,
		Two = 1,
		Three = 2,
		Four = 3,
		Five = 4,
		Six = 5,
		Seven = 6,
		Eight = 7,
		Nine = 8,
		Ten = 9,
		Eleven = 10,
		Count = 11
	}

	private struct ValueDebugButtons
	{
		public KeyCode m_inc;

		public KeyCode m_dec;

		public ValueDebugButtons(KeyCode _dec, KeyCode _inc)
		{
			m_inc = _inc;
			m_dec = _dec;
		}
	}

	[SerializeField]
	private bool m_getPadAssignmentFromSession = true;

	[SerializeField]
	private GameInputConfigData m_defaultInputLookup;

	private Dictionary<WeakReference, CallbackVoid> m_reassignableElements = new Dictionary<WeakReference, CallbackVoid>();

	private PlayerManager m_playerManager;

	public static CallbackVoid OnRegenerateControls = delegate
	{
	};

	private static PlayerInputLookup s_this;

	private static GameInputConfig s_baseInputConfig = null;

	public static bool IsAwake()
	{
		return s_this != null;
	}

	private void Awake()
	{
		s_this = this;
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		if (s_baseInputConfig == null && m_defaultInputLookup != null)
		{
			s_baseInputConfig = m_defaultInputLookup.Config;
		}
		if (m_getPadAssignmentFromSession)
		{
			GameUtils.EnsureBootstrapSetup();
		}
		OnRegenerateControls();
	}

	public static int GetSystemControllerMaximum()
	{
		if (typeof(PCPadInputProvider) == typeof(DirectInputProvider))
		{
			return 11;
		}
		if (typeof(PCPadInputProvider) == typeof(PCPadInputProvider))
		{
			return 12;
		}
		if (typeof(PCPadInputProvider) == typeof(XInputProvider))
		{
			return 4;
		}
		return 12;
	}

	public static ControlPadInput.PadNum GetPadForPlayer(Player _player)
	{
		GameInputConfig inputConfig = GetInputConfig();
		PlayerGameInput inputData = inputConfig.GetInputData(_player);
		if (inputData != null)
		{
			return inputData.Pad;
		}
		return ControlPadInput.PadNum.Count;
	}

	public static GameInputConfig GetInputConfig()
	{
		return s_baseInputConfig;
	}

	public static GameInputConfig GetBaseInputConfig()
	{
		return s_baseInputConfig;
	}

	public static void SetInputConfig(GameInputConfig _inputConfig)
	{
		SetBaseInputConfig(_inputConfig);
	}

	public static void SetBaseInputConfig(GameInputConfig _inputConfig)
	{
		s_baseInputConfig = _inputConfig;
		if (s_this != null)
		{
			s_this.RegenerateControls();
		}
	}

	public static void ResetToDefaultInputConfig()
	{
		if (s_this != null && s_this.m_defaultInputLookup != null)
		{
			SetBaseInputConfig(s_this.m_defaultInputLookup.Config);
		}
	}

	private void RegenerateControls()
	{
		m_reassignableElements.RemoveAll((KeyValuePair<WeakReference, CallbackVoid> x) => !x.Key.IsAlive);
		foreach (KeyValuePair<WeakReference, CallbackVoid> reassignableElement in m_reassignableElements)
		{
			reassignableElement.Value();
		}
		PCPadInputProvider.UpdateKeyboardButtons();
		OnRegenerateControls();
	}

	public static bool IsPadAttached(ControlPadInput.PadNum _pad)
	{
		return Singleton<PCPadInputProvider>.Get().IsPadAttached(_pad);
	}

	public static ILogicalButton ConstructDebugBoundButton(ControlPadInput.PadNum _pad, ControlPadInput.Button _button)
	{
		return ConstructDebugBoundButton(new ControlPadInput.ButtonIdentifier(_pad, _button));
	}

	private static ILogicalButton ConstructDebugBoundButton(ControlPadInput.ButtonIdentifier _buttonIdentifier)
	{
		return new LogicalPadButton<PCPadInputProvider>(_buttonIdentifier.pad, _buttonIdentifier.button);
	}

	private static ILogicalButton GetSidedLogicalCombo(AmbiPadButton _gamePadButton, PlayerGameInput _playerInput)
	{
		ControlPadInput.ButtonIdentifier[] realButtons = GetInputConfig().GetRealButtons(_playerInput, _gamePadButton);
		Converter<ControlPadInput.ButtonIdentifier, ILogicalButton> converter = (ControlPadInput.ButtonIdentifier _id) => ConstructDebugBoundButton(_id);
		ILogicalButton[] buttons = Array.ConvertAll(realButtons, converter);
		return new ComboLogicalButton(buttons);
	}

	private static ILogicalValue GetSidedLogicalCombo(AmbiPadValue _gamePadValue, PlayerGameInput _playerInput)
	{
		ControlPadInput.ValueIdentifier[] realValues = GetInputConfig().GetRealValues(_playerInput, _gamePadValue);
		Converter<ControlPadInput.ValueIdentifier, ILogicalValue> converter = (ControlPadInput.ValueIdentifier _id) => ConstructDebugBoundValue(_id);
		ILogicalValue[] buttons = Array.ConvertAll(realValues, converter);
		return new ComboLogicalValue(buttons);
	}

	public static ILogicalButton GetUIButton(LogicalButtonID _id, Player _p = Player.One)
	{
		return GetButton(_id, _p);
	}

	public static ILogicalValue GetUIValue(LogicalValueID _id, Player _p = Player.One)
	{
		return GetValue(_id, _p);
	}

	public static ILogicalButton GetButton(LogicalButtonID _id, Player _player)
	{
		Generic<ILogicalButton> generator = () => GetFixedButton(_id, _player);
		return BuildReassignableElement<ReassignableButton, ILogicalButton>(generator);
	}

	public static ILogicalButton GetAnyButton(LogicalButtonID _id)
	{
		ILogicalButton[] buttons = new ILogicalButton[4]
		{
			GetEngagedButton(_id, Player.One),
			GetEngagedButton(_id, Player.Two),
			GetEngagedButton(_id, Player.Three),
			GetEngagedButton(_id, Player.Four)
		};
		return new ComboLogicalButton(buttons);
	}

	public static ILogicalButton GetAnyButton(LogicalButtonID _id, PadSide _forceSide)
	{
		ILogicalButton[] buttons = new ILogicalButton[4]
		{
			GetEngagedButton(_id, Player.One, _forceSide),
			GetEngagedButton(_id, Player.Two, _forceSide),
			GetEngagedButton(_id, Player.Three, _forceSide),
			GetEngagedButton(_id, Player.Four, _forceSide)
		};
		return new ComboLogicalButton(buttons);
	}

	public static ILogicalButton GetEngagementButton(PlayerGameInput _playerGameInput)
	{
		return new LogicalPCEngagementButton(_playerGameInput.Pad);
	}

	public static ILogicalButton GetEngagedButton(LogicalButtonID _id, Player _player)
	{
		Generic<ILogicalButton> generator = delegate
		{
			ControlPadInput.PadNum padForPlayer = GetPadForPlayer(_player);
			return (padForPlayer != ControlPadInput.PadNum.Count) ? GateOnEngagement(GetFixedButton(_id, _player), (EngagementSlot)padForPlayer) : new ComboLogicalButton(new ILogicalButton[0]);
		};
		return BuildReassignableElement<ReassignableButton, ILogicalButton>(generator);
	}

	public static ILogicalButton GetEngagedButton(LogicalButtonID _id, Player _player, PadSide _side)
	{
		Generic<ILogicalButton> generator = delegate
		{
			ControlPadInput.PadNum padForPlayer = GetPadForPlayer(_player);
			if (padForPlayer != ControlPadInput.PadNum.Count && s_this != null)
			{
				AmbiControlsMappingData mappingData = ((_side != PadSide.Both) ? s_this.m_playerManager.SidedAmbiMapping : s_this.m_playerManager.UnsidedAmbiMapping);
				PlayerGameInput playerGameInput = new PlayerGameInput(padForPlayer, _side, mappingData);
				return GateOnEngagement(GetFixedButton(_id, playerGameInput), (EngagementSlot)_player);
			}
			return new ComboLogicalButton(new ILogicalButton[0]);
		};
		return BuildReassignableElement<ReassignableButton, ILogicalButton>(generator);
	}

	public static ILogicalValue GetValue(LogicalValueID _id, Player _player)
	{
		Generic<ILogicalValue> generator = () => GetFixedValue(_id, _player);
		return BuildReassignableElement<ReassignableValue, ILogicalValue>(generator);
	}

	public static ILogicalValue GetAnyValue(LogicalValueID _id)
	{
		ILogicalValue[] buttons = new ILogicalValue[4]
		{
			GetEngagedValue(_id, Player.One),
			GetEngagedValue(_id, Player.Two),
			GetEngagedValue(_id, Player.Three),
			GetEngagedValue(_id, Player.Four)
		};
		return new ComboLogicalValue(buttons);
	}

	public static ILogicalValue GetAnyValueForLocals(LogicalValueID _id)
	{
		FastList<User> users = ClientUserSystem.m_Users;
		List<ILogicalValue> list = new List<ILogicalValue>();
		for (int i = 0; i < users.Count; i++)
		{
			User user = users._items[i];
			if (user.IsLocal)
			{
				list.Add(GetEngagedValue(_id, (Player)i));
			}
		}
		return new ComboLogicalValue(list.ToArray());
	}

	private static ILogicalValue GetEngagedValue(LogicalValueID _id, Player _player)
	{
		Generic<ILogicalValue> generator = delegate
		{
			ControlPadInput.PadNum padForPlayer = GetPadForPlayer(_player);
			return (padForPlayer != ControlPadInput.PadNum.Count) ? GateOnEngagement(GetFixedValue(_id, _player), (EngagementSlot)padForPlayer) : new ComboLogicalValue(new ILogicalValue[0]);
		};
		return BuildReassignableElement<ReassignableValue, ILogicalValue>(generator);
	}

	private static Concrete BuildReassignableElement<Concrete, IElement>(Generic<IElement> _generator) where Concrete : class, IReassignable<IElement>, new() where IElement : ILogicalElement
	{
		IElement childNode = _generator();
		Concrete val = new Concrete();
		val.Reassign(childNode);
		WeakReference key = new WeakReference(val, false);
		CallbackVoid value = delegate
		{
			Concrete val2 = key.Target as Concrete;
			if (key.IsAlive && val2 != null)
			{
				val2.Reassign(_generator());
			}
		};
		s_this.m_reassignableElements.Add(key, value);
		return val;
	}

	private static ILogicalButton GetFixedButton(LogicalButtonID _id, Player _player)
	{
		PlayerGameInput inputData = GetInputConfig().GetInputData(_player);
		if (inputData != null)
		{
			return GetFixedButton(_id, inputData);
		}
		return new LogicalKeycodeButton();
	}

	public static AmbiPadButton LogicalToAmbiButton(LogicalButtonID _id)
	{
		switch (_id)
		{
		case LogicalButtonID.PickupAndDrop:
			return AmbiPadButton.One;
		case LogicalButtonID.WorkstationInteract:
			return AmbiPadButton.Two;
		case LogicalButtonID.Dash:
			return AmbiPadButton.Three;
		case LogicalButtonID.Curse:
			return AmbiPadButton.Four;
		case LogicalButtonID.PlayerSwitch:
			return AmbiPadButton.Five;
		default:
			return AmbiPadButton.Count;
		}
	}

	public static AmbiPadValue[] LogicalToAmbiValue(LogicalValueID _id)
	{
		switch (_id)
		{
		case LogicalValueID.MovementX:
			return new AmbiPadValue[2]
			{
				AmbiPadValue.DPadX,
				AmbiPadValue.StickX
			};
		case LogicalValueID.MovementY:
			return new AmbiPadValue[2]
			{
				AmbiPadValue.DPadY,
				AmbiPadValue.StickY
			};
		default:
			return null;
		}
	}

	public static ILogicalButton GetFixedButton(LogicalButtonID _id, PlayerGameInput _playerGameInput)
	{
		switch (_id)
		{
		case LogicalButtonID.None:
			return new LogicalKeycodeButton();
		case LogicalButtonID.PickupAndDrop:
			return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.PickupAndDrop), _playerGameInput);
		case LogicalButtonID.Dash:
			return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.Dash), _playerGameInput);
		case LogicalButtonID.WorkstationInteract:
			return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.WorkstationInteract), _playerGameInput);
		case LogicalButtonID.ResetButton:
			return GetSidedLogicalCombo(AmbiPadButton.Start, _playerGameInput);
		case LogicalButtonID.QuitButton:
			return GetSidedLogicalCombo(AmbiPadButton.Start, _playerGameInput);
		case LogicalButtonID.UIUp:
		{
			ILogicalButton logicalButton5 = new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadY, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickY, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f)
			});
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton6 = new LogicalKeycodeButton(KeyCode.UpArrow, ControlPadInput.Button.DPadUp);
				return new ComboLogicalButton(new ILogicalButton[2] { logicalButton6, logicalButton5 });
			}
			return logicalButton5;
		}
		case LogicalButtonID.UIDown:
		{
			ILogicalButton logicalButton2 = new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadY, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickY, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f)
			});
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton3 = new LogicalKeycodeButton(KeyCode.DownArrow, ControlPadInput.Button.DPadDown);
				return new ComboLogicalButton(new ILogicalButton[2] { logicalButton3, logicalButton2 });
			}
			return logicalButton2;
		}
		case LogicalButtonID.UILeft:
		{
			ILogicalButton logicalButton10 = new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadX, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickX, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f)
			});
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton11 = new LogicalKeycodeButton(KeyCode.LeftArrow, ControlPadInput.Button.DPadLeft);
				return new ComboLogicalButton(new ILogicalButton[2] { logicalButton11, logicalButton10 });
			}
			return logicalButton10;
		}
		case LogicalButtonID.UIRight:
		{
			ILogicalButton logicalButton8 = new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadX, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickX, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f)
			});
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton9 = new LogicalKeycodeButton(KeyCode.RightArrow, ControlPadInput.Button.DPadRight);
				return new ComboLogicalButton(new ILogicalButton[2] { logicalButton9, logicalButton8 });
			}
			return logicalButton8;
		}
		case LogicalButtonID.UILeftPlayerSpecific:
			return new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadX, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickX, _playerGameInput), ValueThresholdButton.ThresholdType.LessThan, -0.75f)
			});
		case LogicalButtonID.UIRightPlayerSpecific:
			return new ComboLogicalButton(new ILogicalButton[2]
			{
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.DPadX, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f),
				new ValueThresholdButton(GetSidedLogicalCombo(AmbiPadValue.StickX, _playerGameInput), ValueThresholdButton.ThresholdType.Greater, 0.75f)
			});
		case LogicalButtonID.UISelect:
		{
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton4 = new LogicalKeycodeButton(KeyCode.Space, ControlPadInput.Button.A);
				ILogicalButton sidedLogicalCombo2 = GetSidedLogicalCombo(AmbiPadButton.Confirm, _playerGameInput);
				ILogicalButton sidedLogicalCombo3 = GetSidedLogicalCombo(AmbiPadButton.Start, _playerGameInput);
				return new ComboLogicalButton(new ILogicalButton[3] { logicalButton4, sidedLogicalCombo2, sidedLogicalCombo3 });
			}
			ILogicalButton sidedLogicalCombo4 = GetSidedLogicalCombo(AmbiPadButton.Confirm, _playerGameInput);
			ILogicalButton sidedLogicalCombo5 = GetSidedLogicalCombo(AmbiPadButton.Start, _playerGameInput);
			return new ComboLogicalButton(new ILogicalButton[2] { sidedLogicalCombo4, sidedLogicalCombo5 });
		}
		case LogicalButtonID.UISelectNotStart:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				ILogicalButton logicalButton = new LogicalKeycodeButton(KeyCode.Space, ControlPadInput.Button.A);
				ILogicalButton sidedLogicalCombo = GetSidedLogicalCombo(AmbiPadButton.Confirm, _playerGameInput);
				return new ComboLogicalButton(new ILogicalButton[2] { logicalButton, sidedLogicalCombo });
			}
			return GetSidedLogicalCombo(AmbiPadButton.Confirm, _playerGameInput);
		case LogicalButtonID.UICancel:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.Escape, ControlPadInput.Button.B);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Cancel, _playerGameInput);
		case LogicalButtonID.UISkip:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.LeftAlt, ControlPadInput.Button.B);
			}
			return GetFixedButton(LogicalButtonID.UICancel, _playerGameInput);
		case LogicalButtonID.UIStick:
			return GetSidedLogicalCombo(AmbiPadButton.Stick, _playerGameInput);
		case LogicalButtonID.Pause:
		{
			ILogicalButton sidedLogicalCombo6 = GetSidedLogicalCombo(AmbiPadButton.Start, _playerGameInput);
			ILogicalButton logicalButton7 = new LogicalKeycodeButton(KeyCode.Escape, ControlPadInput.Button.Start);
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return logicalButton7;
			}
			return new ComboLogicalButton(new ILogicalButton[2] { sidedLogicalCombo6, logicalButton7 });
		}
		case LogicalButtonID.PlayerSwitch:
			return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.PlayerSwitch), _playerGameInput);
		case LogicalButtonID.Curse:
			return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.Curse), _playerGameInput);
		case LogicalButtonID.UIMultiChefMenu:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.E, ControlPadInput.Button.Y);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Four, _playerGameInput);
		case LogicalButtonID.DebugMenu:
			return GetSidedLogicalCombo(AmbiPadButton.DebugMenu, _playerGameInput);
		case LogicalButtonID.UIResultsToggleProfile:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.LeftControl, ControlPadInput.Button.X);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Two, _playerGameInput);
		case LogicalButtonID.UIChangeMode:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.LeftControl, ControlPadInput.Button.X);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Four, _playerGameInput);
		case LogicalButtonID.UIRestartLevel:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return GetSidedLogicalCombo(LogicalToAmbiButton(LogicalButtonID.Dash), _playerGameInput);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Cancel, _playerGameInput);
		case LogicalButtonID.Horn:
			if (KeyboardUtils.IsKeyboard(_playerGameInput))
			{
				return new LogicalKeycodeButton(KeyCode.T, ControlPadInput.Button.LeftAnalog);
			}
			return GetSidedLogicalCombo(AmbiPadButton.Horn, _playerGameInput);
		default:
			return new LogicalKeycodeButton();
		}
	}

	public static ControlPadInput.Button[] GetRealButtons(LogicalButtonID buttonID, PadSide side)
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		PlayerGameInput playerGameInput = new PlayerGameInput(ControlPadInput.PadNum.One, side, (side != PadSide.Both) ? playerManager.SidedAmbiMapping : playerManager.UnsidedAmbiMapping);
		AmbiPadButton ambiPadButton = LogicalToAmbiButton(buttonID);
		if (ambiPadButton != AmbiPadButton.Count)
		{
			ControlPadInput.ButtonIdentifier[] realButtons = GetInputConfig().GetRealButtons(playerGameInput, ambiPadButton);
			return realButtons.ConvertAll((ControlPadInput.ButtonIdentifier x) => x.button);
		}
		return new ControlPadInput.Button[0];
	}

	private static KeyCode GetDebugButton(ControlPadInput.Button _button)
	{
		switch (_button)
		{
		case ControlPadInput.Button.A:
			return KeyCode.G;
		case ControlPadInput.Button.B:
			return KeyCode.H;
		case ControlPadInput.Button.X:
			return KeyCode.F;
		case ControlPadInput.Button.Y:
			return KeyCode.T;
		case ControlPadInput.Button.LB:
			return KeyCode.E;
		case ControlPadInput.Button.RB:
			return KeyCode.U;
		case ControlPadInput.Button.Back:
			return KeyCode.Backspace;
		case ControlPadInput.Button.Start:
			return KeyCode.Return;
		case ControlPadInput.Button.LeftAnalog:
			return KeyCode.LeftShift;
		case ControlPadInput.Button.RightAnalog:
			return KeyCode.RightShift;
		case ControlPadInput.Button.LTrigger:
			return KeyCode.R;
		case ControlPadInput.Button.RTrigger:
			return KeyCode.Y;
		case ControlPadInput.Button.DPadLeft:
			return KeyCode.LeftArrow;
		case ControlPadInput.Button.DPadRight:
			return KeyCode.RightArrow;
		case ControlPadInput.Button.DPadUp:
			return KeyCode.UpArrow;
		case ControlPadInput.Button.DPadDown:
			return KeyCode.DownArrow;
		default:
			return KeyCode.None;
		}
	}

	private static ILogicalValue ConstructDebugBoundValue(ControlPadInput.ValueIdentifier _valueIdentifier)
	{
		return new LogicalPadValue<PCPadInputProvider>(_valueIdentifier.pad, _valueIdentifier.value);
	}

	private static ILogicalValue GetFixedValue(LogicalValueID _id, Player _player)
	{
		PlayerGameInput inputData = GetInputConfig().GetInputData(_player);
		if (inputData != null)
		{
			return GetFixedValue(_id, inputData);
		}
		return new LogicalKeycodeValue();
	}

	private static ILogicalValue GetFixedValue(LogicalValueID _id, PlayerGameInput _playerGameInput)
	{
		switch (_id)
		{
		case LogicalValueID.None:
			return new LogicalKeycodeValue();
		case LogicalValueID.MovementX:
			return new ComboLogicalValue(new ILogicalValue[2]
			{
				GetSidedLogicalCombo(AmbiPadValue.DPadX, _playerGameInput),
				GetSidedLogicalCombo(AmbiPadValue.StickX, _playerGameInput)
			});
		case LogicalValueID.MovementY:
			return new ComboLogicalValue(new ILogicalValue[2]
			{
				GetSidedLogicalCombo(AmbiPadValue.DPadY, _playerGameInput),
				GetSidedLogicalCombo(AmbiPadValue.StickY, _playerGameInput)
			});
		default:
			return new LogicalKeycodeValue();
		}
	}

	private static ILogicalValue GetEngagedFixedValue(LogicalValueID _id, Player _player)
	{
		ControlPadInput.PadNum padForPlayer = GetPadForPlayer(_player);
		return GateOnEngagement(GetFixedValue(_id, _player), (EngagementSlot)padForPlayer);
	}

	private static ILogicalValue GateOnEngagement(ILogicalValue _value, EngagementSlot _e)
	{
		Generic<bool> callback = () => s_this.m_playerManager.GetUser(_e) != null;
		return new GateLogicalValue(_value, callback);
	}

	private static ILogicalButton GateOnEngagement(ILogicalButton _button, EngagementSlot _e)
	{
		Generic<bool> callback = () => s_this.m_playerManager.GetUser(_e) != null;
		return new GateLogicalButton(_button, callback);
	}

	private static ValueDebugButtons GetDebugButtons(ControlPadInput.Value _value)
	{
		switch (_value)
		{
		case ControlPadInput.Value.LStickX:
			return new ValueDebugButtons(KeyCode.A, KeyCode.D);
		case ControlPadInput.Value.LStickY:
			return new ValueDebugButtons(KeyCode.W, KeyCode.S);
		case ControlPadInput.Value.RStickX:
			return new ValueDebugButtons(KeyCode.J, KeyCode.L);
		case ControlPadInput.Value.RStickY:
			return new ValueDebugButtons(KeyCode.I, KeyCode.K);
		case ControlPadInput.Value.DPadX:
			return new ValueDebugButtons(KeyCode.LeftArrow, KeyCode.RightArrow);
		case ControlPadInput.Value.DPadY:
			return new ValueDebugButtons(KeyCode.DownArrow, KeyCode.UpArrow);
		case ControlPadInput.Value.LTrigger:
			return new ValueDebugButtons(KeyCode.None, KeyCode.R);
		case ControlPadInput.Value.RTrigger:
			return new ValueDebugButtons(KeyCode.None, KeyCode.Y);
		default:
			return new ValueDebugButtons(KeyCode.None, KeyCode.None);
		}
	}

	private void Update()
	{
		MonitorDebugPadSwitching();
		XInputProvider.Update();
		DirectInputProvider.Update();
	}

	private void MonitorDebugPadSwitching()
	{
	}
}
