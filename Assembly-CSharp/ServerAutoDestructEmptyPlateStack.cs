using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAutoDestructEmptyPlateStack : ServerSynchroniserBase, ISurfacePlacementNotified
{
	private AutoDestructEmptyPlateStack m_autoDestruct;

	private ServerPlateStackBase m_plateStack;

	private ServerAttachStation m_attachStation;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_autoDestruct = (AutoDestructEmptyPlateStack)synchronisedObject;
		m_plateStack = base.gameObject.RequestComponent<ServerPlateStackBase>();
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (ActiveAndEnabled && !(m_autoDestruct == null) && m_autoDestruct.enabled && !(m_plateStack == null) && !(m_plateStack.gameObject == null) && m_plateStack.GetSize() <= 0)
		{
			if (m_attachStation != null)
			{
				m_attachStation.TakeItem();
			}
			NetworkUtils.DestroyObjectsRecursive(m_plateStack.gameObject);
		}
	}

	public void OnSurfacePlacement(ServerAttachStation _station)
	{
		m_attachStation = _station;
	}

	public void OnSurfaceDeplacement(ServerAttachStation _station)
	{
		m_attachStation = null;
	}
}
