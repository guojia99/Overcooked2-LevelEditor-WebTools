using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientSprayingUtensil : ClientSynchroniserBase
{
	private bool m_bSpraying;

	private ParticleSystem m_sprayEffect;

	private ClientInteractable m_interactable;

	private SprayingUtensil m_SprayingUtensil;

	private GameObject m_carrier;

	private CallbackVoid m_onSpray;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_SprayingUtensil = (SprayingUtensil)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<ClientInteractable>();
		m_interactable.SetStickyInteractionCallback(IsStickey);
	}

	public void RegisterOnSpray(CallbackVoid _callback)
	{
		m_onSpray = (CallbackVoid)Delegate.Combine(m_onSpray, _callback);
	}

	public void UnregisterOnSpray(CallbackVoid _callback)
	{
		m_onSpray = (CallbackVoid)Delegate.Remove(m_onSpray, _callback);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_interactable)
		{
			m_interactable.SetStickyInteractionCallback(null);
		}
	}

	private bool IsStickey()
	{
		return false;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SprayingUtensil;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		SprayingUtensilMessage sprayingUtensilMessage = (SprayingUtensilMessage)serialisable;
		if (m_bSpraying == sprayingUtensilMessage.m_bSpraying)
		{
			return;
		}
		if (sprayingUtensilMessage.m_bSpraying)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(sprayingUtensilMessage.m_Carrier);
			if (entry != null)
			{
				m_carrier = entry.m_GameObject;
			}
			StartSpray();
		}
		else
		{
			StopSpray();
			m_carrier = null;
		}
		m_bSpraying = sprayingUtensilMessage.m_bSpraying;
	}

	public override void UpdateSynchronising()
	{
		if (TimeManager.IsPaused(base.gameObject))
		{
			StopSpray();
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		StopSpray();
	}

	private void StartSpray()
	{
		if (m_sprayEffect == null)
		{
			GameObject obj = m_SprayingUtensil.m_sprayEffectPrefab.InstantiateOnParent(m_SprayingUtensil.m_effectAttachPoint);
			m_sprayEffect = obj.RequireComponent<ParticleSystem>();
			GameUtils.StartAudio(m_SprayingUtensil.m_audioTag, this, m_SprayingUtensil.gameObject.layer);
		}
		if ((bool)m_carrier)
		{
			PlayerControls playerControls = m_carrier.RequireComponent<PlayerControls>();
			playerControls.SetMovementScale(0f);
			PlayerIDProvider playerIDProvider = m_carrier.RequireComponent<PlayerIDProvider>();
			GameUtils.StartNXRumble(playerIDProvider.GetID(), m_SprayingUtensil.m_audioTag);
		}
		if (m_onSpray != null)
		{
			m_onSpray();
		}
	}

	private void StopSpray()
	{
		if (m_sprayEffect != null)
		{
			m_sprayEffect.transform.SetParent(null);
			m_sprayEffect.Stop();
			m_sprayEffect = null;
			GameUtils.StopAudio(m_SprayingUtensil.m_audioTag, this);
		}
		if ((bool)m_carrier)
		{
			PlayerControls playerControls = m_carrier.RequireComponent<PlayerControls>();
			playerControls.SetMovementScale(1f);
			PlayerIDProvider playerIDProvider = m_carrier.RequireComponent<PlayerIDProvider>();
			GameUtils.StopNXRumble(playerIDProvider.GetID(), m_SprayingUtensil.m_audioTag);
		}
	}
}
