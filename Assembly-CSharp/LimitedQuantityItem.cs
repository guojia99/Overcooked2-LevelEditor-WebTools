using System;
using UnityEngine;

public class LimitedQuantityItem : MonoBehaviour
{
	private ImpendingDestructionCallback m_Callback = delegate
	{
	};

	public void NotifyOfImpendingDestruction()
	{
		m_Callback(base.gameObject);
	}

	public void RegisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_Callback = (ImpendingDestructionCallback)Delegate.Combine(m_Callback, func);
	}

	public void UnregisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_Callback = (ImpendingDestructionCallback)Delegate.Remove(m_Callback, func);
	}
}
