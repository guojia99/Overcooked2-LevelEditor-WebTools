using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientLimitedQuantityItem : ClientSynchroniserBase
{
	private LimitedQuantityItem m_baseObject;

	private LimitedQuantityItemManager m_Manager;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (LimitedQuantityItem)synchronisedObject;
		m_Manager = GameUtils.RequireManager<LimitedQuantityItemManager>();
		base.StartSynchronising(synchronisedObject);
	}

	public void PlayDestructionPFX()
	{
		if (null != m_Manager.m_DestroyPFXPrefab)
		{
			m_Manager.m_DestroyPFXPrefab.InstantiatePFX(base.transform.position);
		}
	}

	public void RegisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_baseObject.RegisterImpendingDestructionNotification(func);
	}

	public void UnregisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_baseObject.UnregisterImpendingDestructionNotification(func);
	}

	public void NotifyOfImpendingDestruction()
	{
		m_baseObject.NotifyOfImpendingDestruction();
	}
}
