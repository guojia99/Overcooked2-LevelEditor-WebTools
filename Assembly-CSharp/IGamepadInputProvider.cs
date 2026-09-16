public interface IGamepadInputProvider
{
	bool IsPadAttached(ControlPadInput.PadNum _pad);

	bool IsDown(ControlPadInput.PadNum _pad, ControlPadInput.Button _button);

	float GetValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value);
}
public interface IGamepadInputProvider<ButtonID, ValueID>
{
	bool IsPadAttached(ControlPadInput.PadNum _pad);

	bool IsDown(ControlPadInput.PadNum _pad, ButtonID _button);

	float GetValue(ControlPadInput.PadNum _pad, ValueID _value);
}
