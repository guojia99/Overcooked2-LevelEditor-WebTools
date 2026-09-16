using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerRubbishBin : ServerSynchroniserBase, IDisposer, IHandlePickup, IHandlePlacement, IBaseHandlePickup, IBaseHandlePlacement
{
	private class ItemData
	{
		public IAttachment m_object;

		public float m_deathProp;
	}

	private RubbishBin m_rubbishBin;

	private List<ItemData> m_itemData = new List<ItemData>();

	private Generic<bool> m_true = () => true;

	private RubbishBinMessage m_Data = new RubbishBinMessage();

	private ServerAttachStation m_serverAttachStation;

	private ServerTabletopConveyenceReceiver m_serverReceiver;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_rubbishBin = (RubbishBin)synchronisedObject;
		m_serverAttachStation = m_rubbishBin.gameObject.RequireComponent<ServerAttachStation>();
		m_serverReceiver = base.gameObject.RequireComponent<ServerTabletopConveyenceReceiver>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RubbishBin;
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return _carrier != null && m_serverAttachStation.CanHandlePickup(_carrier) && m_serverReceiver.IsReceiving();
	}

	public int GetPickupPriority()
	{
		return int.MaxValue;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		if (_carrier != null)
		{
			m_serverAttachStation.HandlePickup(_carrier, _directionXZ);
		}
	}

	public void PassToDestroy(IAttachment _attachment)
	{
		ServerLimitedQuantityItem serverLimitedQuantityItem = _attachment.AccessGameObject().RequestComponent<ServerLimitedQuantityItem>();
		if (null != serverLimitedQuantityItem)
		{
			serverLimitedQuantityItem.AddInvincibilityCondition(m_true);
		}
		_attachment.AccessGameObject().GetComponent<Collider>().enabled = false;
		m_Data.BinnedItemEntityID = ((ServerSynchroniserBase)_attachment).GetEntityId();
		m_Data.m_alive = true;
		SendServerEvent(m_Data);
		ItemData itemData = new ItemData();
		itemData.m_object = _attachment;
		itemData.m_deathProp = 0f;
		m_itemData.Add(itemData);
		ServerHandlePickupReferral serverHandlePickupReferral = _attachment.AccessGameObject().RequestComponent<ServerHandlePickupReferral>();
		if (serverHandlePickupReferral != null)
		{
			serverHandlePickupReferral.SetHandlePickupReferree(this);
		}
	}

	public override void UpdateSynchronising()
	{
		GameObject gameObject = m_serverAttachStation.InspectItem();
		if (gameObject != null && !m_serverReceiver.IsReceiving() && m_itemData.Count <= 0)
		{
			IAttachment attachment = gameObject.RequireInterface<IAttachment>();
			IThrowable component = gameObject.GetComponent<IThrowable>();
			if (component as MonoBehaviour != null)
			{
				MonoBehaviour monoBehaviour = component.GetPreviousThrower() as MonoBehaviour;
				if (monoBehaviour != null)
				{
					GameObject gameObject2 = (component as MonoBehaviour).gameObject;
					if (monoBehaviour.gameObject != null)
					{
						ServerMessenger.Achievement(monoBehaviour.gameObject, 14);
					}
					GameObject gameObject3 = attachment.AccessGameObject();
					if (gameObject3 != null)
					{
						IDisposalBehaviour disposalBehaviour = gameObject3.RequestInterface<IDisposalBehaviour>();
						if (disposalBehaviour != null)
						{
							disposalBehaviour.Destroying(this);
						}
					}
				}
			}
			PassToDestroy(attachment);
		}
		for (int num = m_itemData.Count - 1; num >= 0; num--)
		{
			ItemData itemData = m_itemData[num];
			if (itemData.m_object as MonoBehaviour == null)
			{
				m_itemData.RemoveAt(num);
			}
		}
		for (int i = 0; i < m_itemData.Count; i++)
		{
			ItemData itemData2 = m_itemData[i];
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			itemData2.m_deathProp = Mathf.Clamp01(itemData2.m_deathProp + deltaTime / m_rubbishBin.m_fallTime);
			if (itemData2.m_deathProp >= 1f && m_serverAttachStation.InspectItem() != null)
			{
				m_serverAttachStation.TakeItem();
				GameObject obj = itemData2.m_object.AccessGameObject();
				ServerHandlePickupReferral serverHandlePickupReferral = obj.RequestComponent<ServerHandlePickupReferral>();
				if (serverHandlePickupReferral != null && serverHandlePickupReferral.GetHandlePickupReferree() == this)
				{
					serverHandlePickupReferral.SetHandlePickupReferree(null);
				}
				IAttachment attachment2 = obj.RequireInterface<IAttachment>();
				m_Data.BinnedItemEntityID = ((ServerSynchroniserBase)attachment2).GetEntityId();
				m_Data.m_alive = false;
				SendServerEvent(m_Data);
				ServerPlayerRespawnManager.KillOrRespawn(obj, null);
				itemData2.m_object = null;
			}
		}
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		if (_carrier == null || !(_carrier.AccessGameObject() != null))
		{
			return;
		}
		IDisposalBehaviour disposalBehaviour = _carrier.InspectCarriedItem().RequestInterface<IDisposalBehaviour>();
		if (disposalBehaviour != null && !disposalBehaviour.WillBeDestroyed())
		{
			disposalBehaviour.AddToDisposer(_carrier, this);
			return;
		}
		if (disposalBehaviour != null)
		{
			disposalBehaviour.Destroying(this);
		}
		m_serverAttachStation.HandlePlacement(_carrier, _directionXZ, _context);
		ServerMessenger.Achievement(_carrier.AccessGameObject(), 14);
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (gameObject == null || gameObject.RequestInterface<IDisposalBehaviour>() == null)
		{
			return false;
		}
		return m_serverAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context);
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}
}
