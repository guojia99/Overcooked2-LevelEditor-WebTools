using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlayerRespawnBehaviour : ServerSynchroniserBase, IRespawnBehaviour
{
	private PlayerRespawnBehaviour m_PlayerRespawnBehaviour;

	private RespawnMessage m_ServerData = new RespawnMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_PlayerRespawnBehaviour = (PlayerRespawnBehaviour)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnBehaviour;
	}

	public IEnumerator RespawnCoroutine(ServerRespawnCollider _collider)
	{
		DropOrDestroyHeldItems(_collider);
		m_ServerData.m_RespawnType = _collider.Type;
		m_ServerData.m_Phase = RespawnMessage.Phase.Begin;
		SendServerEvent(m_ServerData);
		yield break;
	}

	private void DropOrDestroyHeldItems(ServerRespawnCollider _collider)
	{
		IPlayerCarrier playerCarrier = base.gameObject.RequireInterface<IPlayerCarrier>();
		for (int i = 0; i < 2; i++)
		{
			if (playerCarrier.InspectCarriedItem((PlayerAttachTarget)i) != null)
			{
				if (_collider.Type == RespawnCollider.RespawnType.FallDeath || _collider.Type == RespawnCollider.RespawnType.Drowning)
				{
					GameObject gameObject = playerCarrier.TakeItem((PlayerAttachTarget)i);
					_collider.ObjectAdded(gameObject);
				}
				else
				{
					PlayerControlsHelper.DropHeldItem(m_PlayerRespawnBehaviour.m_playerControls, base.gameObject.transform.forward.XZ());
				}
			}
		}
	}
}
