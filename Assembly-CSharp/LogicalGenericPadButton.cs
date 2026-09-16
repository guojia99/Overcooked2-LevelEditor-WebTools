public class LogicalGenericPadButton<InputProvider, ButtonID, ValueID> : LogicalPadButtonBase<ButtonID> where InputProvider : Singleton<InputProvider>, IGamepadInputProvider<ButtonID, ValueID>, new()
{
	protected InputProvider m_inputProvider;

	public LogicalGenericPadButton(ControlPadInput.PadNum _pad, ButtonID _button)
		: base(_pad, _button)
	{
		m_inputProvider = Singleton<InputProvider>.Get();
	}

	public override bool IsDown()
	{
		bool flag = m_inputProvider.IsDown(m_pad, m_button);
		Update(flag);
		return flag;
	}
}
