using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientBackpackRespawnBehaviour : ClientSynchroniserBase
{
	private BackpackRespawnBehaviour m_backpackRespawnBehaviour;

	private WaitForSeconds m_waitForPfxDelay;

	private Transform m_startParent;

	private Vector3 m_startLocation;

	private ClientWorldObjectSynchroniser m_worldObjectSynchroniser;

	private ClientWorldObjectSynchroniser m_physicsObjectSynchroniser;

	private ClientPhysicalAttachment m_physicalAttachment;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpackRespawnBehaviour = (BackpackRespawnBehaviour)synchronisedObject;
		m_waitForPfxDelay = new WaitForSeconds(m_backpackRespawnBehaviour.m_particleTime);
		m_physicalAttachment = base.gameObject.RequireComponent<ClientPhysicalAttachment>();
		m_worldObjectSynchroniser = base.gameObject.RequireComponent<ClientWorldObjectSynchroniser>();
		m_physicsObjectSynchroniser = m_physicalAttachment.AccessRigidbody().gameObject.RequireComponent<ClientWorldObjectSynchroniser>();
		m_startParent = base.transform.parent;
		m_startLocation = base.transform.localPosition;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnBehaviour;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		RespawnMessage respawnMessage = (RespawnMessage)serialisable;
		if (respawnMessage.m_Phase == RespawnMessage.Phase.Begin)
		{
			base.gameObject.SetActive(false);
		}
		else
		{
			if (respawnMessage.m_Phase != RespawnMessage.Phase.End)
			{
				return;
			}
			m_physicsObjectSynchroniser.Pause();
			m_worldObjectSynchroniser.Pause();
			m_physicalAttachment.AccessRigidbody().transform.SetParent(m_startParent, false);
			m_physicalAttachment.AccessRigidbody().transform.localPosition = m_startLocation;
			m_physicalAttachment.AccessRigidbody().transform.localScale = Vector3.one;
			m_physicalAttachment.AccessRigidbody().transform.localRotation = Quaternion.identity;
			base.transform.SetParent(m_physicalAttachment.AccessRigidbody().transform, false);
			base.transform.localPosition = Vector3.zero;
			base.transform.localScale = Vector3.one;
			base.transform.localRotation = Quaternion.identity;
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = base.gameObject.RequireComponent<ServerWorldObjectSynchroniser>();
				if (serverWorldObjectSynchroniser != null)
				{
					serverWorldObjectSynchroniser.ResumeAllClients();
				}
				ServerPhysicsObjectSynchroniser serverPhysicsObjectSynchroniser = base.gameObject.RequestComponentUpwardsRecursive<ServerPhysicsObjectSynchroniser>();
				if (serverPhysicsObjectSynchroniser != null)
				{
					serverPhysicsObjectSynchroniser.ResumeAllClients();
				}
			}
			base.gameObject.SetActive(true);
			StartCoroutine(EndRespawn());
		}
	}

	private IEnumerator EndRespawn()
	{
		if (m_worldObjectSynchroniser != null)
		{
			while (!m_worldObjectSynchroniser.IsReadyToResume())
			{
				yield return null;
			}
			m_worldObjectSynchroniser.Resume();
		}
		if (m_physicsObjectSynchroniser != null)
		{
			while (!m_physicsObjectSynchroniser.IsReadyToResume())
			{
				yield return null;
			}
			m_physicsObjectSynchroniser.Resume();
		}
		Collider collider = base.gameObject.RequestComponent<Collider>();
		if (collider != null)
		{
			collider.enabled = true;
		}
		m_backpackRespawnBehaviour.m_spawnEffect.InstantiateOnParent(base.transform);
		GameUtils.TriggerAudio(GameOneShotAudioTag.PlayerSpawn, base.gameObject.layer);
		yield return m_waitForPfxDelay;
	}
}
