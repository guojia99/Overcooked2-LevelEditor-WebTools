public class GateLogicalValue : ILogicalValue, ILogicalElement
{
	private Generic<bool> m_callback;

	private ILogicalValue m_childValue;

	public GateLogicalValue(ILogicalValue _childNode, Generic<bool> _callback)
	{
		m_callback = _callback;
		m_childValue = _childNode;
	}

	public virtual float GetValue()
	{
		if (m_callback())
		{
			return m_childValue.GetValue();
		}
		return 0f;
	}

	public void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_childValue.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
