using UnityEngine;

public class ClientBackpack : ClientCarryableItem, IHandleAttachTarget
{
	private Backpack m_backpack;

	private ClientHandlePickupReferral m_pickupReferral;

	private IClientAttachment m_attachment;

	private ClientBackpackDispenser m_backpackDispenser;

	private int m_previousLayer;

	private int m_layerWhenAttached;

	public override PlayerAttachTarget PlayerAttachTarget
	{
		get
		{
			return PlayerAttachTarget.Back;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpack = synchronisedObject as Backpack;
		m_pickupReferral = base.gameObject.RequireComponent<ClientHandlePickupReferral>();
		m_pickupReferral.RegisterAllowReferralBlock(CanBlockReferral);
		m_attachment = base.gameObject.RequireInterface<IClientAttachment>();
		m_attachment.RegisterAttachChangedCallback(OnAttachmentChanged);
		m_backpackDispenser = base.gameObject.RequireComponent<ClientBackpackDispenser>();
		m_layerWhenAttached = LayerMask.NameToLayer("AttachedBackpack");
	}

	public bool CanBlockReferral(ICarrier _carrier)
	{
		return !m_attachment.IsAttached();
	}

	public bool CanHandleDispenserPickup(ICarrier _carrier)
	{
		return m_backpack.CanHandleDispenserPickup(_carrier);
	}

	private void OnAttachmentChanged(IParentable _parentable)
	{
		MonoBehaviour monoBehaviour = _parentable as MonoBehaviour;
		if (monoBehaviour != null)
		{
			if (monoBehaviour.gameObject.RequestInterface<ICarrierPlacement>() != null)
			{
				m_pickupReferral.SetHandlePickupReferree(m_backpackDispenser);
				if (m_backpack.m_usesSeparateColliders)
				{
					BoxCollider boxCollider = m_backpack.m_collider as BoxCollider;
					boxCollider.center = m_backpack.m_carriedColliderCenter;
					boxCollider.size = m_backpack.m_carriedColliderSize;
				}
			}
			m_previousLayer = base.gameObject.layer;
			base.gameObject.layer = m_layerWhenAttached;
		}
		else
		{
			m_pickupReferral.SetHandlePickupReferree(null);
			base.gameObject.layer = m_previousLayer;
			if (m_backpack.m_usesSeparateColliders)
			{
				BoxCollider boxCollider2 = m_backpack.m_collider as BoxCollider;
				boxCollider2.center = m_backpack.m_restingColliderCenter;
				boxCollider2.size = m_backpack.m_restingColliderSize;
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachment != null)
		{
			m_attachment.UnregisterAttachChangedCallback(OnAttachmentChanged);
		}
		if (m_pickupReferral != null)
		{
			m_pickupReferral.UnregisterAllowReferralBlock(CanBlockReferral);
		}
	}
}
