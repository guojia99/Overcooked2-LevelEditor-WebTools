public class ReassignableValue : ILogicalValue, IReassignable<ILogicalValue>, ILogicalElement
{
	private ILogicalValue m_childValue;

	public ReassignableValue()
	{
		m_childValue = new LogicalKeycodeValue();
	}

	public ReassignableValue(ILogicalValue _childNode)
	{
		m_childValue = _childNode;
	}

	public void Reassign(ILogicalValue _childNode)
	{
		m_childValue = _childNode;
	}

	public virtual float GetValue()
	{
		return m_childValue.GetValue();
	}

	public void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_childValue.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
