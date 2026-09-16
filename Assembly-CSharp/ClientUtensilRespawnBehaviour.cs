using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientUtensilRespawnBehaviour : ClientSynchroniserBase
{
	private UtensilRespawnBehaviour m_utensilRespawnBehaviour;

	private WaitForSeconds m_waitForPfxDelay;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_utensilRespawnBehaviour = (UtensilRespawnBehaviour)synchronisedObject;
		m_waitForPfxDelay = new WaitForSeconds(m_utensilRespawnBehaviour.m_particleTime);
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
		else if (respawnMessage.m_Phase == RespawnMessage.Phase.End)
		{
			base.gameObject.SetActive(true);
			base.transform.position = respawnMessage.m_RespawnPosition;
			base.transform.localScale = Vector3.one;
			Collider component = base.gameObject.GetComponent<Collider>();
			if (component != null)
			{
				component.enabled = true;
			}
			StartCoroutine(EndRespawn());
		}
	}

	private IEnumerator EndRespawn()
	{
		m_utensilRespawnBehaviour.m_spawnEffect.InstantiateOnParent(base.transform);
		GameUtils.TriggerAudio(GameOneShotAudioTag.PlayerSpawn, base.gameObject.layer);
		yield return m_waitForPfxDelay;
	}
}
