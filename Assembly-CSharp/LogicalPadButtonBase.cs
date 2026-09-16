public abstract class LogicalPadButtonBase<ButtonId> : LogicalButtonBase
{
	protected ControlPadInput.PadNum m_pad;

	protected ButtonId m_button;

	public LogicalPadButtonBase(ControlPadInput.PadNum _pad, ButtonId _button)
	{
		m_pad = _pad;
		m_button = _button;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
	}

	public ButtonId GetControlpadButton()
	{
		return m_button;
	}
}
