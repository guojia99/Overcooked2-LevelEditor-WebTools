using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientRespawnCollider : ClientSynchroniserBase
{
	private RespawnCollider m_RespawnCollider;

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnCollider;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_RespawnCollider = (RespawnCollider)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		RespawnColliderMessage respawnColliderMessage = (RespawnColliderMessage)serialisable;
		if (m_RespawnCollider.m_onDeathEffect != null)
		{
			GameObject gameObject = GameObjectUtils.Instantiate(m_RespawnCollider.m_onDeathEffect, base.transform, base.transform.InverseTransformPoint(respawnColliderMessage.m_killPosition), Quaternion.identity);
			GameUtils.TriggerAudio(GameOneShotAudioTag.ItemSplash, base.gameObject.layer);
			if (gameObject != null && gameObject.RequestComponentRecursive<AutoDestructParticleSystem>() == null)
			{
				gameObject.AddComponent<AutoDestructParticleSystem>();
			}
		}
	}
}
