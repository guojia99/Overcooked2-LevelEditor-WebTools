using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerContentsDisposalBehaviour : ServerSynchroniserBase, IDisposalBehaviour
{
	private ServerIngredientContainer m_container;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_container = base.gameObject.RequireComponent<ServerIngredientContainer>();
	}

	public void AddToDisposer(ICarrier _carrier, IDisposer _iDisposer)
	{
		if (m_container.GetContentsCount() > 0)
		{
			ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.TrashCan, base.gameObject.layer);
			ServerMessenger.Achievement(_carrier.AccessGameObject(), 14);
		}
		m_container.Empty();
	}

	public void AddToDisposer(IDisposer _iDisposer)
	{
		m_container.Empty();
	}

	public bool WillBeDestroyed()
	{
		return false;
	}

	public void Destroying(IDisposer disposer)
	{
		if (m_container.GetContentsCount() > 0)
		{
			ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.TrashCan, base.gameObject.layer);
		}
	}
}
