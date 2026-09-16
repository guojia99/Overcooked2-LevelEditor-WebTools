public class LogicalGenericPadValue<InputProvider, ButtonID, ValueID> : LogicalPadValueBase<ValueID> where InputProvider : Singleton<InputProvider>, IGamepadInputProvider<ButtonID, ValueID>, new()
{
	private InputProvider m_inputProvider;

	protected ILogicalButton m_positiveButton = new LogicalKeycodeButton();

	protected ILogicalButton m_negativeButton = new LogicalKeycodeButton();

	public LogicalGenericPadValue(ControlPadInput.PadNum _pad, ValueID _value, ILogicalButton _nveButton, ILogicalButton _pveButton)
		: base(_pad, _value)
	{
		m_inputProvider = Singleton<InputProvider>.Get();
		m_negativeButton = _nveButton;
		m_positiveButton = _pveButton;
	}

	public LogicalGenericPadValue(ControlPadInput.PadNum _pad, ValueID _value)
		: this(_pad, _value, (ILogicalButton)new LogicalKeycodeButton(), (ILogicalButton)new LogicalKeycodeButton())
	{
	}

	public override float GetValue()
	{
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
