using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlayerAttachmentCarrier : ServerSynchroniserBase, IPlayerCarrier, ICarrier, ICarrierPlacement
{
	public class BlockPickup : IHandlePickup, IBaseHandlePickup
	{
		private ServerPlayerAttachmentCarrier m_carrier;

		public BlockPickup(ServerPlayerAttachmentCarrier _carrier)
		{
			m_carrier = _carrier;
		}

		public bool CanHandlePickup(ICarrier _carrier)
		{
			return false;
		}

		public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
		{
		}

		public int GetPickupPriority()
		{
			return int.MaxValue;
		}

		public void ForceDetach()
		{
			if (m_carrier.InspectCarriedItem() != null)
			{
				m_carrier.TakeItem();
			}
		}
	}

	public class BlockCatching : IHandleCatch
	{
		private ServerPlayerAttachmentCarrier m_carrier;

		public BlockCatching(ServerPlayerAttachmentCarrier _carrier)
		{
			m_carrier = _carrier;
		}

		public bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ)
		{
			return false;
		}

		public void HandleCatch(ICatchable _object, Vector2 _directionXZ)
		{
		}

		public void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ)
		{
		}

		public int GetCatchingPriority()
		{
			return int.MaxValue;
		}
	}

	private PlayerAttachmentCarrier m_attachmentCarrier;

	private IAttachment[] m_carriedObjects = new IAttachment[2];

	private VoidGeneric<GameObject, GameObject> m_carryItemChangedCallback = delegate
	{
	};

	private Generic<bool> m_true = () => true;

	private ChefCarryMessage m_ServerData = new ChefCarryMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachmentCarrier = (PlayerAttachmentCarrier)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.ChefCarry;
	}

	public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
	{
		m_carryItemChangedCallback = (VoidGeneric<GameObject, GameObject>)Delegate.Combine(m_carryItemChangedCallback, _callback);
	}

	public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
	{
		m_carryItemChangedCallback = (VoidGeneric<GameObject, GameObject>)Delegate.Remove(m_carryItemChangedCallback, _callback);
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

	public void DestroyCarriedItem()
	{
		IAttachment attachment = m_carriedObjects[0];
		GameObject param = attachment.AccessGameObject();
		NetworkUtils.DestroyObjectsRecursive(attachment.AccessGameObject());
		m_carriedObjects[0] = null;
		m_carryItemChangedCallback(param, null);
		m_ServerData.m_carriableItem = 0u;
		m_ServerData.m_playerAttachTarget = PlayerAttachTarget.Default;
		SendServerEvent(m_ServerData);
	}

	public void CarryItem(GameObject _object)
	{
		PlayerAttachTarget playerAttachTarget = PlayerAttachTarget.Default;
		IHandleAttachTarget handleAttachTarget = _object.RequestInterface<IHandleAttachTarget>();
		if (handleAttachTarget as MonoBehaviour != null)
		{
			playerAttachTarget = handleAttachTarget.PlayerAttachTarget;
		}
		ServerHandlePickupReferral component = _object.GetComponent<ServerHandlePickupReferral>();
		if ((bool)component && component.CanBeBlocked(this))
		{
			component.SetHandlePickupReferree(new BlockPickup(this));
		}
		ServerAttachmentCatchingProxy component2 = _object.GetComponent<ServerAttachmentCatchingProxy>();
		if ((bool)component2)
		{
			component2.SetHandleCatchingReferree(new BlockCatching(this));
		}
		IAttachment attachment = m_carriedObjects[(int)playerAttachTarget];
		IAttachment component3 = _object.GetComponent<IAttachment>();
		if (component3 != null)
		{
			m_carryItemChangedCallback((!(attachment as MonoBehaviour != null)) ? null : attachment.AccessGameObject(), _object);
			ServerLimitedQuantityItem serverLimitedQuantityItem = _object.RequestComponent<ServerLimitedQuantityItem>();
			if (null != serverLimitedQuantityItem)
			{
				serverLimitedQuantityItem.AddInvincibilityCondition(m_true);
			}
			m_carriedObjects[(int)playerAttachTarget] = component3;
			if (null != serverLimitedQuantityItem)
			{
				serverLimitedQuantityItem.RegisterImpendingDestructionNotification(OnAttachmentDestroyed);
			}
			component3.Attach(m_attachmentCarrier);
			ICarryNotified[] array = _object.RequestInterfaces<ICarryNotified>();
			foreach (ICarryNotified carryNotified in array)
			{
				carryNotified.OnCarryBegun(this);
			}
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(component3.AccessGameObject());
			uint uEntityID = entry.m_Header.m_uEntityID;
			m_ServerData.m_carriableItem = uEntityID;
			m_ServerData.m_playerAttachTarget = playerAttachTarget;
			SendServerEvent(m_ServerData);
		}
	}

	public GameObject TakeItem(PlayerAttachTarget playerAttachTarget)
	{
		IAttachment attachment = m_carriedObjects[(int)playerAttachTarget];
		ServerHandlePickupReferral component = attachment.AccessGameObject().GetComponent<ServerHandlePickupReferral>();
		if ((bool)component)
		{
			component.SetHandlePickupReferree(null);
		}
		attachment.Detach();
		ServerLimitedQuantityItem serverLimitedQuantityItem = attachment.AccessGameObject().RequestComponent<ServerLimitedQuantityItem>();
		if (null != serverLimitedQuantityItem)
		{
			serverLimitedQuantityItem.RemoveInvincibilityCondition(m_true);
			serverLimitedQuantityItem.Touch();
		}
		m_ServerData.m_carriableItem = 0u;
		m_ServerData.m_playerAttachTarget = playerAttachTarget;
		GameObject gameObject = attachment.AccessGameObject();
		m_carryItemChangedCallback(gameObject, null);
		if (null != serverLimitedQuantityItem)
		{
			serverLimitedQuantityItem.UnregisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		m_carriedObjects[(int)playerAttachTarget] = null;
		SendServerEvent(m_ServerData);
		return gameObject;
	}

	public GameObject TakeItem()
	{
		return TakeItem(PlayerAttachTarget.Default);
	}

	private void OnAttachmentDestroyed(GameObject toBeDestroyed)
	{
		PlayerAttachTarget playerAttachTarget = PlayerAttachTarget.Default;
		IHandleAttachTarget handleAttachTarget = toBeDestroyed.RequestInterface<IHandleAttachTarget>();
		if (handleAttachTarget as MonoBehaviour != null)
		{
			playerAttachTarget = handleAttachTarget.PlayerAttachTarget;
		}
		IAttachment attachment = m_carriedObjects[(int)playerAttachTarget];
		IAttachment attachment2 = toBeDestroyed.RequestInterface<IAttachment>();
		if (attachment != null && attachment2 == attachment)
		{
			LimitedQuantityItem component = attachment.AccessGameObject().GetComponent<LimitedQuantityItem>();
			if (null != component)
			{
				component.UnregisterImpendingDestructionNotification(OnAttachmentDestroyed);
			}
			TakeItem();
		}
	}

	public GameObject AccessGameObject()
	{
		return base.gameObject;
	}
}
