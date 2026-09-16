using UnityEngine;

public class LogicalPadValue<InputProvider> : LogicalPadValueBase<ControlPadInput.Value> where InputProvider : Singleton<InputProvider>, IGamepadInputProvider, new()
{
	private InputProvider m_inputProvider;

	protected ILogicalButton m_positiveButton = new LogicalKeycodeButton();

	protected ILogicalButton m_negativeButton = new LogicalKeycodeButton();

	public LogicalPadValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value, ILogicalButton _nveButton, ILogicalButton _pveButton)
		: base(_pad, _value)
	{
		m_inputProvider = Singleton<InputProvider>.Get();
		m_negativeButton = _nveButton;
		m_positiveButton = _pveButton;
	}

	public LogicalPadValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value)
		: this(_pad, _value, (ILogicalButton)new LogicalKeycodeButton(), (ILogicalButton)new LogicalKeycodeButton())
	{
	}

	protected virtual bool CanProcessInput()
	{
		return Application.isFocused;
	}

	public override float GetValue()
	{
		if (!CanProcessInput())
		{
			return 0f;
		}
		if (m_positiveButton.IsDown())
		{
			return 1f;
		}
		if (m_negativeButton.IsDown())
		{
			return -1f;
		}
		return m_inputProvider.GetValue(m_pad, m_value);
	}
}
