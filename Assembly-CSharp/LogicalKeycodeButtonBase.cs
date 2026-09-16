using UnityEngine;

public abstract class LogicalKeycodeButtonBase<ButtonId> : LogicalButtonBase
{
	protected KeyCode? m_keyCode;

	protected ButtonId m_button;

	public LogicalKeycodeButtonBase(KeyCode? _code, ButtonId _button)
	{
		m_keyCode = _code;
		m_button = _button;
	}

	public LogicalKeycodeButtonBase()
	{
	}

	public override bool IsDown()
	{
		if (!CanProcessInput())
		{
			Update(false);
			return false;
		}
		bool flag = false;
		KeyCode? keyCode = m_keyCode;
		if (keyCode.HasValue)
		{
			KeyCode? keyCode2 = m_keyCode;
			flag = Input.GetKey((!keyCode2.HasValue) ? KeyCode.A : keyCode2.Value);
		}
		Update(flag);
		return flag;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
	}

	public ButtonId GetControlButton()
	{
		return m_button;
	}
}
