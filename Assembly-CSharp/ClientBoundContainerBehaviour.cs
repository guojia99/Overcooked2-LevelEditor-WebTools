using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientBoundContainerBehaviour : ClientSynchroniserBase
{
	private BoundContainer m_boundContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_boundContainer = (BoundContainer)synchronisedObject;
	}
}
