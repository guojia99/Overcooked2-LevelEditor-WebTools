using UnityEngine;

public abstract class LogicalButtonBase : ILogicalButton, ILogicalElement
{
	private bool m_pressClaimed = true;

	private bool m_releaseClaimed = true;

	private float m_buttonDownTime;

	private float m_buttonDownLength;

	private bool m_down;

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

	public bool HasUnclaimedPressEvent()
	{
		bool flag = IsDown();
		Update(flag);
		return flag && !m_pressClaimed;
	}

	public void ClaimPressEvent()
	{
		m_pressClaimed = true;
	}

	public bool HasUnclaimedReleaseEvent()
	{
		bool flag = IsDown();
		Update(flag);
		return !flag && !m_releaseClaimed;
	}

	public void ClaimReleaseEvent()
	{
		m_releaseClaimed = true;
	}

	public float GetHeldTimeLength()
	{
		bool flag = IsDown();
		Update(flag);
		if (flag)
		{
			m_buttonDownLength = Time.time - m_buttonDownTime;
		}
		return m_buttonDownLength;
	}

	public abstract bool IsDown();

	public abstract void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _graph, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head);

	protected virtual bool CanProcessInput()
	{
		return Application.isFocused;
	}

	protected void Update(bool _isDown)
	{
		m_pressClaimed = m_pressClaimed && _isDown;
		m_releaseClaimed = m_releaseClaimed && !_isDown;
		if (_isDown && !m_down)
		{
			m_buttonDownTime = Time.time;
		}
		else if (_isDown && !m_down)
		{
			m_buttonDownLength = Time.time - m_buttonDownTime;
		}
		m_down = _isDown;
		if (!CanProcessInput())
		{
			ClaimPressEvent();
			ClaimReleaseEvent();
		}
	}
}
