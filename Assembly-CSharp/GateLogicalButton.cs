public class GateLogicalButton : LogicalButtonBase
{
	private Generic<bool> m_callback;

	private ILogicalButton m_childButton;

	public GateLogicalButton(ILogicalButton _childNode, Generic<bool> _callback)
	{
		m_callback = _callback;
		m_childButton = _childNode;
	}

	public override bool IsDown()
	{
		return m_callback() && m_childButton.IsDown();
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_childButton.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
