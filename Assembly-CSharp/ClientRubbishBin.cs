using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientRubbishBin : ClientSynchroniserBase, IClientHandlePickup, IClientHandlePlacement, IBaseHandlePickup, IBaseHandlePlacement
{
	private class ItemData
	{
		public Transform m_transform;

		public float m_deathProp;

		public bool m_alive = true;
	}

	private RubbishBin m_rubbishBin;

	private List<ItemData> m_itemData = new List<ItemData>();

	private ClientAttachStation m_clientAttachStation;

	private AttachStation m_attachStation;

	private ClientTabletopConveyenceReceiver m_clientReceiver;

	public override EntityType GetEntityType()
	{
		return EntityType.RubbishBin;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_rubbishBin = (RubbishBin)synchronisedObject;
		m_attachStation = m_rubbishBin.gameObject.RequireComponent<AttachStation>();
		m_clientAttachStation = m_rubbishBin.gameObject.RequireComponent<ClientAttachStation>();
		m_clientReceiver = base.gameObject.RequireComponent<ClientTabletopConveyenceReceiver>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		RubbishBinMessage rubbishBinMessage = (RubbishBinMessage)serialisable;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(rubbishBinMessage.BinnedItemEntityID);
		if (entry == null)
		{
			return;
		}
		entry.m_GameObject.GetComponent<Collider>().enabled = false;
		Transform transform = entry.m_GameObject.transform;
		if (!rubbishBinMessage.m_alive)
		{
			for (int i = 0; i < m_itemData.Count; i++)
			{
				if (m_itemData[i].m_transform == transform)
				{
					m_itemData[i].m_alive = rubbishBinMessage.m_alive;
					break;
				}
			}
			ClientHandlePickupReferral clientHandlePickupReferral = entry.m_GameObject.RequestComponent<ClientHandlePickupReferral>();
			if (clientHandlePickupReferral != null && clientHandlePickupReferral.GetHandlePickupReferree() == this)
			{
				clientHandlePickupReferral.SetHandlePickupReferree(null);
			}
		}
		else
		{
			ItemData itemData = new ItemData();
			itemData.m_transform = transform;
			itemData.m_deathProp = 0f;
			itemData.m_alive = true;
			m_itemData.Add(itemData);
			ClientHandlePickupReferral clientHandlePickupReferral2 = entry.m_GameObject.RequestComponent<ClientHandlePickupReferral>();
			if (clientHandlePickupReferral2 != null)
			{
				clientHandlePickupReferral2.SetHandlePickupReferree(this);
			}
		}
	}

	public override void UpdateSynchronising()
	{
		for (int num = m_itemData.Count - 1; num >= 0; num--)
		{
			ItemData itemData = m_itemData[num];
			if (itemData.m_transform == null || itemData.m_deathProp >= 1f || !itemData.m_alive)
			{
				m_itemData.RemoveAt(num);
			}
		}
		for (int i = 0; i < m_itemData.Count; i++)
		{
			ItemData itemData2 = m_itemData[i];
			if (itemData2.m_transform.parent == m_attachStation.GetAttachPoint(itemData2.m_transform.gameObject))
			{
				float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
				itemData2.m_deathProp = Mathf.Clamp01(itemData2.m_deathProp + deltaTime / m_rubbishBin.m_fallTime);
				Transform transform = itemData2.m_transform;
				transform.localPosition = -Vector3.up * m_rubbishBin.m_fallDistance;
				transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.AddY(m_rubbishBin.m_angularVelocity * deltaTime));
				transform.localScale = VectorUtils.Splat3(1f - itemData2.m_deathProp);
			}
		}
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return _carrier != null && m_clientAttachStation.CanHandlePickup(_carrier) && m_clientReceiver.IsReceiving();
	}

	public int GetPickupPriority()
	{
		return int.MaxValue;
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (gameObject == null || gameObject.RequestInterface<IDisposalBehaviour>() == null)
		{
			return false;
		}
		return m_clientAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context);
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}
}
