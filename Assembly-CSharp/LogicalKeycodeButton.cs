using UnityEngine;

public class LogicalKeycodeButton : LogicalKeycodeButtonBase<ControlPadInput.Button>
{
	public LogicalKeycodeButton(KeyCode? _code, ControlPadInput.Button _button)
		: base(_code, _button)
	{
	}

	public LogicalKeycodeButton()
	{
	}
}
