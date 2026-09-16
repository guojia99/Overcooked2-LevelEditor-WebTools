using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlayerAttachmentCarrier : ClientSynchroniserBase, IPlayerCarrier, ICarrier, ICarrierPlacement
{
	private class BlockPickup : IClientHandlePickup, IBaseHandlePickup
	{
		public bool CanHandlePickup(ICarrier _carrier)
		{
			return false;
		}

		public int GetPickupPriority()
		{
			return int.MaxValue;
		}
	}

	private PlayerAttachmentCarrier m_attachmentCarrier;

	private IClientAttachment[] m_carriedObjects = new IClientAttachment[2];

	private VoidGeneric<GameObject, GameObject> m_carryItemChangedCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachmentCarrier = (PlayerAttachmentCarrier)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.ChefCarry;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		ChefCarryMessage chefCarryMessage = (ChefCarryMessage)serialisable;
		IClientAttachment clientAttachment = m_carriedObjects[(int)chefCarryMessage.m_playerAttachTarget];
		IClientAttachment clientAttachment2 = null;
		uint carriableItem = chefCarryMessage.m_carriableItem;
		if (carriableItem != 0)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(carriableItem);
			IClientAttachment component = entry.m_GameObject.GetComponent<IClientAttachment>();
			clientAttachment2 = component;
		}
		if (clientAttachment2 != clientAttachment)
		{
			if (clientAttachment2 != null)
			{
				CarryItem(clientAttachment2.AccessGameObject());
			}
			else
			{
				TakeItem(chefCarryMessage.m_playerAttachTarget);
			}
		}
	}

	public GameObject AccessGameObject()
	{
		return base.gameObject;
	}

	public GameObject InspectCarriedItem()
	{
		return InspectCarriedItem(PlayerAttachTarget.Default);
	}

	public GameObject InspectCarriedItem(PlayerAttachTarget playerAttachTarget)
	{
		return (!(m_carriedObjects[(int)playerAttachTarget] as MonoBehaviour != null)) ? null : m_carriedObjects[(int)playerAttachTarget].AccessGameObject();
	}

	public bool HasAttachment(PlayerAttachTarget playerAttachTarget)
	{
		return m_attachmentCarrier.GetAttachPoint(playerAttachTarget).childCount > 0;
	}

	public void CarryItem(GameObject _object)
	{
		PlayerAttachTarget playerAttachTarget = PlayerAttachTarget.Default;
		IHandleAttachTarget handleAttachTarget = _object.RequestInterface<IHandleAttachTarget>();
		if (handleAttachTarget as MonoBehaviour != null)
		{
			playerAttachTarget = handleAttachTarget.PlayerAttachTarget;
		}
		MonoBehaviour monoBehaviour = m_carriedObjects[(int)playerAttachTarget] as MonoBehaviour;
		IClientAttachment component = _object.GetComponent<IClientAttachment>();
		if (component != null)
		{
			m_carriedObjects[(int)playerAttachTarget] = component;
		}
		ICarryNotified[] array = _object.RequestInterfaces<ICarryNotified>();
		foreach (ICarryNotified carryNotified in array)
		{
			carryNotified.OnCarryBegun(this);
		}
		ClientHandlePickupReferral component2 = _object.GetComponent<ClientHandlePickupReferral>();
		if ((bool)component2 && component2.CanBeBlocked(this))
		{
			component2.SetHandlePickupReferree(new BlockPickup());
		}
		m_carryItemChangedCallback(null, _object);
		WindAccumulator windAccumulator = _object.RequestComponent<WindAccumulator>();
		if (windAccumulator != null)
		{
			windAccumulator.Reset();
		}
	}

	public GameObject TakeItem(PlayerAttachTarget playerAttachTarget)
	{
		if (m_carriedObjects[(int)playerAttachTarget] != null)
		{
			IClientAttachment clientAttachment = m_carriedObjects[(int)playerAttachTarget];
			ClientHandlePickupReferral component = clientAttachment.AccessGameObject().GetComponent<ClientHandlePickupReferral>();
			if ((bool)component)
			{
				component.SetHandlePickupReferree(null);
			}
			ICarryNotified[] array = clientAttachment.AccessGameObject().RequestInterfaces<ICarryNotified>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].OnCarryEnded(this);
			}
			MonoBehaviour monoBehaviour = clientAttachment as MonoBehaviour;
			if (monoBehaviour != null)
			{
				GameObject gameObject = clientAttachment.AccessGameObject();
				m_carryItemChangedCallback(gameObject, null);
				m_carriedObjects[(int)playerAttachTarget] = null;
				return gameObject;
			}
		}
		return null;
	}

	public GameObject TakeItem()
	{
		return TakeItem(PlayerAttachTarget.Default);
	}

	public void DestroyCarriedItem()
	{
		IClientAttachment clientAttachment = m_carriedObjects[0];
		MonoBehaviour monoBehaviour = clientAttachment as MonoBehaviour;
		if (monoBehaviour != null)
		{
			GameObject param = clientAttachment.AccessGameObject();
			UnityEngine.Object.DestroyObject(clientAttachment.AccessGameObject());
			m_carriedObjects[0] = null;
			m_carryItemChangedCallback(param, null);
		}
	}

	public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
	{
		m_carryItemChangedCallback = (VoidGeneric<GameObject, GameObject>)Delegate.Combine(m_carryItemChangedCallback, _callback);
	}

	public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
	{
		m_carryItemChangedCallback = (VoidGeneric<GameObject, GameObject>)Delegate.Remove(m_carryItemChangedCallback, _callback);
	}
}
