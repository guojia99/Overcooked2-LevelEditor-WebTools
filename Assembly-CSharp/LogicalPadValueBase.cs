public abstract class LogicalPadValueBase<ValueID> : ILogicalValue, ILogicalElement
{
	protected ControlPadInput.PadNum m_pad;

	protected ValueID m_value;

	public LogicalPadValueBase(ControlPadInput.PadNum _pad, ValueID _value)
	{
		m_pad = _pad;
		m_value = _value;
	}

	public virtual void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
	}

	public abstract float GetValue();

	public ValueID GetControlpadValue()
	{
		return m_value;
	}
}
