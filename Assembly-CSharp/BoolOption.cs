internal abstract class BoolOption : Option<BoolOption.States>, OptionsData.IUnloadable
{
	public enum States
	{
		False = 0,
		True = 1
	}

	protected States m_prevState;

	protected States m_state;

	public void Unload()
	{
		m_prevState = States.False;
		m_state = States.False;
	}

	protected override States GetState()
	{
		return m_state;
	}

	protected override void SetState(States _state)
	{
		m_prevState = m_state;
		m_state = _state;
	}

	public override void Commit()
	{
	}
}
