public class NetworkLogicalValue : ILogicalValue, ILogicalElement
{
	private PlayerInputLookup.LogicalValueID m_ValueID;

	private ILogicalValue m_LogicalValue;

	private float m_Value;

	public NetworkLogicalValue(ILogicalValue _valueToNetwork, PlayerInputLookup.LogicalValueID _valueID)
	{
		m_LogicalValue = _valueToNetwork;
		m_ValueID = _valueID;
	}

	public virtual float GetValue()
	{
		return m_Value;
	}

	public void SetValue(float _value)
	{
		m_Value = _value;
	}

	public PlayerInputLookup.LogicalValueID GetLogicalID()
	{
		return m_ValueID;
	}

	public void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_LogicalValue.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
