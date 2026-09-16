using XInputDotNetPure;

public class XInputProvider : Singleton<XInputProvider>, IGamepadInputProvider
{
	private static GamePadState[] s_stateCache = new GamePadState[4];

	public static int ControllerCount = 4;

	public bool IsPadAttached(ControlPadInput.PadNum _pad)
	{
		return GetState(_pad).IsConnected;
	}

	public bool IsDown(ControlPadInput.PadNum _pad, ControlPadInput.Button _button)
	{
		switch (_button)
		{
		case ControlPadInput.Button.LTrigger:
			return GetValue(_pad, ControlPadInput.Value.LTrigger) > 0.5f;
		case ControlPadInput.Button.RTrigger:
			return GetValue(_pad, ControlPadInput.Value.RTrigger) > 0.5f;
		default:
		{
			GamePadState state = GetState(_pad);
			return GetButtonState(state, _button) == ButtonState.Pressed;
		}
		}
	}

	private static ButtonState GetButtonState(GamePadState _padState, ControlPadInput.Button _button)
	{
		switch (_button)
		{
		case ControlPadInput.Button.A:
			return _padState.Buttons.A;
		case ControlPadInput.Button.X:
			return _padState.Buttons.X;
		case ControlPadInput.Button.B:
			return _padState.Buttons.B;
		case ControlPadInput.Button.Y:
			return _padState.Buttons.Y;
		case ControlPadInput.Button.LB:
			return _padState.Buttons.LeftShoulder;
		case ControlPadInput.Button.RB:
			return _padState.Buttons.RightShoulder;
		case ControlPadInput.Button.Back:
			return _padState.Buttons.Back;
		case ControlPadInput.Button.Start:
			return _padState.Buttons.Start;
		case ControlPadInput.Button.LeftAnalog:
			return _padState.Buttons.LeftStick;
		case ControlPadInput.Button.RightAnalog:
			return _padState.Buttons.RightStick;
		case ControlPadInput.Button.DPadUp:
			return _padState.DPad.Up;
		case ControlPadInput.Button.DPadDown:
			return _padState.DPad.Down;
		case ControlPadInput.Button.DPadLeft:
			return _padState.DPad.Left;
		case ControlPadInput.Button.DPadRight:
			return _padState.DPad.Right;
		default:
			return ButtonState.Released;
		}
	}

	public float GetValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value)
	{
		GamePadState state = GetState(_pad);
		switch (_value)
		{
		case ControlPadInput.Value.LStickX:
			return state.ThumbSticks.Left.X;
		case ControlPadInput.Value.LStickY:
			return 0f - state.ThumbSticks.Left.Y;
		case ControlPadInput.Value.RStickX:
			return state.ThumbSticks.Right.X;
		case ControlPadInput.Value.RStickY:
			return 0f - state.ThumbSticks.Right.Y;
		case ControlPadInput.Value.DPadX:
			return ButtonsToValue(state.DPad.Left, state.DPad.Right);
		case ControlPadInput.Value.DPadY:
			return 0f - ButtonsToValue(state.DPad.Down, state.DPad.Up);
		case ControlPadInput.Value.LTrigger:
			return state.Triggers.Left;
		case ControlPadInput.Value.RTrigger:
			return state.Triggers.Right;
		default:
			return 0f;
		}
	}

	private static float ButtonsToValue(ButtonState _negative, ButtonState _positive)
	{
		float num = ((_negative != ButtonState.Pressed) ? 0f : (-1f));
		float num2 = ((_positive != ButtonState.Pressed) ? 0f : 1f);
		return num + num2;
	}

	private static bool IsDown(ButtonState _state)
	{
		return _state == ButtonState.Pressed;
	}

	private static GamePadState GetState(ControlPadInput.PadNum _pad)
	{
		return s_stateCache[(int)_pad];
	}

	public static void Update()
	{
		for (int i = 0; i < s_stateCache.Length; i++)
		{
			s_stateCache[i] = GamePad.GetState((PlayerIndex)i);
		}
	}
}
