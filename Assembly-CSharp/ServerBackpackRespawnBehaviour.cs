using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerBackpackRespawnBehaviour : ServerSynchroniserBase, IRespawnBehaviour
{
	protected BackpackRespawnBehaviour m_backpackRespawnBehaviour;

	private RespawnMessage m_ServerData = new RespawnMessage();

	private bool m_isRespawning;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpackRespawnBehaviour = (BackpackRespawnBehaviour)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnBehaviour;
	}

	public IEnumerator RespawnCoroutine(ServerRespawnCollider _collider)
	{
		if (m_isRespawning)
		{
			yield break;
		}
		m_isRespawning = true;
		ServerPhysicalAttachment attachment = base.gameObject.RequestComponent<ServerPhysicalAttachment>();
		if (attachment.IsAttached())
		{
			attachment.Detach();
		}
		if ((bool)attachment)
		{
			attachment.ManualDisable(true);
		}
		m_ServerData.m_RespawnType = ((!(_collider != null)) ? RespawnCollider.RespawnType.FallDeath : _collider.Type);
		m_ServerData.m_Phase = RespawnMessage.Phase.Begin;
		SendServerEvent(m_ServerData);
		base.gameObject.SetActive(false);
		IEnumerator wait = CoroutineUtils.TimerRoutine(m_backpackRespawnBehaviour.m_respawnTime, base.gameObject.layer);
		while (wait.MoveNext())
		{
			yield return null;
		}
		if ((bool)attachment)
		{
			attachment.ManualEnable();
		}
		if (!(this == null) && !(base.gameObject == null))
		{
			Collider collider = base.gameObject.GetComponent<Collider>();
			if (collider != null)
			{
				collider.enabled = true;
			}
			m_ServerData.m_Phase = RespawnMessage.Phase.End;
			SendServerEvent(m_ServerData);
			m_isRespawning = false;
		}
	}
}
