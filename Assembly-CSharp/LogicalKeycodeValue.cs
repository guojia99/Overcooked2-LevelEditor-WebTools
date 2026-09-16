using UnityEngine;

public class LogicalKeycodeValue : ILogicalValue, ILogicalElement
{
	private ValueCode? m_valueCode;

	private ILogicalButton m_positiveButton = new LogicalKeycodeButton();

	private ILogicalButton m_negativeButton = new LogicalKeycodeButton();

	public LogicalKeycodeValue(ValueCode? _code, ILogicalButton _nveButton, ILogicalButton _pveButton)
	{
		m_valueCode = _code;
		m_negativeButton = _nveButton;
		m_positiveButton = _pveButton;
	}

	public LogicalKeycodeValue(ValueCode? _code)
	{
		m_valueCode = _code;
	}

	public LogicalKeycodeValue()
	{
	}

	public virtual float GetValue()
	{
		if (m_positiveButton.IsDown())
		{
			return 1f;
		}
		if (m_negativeButton.IsDown())
		{
			return -1f;
		}
		ValueCode? valueCode = m_valueCode;
		if (valueCode.HasValue)
		{
			ValueCode? valueCode2 = m_valueCode;
			return Input.GetAxis((valueCode2.HasValue ? valueCode2.Value : ValueCode.Joystick1Axis1).ToString());
		}
		return 0f;
	}

	public virtual void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
	}
}
