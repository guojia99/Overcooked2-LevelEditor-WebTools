using UnityEngine;

public class ComboLogicalValue : ILogicalValue, ILogicalElement
{
	private delegate float Void2Float();

	public enum ComboType
	{
		AbsMax = 0
	}

	private ILogicalValue[] m_baseButtons;

	private Void2Float m_valueFn;

	public ComboLogicalValue(ILogicalValue[] _buttons, ComboType _comboType = ComboType.AbsMax)
	{
		m_baseButtons = _buttons;
		m_valueFn = GetValueFunction(_comboType);
	}

	public virtual float GetValue()
	{
		return m_valueFn();
	}

	private float GetAbsMaxValue()
	{
		float num = 0f;
		for (int i = 0; i < m_baseButtons.Length; i++)
		{
			float value = m_baseButtons[i].GetValue();
			if (Mathf.Abs(value) > Mathf.Abs(num))
			{
				num = value;
			}
		}
		return num;
	}

	private Void2Float GetValueFunction(ComboType _comboType)
	{
		if (_comboType == ComboType.AbsMax)
		{
			return GetAbsMaxValue;
		}
		return () => 0f;
	}

	public void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
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
