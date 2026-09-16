public class TimedLogicalButton : ILogicalButton, ILogicalElement
{
	public enum Condition
	{
		HeldShorter = 0,
		HeldLonger = 1
	}

	protected float m_downTime;

	protected float m_requiredDownTime;

	protected Condition m_condition;

	protected ILogicalButton m_buttonBase;

	public TimedLogicalButton(ILogicalButton _baseButton, Condition _condition, float _requiredDownTime)
	{
		m_buttonBase = _baseButton;
		m_requiredDownTime = _requiredDownTime;
		m_condition = _condition;
	}

	public TimedLogicalButton()
	{
		m_requiredDownTime = 0f;
		m_condition = Condition.HeldLonger;
	}

	public bool JustPressed()
	{
		bool flag = HasUnclaimedPressEvent();
		if (flag)
		{
			ClaimPressEvent();
		}
		return flag;
	}

	public bool JustReleased()
	{
		bool flag = HasUnclaimedReleaseEvent();
		if (flag)
		{
			ClaimReleaseEvent();
		}
		return flag;
	}

	public virtual void ClaimPressEvent()
	{
		if (m_condition == Condition.HeldShorter)
		{
			m_buttonBase.ClaimReleaseEvent();
		}
		m_buttonBase.ClaimPressEvent();
	}

	public virtual bool HasUnclaimedPressEvent()
	{
		bool flag = m_buttonBase.HasUnclaimedReleaseEvent();
		bool flag2 = m_buttonBase.HasUnclaimedPressEvent();
		float heldTimeLength = m_buttonBase.GetHeldTimeLength();
		bool flag3 = false;
		switch (m_condition)
		{
		case Condition.HeldShorter:
			flag3 = heldTimeLength < m_requiredDownTime && flag;
			break;
		case Condition.HeldLonger:
			flag3 = heldTimeLength >= m_requiredDownTime && flag2;
			break;
		default:
			flag3 = false;
			break;
		}
		if (flag3)
		{
			return true;
		}
		return false;
	}

	public virtual bool HasUnclaimedReleaseEvent()
	{
		bool flag = m_buttonBase.HasUnclaimedReleaseEvent();
		float heldTimeLength = m_buttonBase.GetHeldTimeLength();
		bool flag2 = false;
		switch (m_condition)
		{
		case Condition.HeldShorter:
			flag2 = heldTimeLength < m_requiredDownTime && flag;
			break;
		case Condition.HeldLonger:
			flag2 = heldTimeLength >= m_requiredDownTime && flag;
			break;
		default:
			flag2 = false;
			break;
		}
		if (flag2)
		{
			return true;
		}
		return false;
	}

	public virtual void ClaimReleaseEvent()
	{
		m_buttonBase.ClaimReleaseEvent();
		if (m_condition == Condition.HeldShorter)
		{
			m_buttonBase.ClaimPressEvent();
		}
	}

	public virtual bool IsDown()
	{
		if (m_condition == Condition.HeldLonger)
		{
			float heldTimeLength = m_buttonBase.GetHeldTimeLength();
			return heldTimeLength >= m_requiredDownTime && m_buttonBase.IsDown();
		}
		return false;
	}

	public virtual float GetHeldTimeLength()
	{
		if (m_buttonBase.IsDown())
		{
			return m_buttonBase.GetHeldTimeLength();
		}
		return 0f;
	}

	public void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _tree, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head)
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head2;
		m_buttonBase.GetLogicTreeData(out _tree, out _head2);
		_tree.AddLink(this, _head2.m_value, new LogicalLinkInfo());
		_head = _tree.GetNode(this);
	}
}
