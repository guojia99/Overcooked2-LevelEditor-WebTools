public class ComboLogicalButton : LogicalButtonBase
{
	public enum ComboType
	{
		And = 0,
		Or = 1
	}

	private ILogicalButton[] m_baseButtons;

	private ComboType m_comboType;

	public ComboLogicalButton(ILogicalButton[] _buttons, ComboType _comboType = ComboType.Or)
	{
		m_baseButtons = _buttons;
		m_comboType = _comboType;
	}

	public override bool IsDown()
	{
		bool flag = false;
		switch (m_comboType)
		{
		case ComboType.And:
		{
			flag = true;
			for (int j = 0; j < m_baseButtons.Length; j++)
			{
				flag = flag && m_baseButtons[j].IsDown();
			}
			break;
		}
		case ComboType.Or:
		{
			flag = false;
			for (int i = 0; i < m_baseButtons.Length; i++)
			{
				flag = flag || m_baseButtons[i].IsDown();
			}
			break;
		}
		}
		Update(flag);
		return flag;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
		for (int i = 0; i < m_baseButtons.Length; i++)
		{
			AcyclicGraph<ILogicalElement, LogicalLinkInfo> _graph;
			AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
			m_baseButtons[i].GetLogicTreeData(out _graph, out _head2);
			_tree = AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Merge(_tree, _graph);
			_tree.AddLink(_head2.m_value, _head.m_value, new LogicalLinkInfo());
		}
	}
}
