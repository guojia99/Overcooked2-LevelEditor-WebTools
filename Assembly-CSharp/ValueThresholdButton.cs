using UnityEngine;

public class ValueThresholdButton : LogicalButtonBase
{
	public enum ThresholdType
	{
		Greater = 0,
		LessThan = 1,
		AbsGreater = 2,
		AbsLessThan = 3
	}

	public ILogicalValue m_baseValue;

	public float m_thldValue;

	public ThresholdType m_thldType;

	public ValueThresholdButton(ILogicalValue _logicalValue, ThresholdType _type, float _thldValue)
	{
		m_baseValue = _logicalValue;
		m_thldType = _type;
		m_thldValue = _thldValue;
	}

	public override bool IsDown()
	{
		if (!CanProcessInput())
		{
			Update(false);
			return false;
		}
		bool flag = true;
		float value = m_baseValue.GetValue();
		switch (m_thldType)
		{
		case ThresholdType.Greater:
			flag = value > m_thldValue;
			break;
		case ThresholdType.AbsGreater:
			flag = Mathf.Abs(value) > m_thldValue;
			break;
		case ThresholdType.LessThan:
			flag = value < m_thldValue;
			break;
		case ThresholdType.AbsLessThan:
			flag = Mathf.Abs(value) < m_thldValue;
			break;
		}
		Update(flag);
		return flag;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_baseValue.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
