using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerSprayingUtensil : ServerSynchroniserBase, ICarryNotified, ITriggerReceiver
{
	private SprayingUtensilMessage m_ServerData = new SprayingUtensilMessage();

	private GameObject m_carrier;

	private RaycastHit[] m_RaycastHits = new RaycastHit[16];

	private Collider m_UtensilCollider;

	private float m_MaxSprayDistance;

	private SprayingUtensil m_SprayingUtensil;

	public GameObject Carrier
	{
		get
		{
			return m_carrier;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_SprayingUtensil = (SprayingUtensil)synchronisedObject;
		m_UtensilCollider = m_SprayingUtensil.gameObject.RequireComponent<Collider>();
		m_MaxSprayDistance = m_SprayingUtensil.m_sprayDistance;
		GetComponent<ClientInteractable>().SetStickyInteractionCallback(() => false);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SprayingUtensil;
	}

	public override void UpdateSynchronising()
	{
		if (IsSpraying())
		{
			UpdateSprayDistance();
		}
	}

	public override void OnDestroy()
	{
		StopSpray();
		if ((bool)m_carrier)
		{
			OnCarryEnded(null);
		}
		GetComponent<ClientInteractable>().SetStickyInteractionCallback(null);
		base.OnDestroy();
	}

	public virtual void OnCarryBegun(ICarrier _carrier)
	{
		m_carrier = (_carrier as MonoBehaviour).gameObject;
	}

	public virtual void OnCarryEnded(ICarrier _carrier)
	{
		if ((bool)m_carrier)
		{
			PlayerControls playerControls = m_carrier.RequireComponent<PlayerControls>();
			playerControls.SetMovementScale(1f);
		}
	}

	protected virtual void StartSpray()
	{
		if (!m_ServerData.m_bSpraying)
		{
			m_ServerData.m_bSpraying = true;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_carrier);
			if (entry != null)
			{
				m_ServerData.m_Carrier = entry.m_Header.m_uEntityID;
			}
			SendServerEvent(m_ServerData);
		}
	}

	protected virtual void StopSpray()
	{
		if (m_ServerData.m_bSpraying)
		{
			m_ServerData.m_bSpraying = false;
			m_ServerData.m_Carrier = 0u;
			SendServerEvent(m_ServerData);
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (m_SprayingUtensil.m_startSprayTrigger == _trigger)
		{
			StartSpray();
		}
		else if (m_SprayingUtensil.m_stopSprayTrigger == _trigger)
		{
			StopSpray();
		}
	}

	private void UpdateSprayDistance()
	{
		m_SprayingUtensil.m_sprayDistance = m_MaxSprayDistance;
		Ray ray = new Ray(m_SprayingUtensil.m_effectAttachPoint.position - m_SprayingUtensil.m_effectAttachPoint.forward, m_SprayingUtensil.m_effectAttachPoint.forward);
		int num = Physics.RaycastNonAlloc(ray, m_RaycastHits, m_SprayingUtensil.m_sprayDistance, m_SprayingUtensil.m_CollisionLayerMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num; i++)
		{
			if (m_RaycastHits[i].collider != m_UtensilCollider && m_RaycastHits[i].distance < m_SprayingUtensil.m_sprayDistance)
			{
				m_SprayingUtensil.m_sprayDistance = m_RaycastHits[i].distance;
			}
		}
	}

	protected bool IsInSpray(Transform _t)
	{
		Transform transform = m_carrier.transform;
		Vector3 position = transform.position;
		Vector3 position2 = _t.position;
		Vector3 lhs = position2 - position;
		float num = (m_SprayingUtensil.m_sprayDistance + 0.6f) * (m_SprayingUtensil.m_sprayDistance + 0.6f);
		if (lhs.sqrMagnitude < num)
		{
			float num2 = Vector3.Dot(lhs, transform.forward);
			if (num2 > 0f && num2 < m_SprayingUtensil.m_sprayDistance + 0.6f)
			{
				float num3 = num2 * Mathf.Sin((float)Math.PI / 180f * m_SprayingUtensil.m_sprayAngleInDegrees * 0.5f);
				Vector3 vector = transform.position + transform.forward * num2;
				float num4 = (num3 + 0.6f) * (num3 + 0.6f);
				if (num4 > (position2 - vector).sqrMagnitude)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsSpraying()
	{
		return m_ServerData.m_bSpraying;
	}
}
