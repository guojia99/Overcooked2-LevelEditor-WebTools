public class LogicalPadButton<InputProvider> : LogicalPadButtonBase<ControlPadInput.Button> where InputProvider : Singleton<InputProvider>, IGamepadInputProvider, new()
{
	protected InputProvider m_inputProvider;

	public LogicalPadButton(ControlPadInput.PadNum _pad, ControlPadInput.Button _button)
		: base(_pad, _button)
	{
		m_inputProvider = Singleton<InputProvider>.Get();
	}

	public override bool IsDown()
	{
		if (!CanProcessInput())
		{
			Update(false);
			return false;
		}
		bool flag = m_inputProvider.IsDown(m_pad, m_button);
		Update(flag);
		return flag;
	}
}
