public class NetworkLogicalButton : LogicalButtonBase
{
	private PlayerInputLookup.LogicalButtonID m_ButtonID;

	private ILogicalButton m_Button;

	private bool m_IsDown;

	private bool m_ButtonRequiresAppFocus;

	public NetworkLogicalButton(ILogicalButton _button, PlayerInputLookup.LogicalButtonID _buttonID, bool _buttonRequiresAppFocus)
	{
		m_Button = _button;
		m_ButtonID = _buttonID;
		m_ButtonRequiresAppFocus = _buttonRequiresAppFocus;
	}

	public override void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_Button.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}

	public override bool IsDown()
	{
		return m_IsDown;
	}

	protected override bool CanProcessInput()
	{
		return !m_ButtonRequiresAppFocus || base.CanProcessInput();
	}

	public PlayerInputLookup.LogicalButtonID GetButtonID()
	{
		return m_ButtonID;
	}

	public void SetIsDown(bool _isDown)
	{
		m_IsDown = _isDown;
	}
}
