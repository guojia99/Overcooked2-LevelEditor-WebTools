using UnityEngine;

public class LogicalPCEngagementButton : LogicalButtonBase
{
	protected PCPadInputProvider m_InputProvider;

	protected ControlPadInput.PadNum m_Pad;

	public LogicalPCEngagementButton(ControlPadInput.PadNum _pad)
	{
		m_InputProvider = Singleton<PCPadInputProvider>.Get();
		m_Pad = _pad;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		_tree = new AcyclicGraph<ILogicalElement, LogicalLinkInfo>(this);
		_head = _tree.GetNode(this);
	}

	public override bool IsDown()
	{
		if (!Application.isFocused)
		{
			Update(false);
			return false;
		}
		bool flag = m_InputProvider.IsEngagementDown(m_Pad);
		Update(flag);
		return flag;
	}
}
