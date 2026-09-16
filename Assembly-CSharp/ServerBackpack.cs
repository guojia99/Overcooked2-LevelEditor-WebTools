using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerBackpack : ServerCarryableItem, IHandleAttachTarget
{
	private Backpack m_backpack;

	private ServerHandlePickupReferral m_pickupReferral;

	private IAttachment m_attachment;

	private ServerBackpackDispenser m_backpackDispenser;

	private ServerPhysicalAttachment m_physicalAttachment;

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
		m_pickupReferral = base.gameObject.RequireComponent<ServerHandlePickupReferral>();
		m_pickupReferral.RegisterAllowReferralBlock(CanBlockReferral);
		m_attachment = base.gameObject.RequireInterface<IAttachment>();
		m_attachment.RegisterAttachChangedCallback(OnAttachmentChanged);
		m_backpackDispenser = base.gameObject.RequireComponent<ServerBackpackDispenser>();
		m_physicalAttachment = base.gameObject.RequireComponent<ServerPhysicalAttachment>();
		Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	public override void StopSynchronising()
	{
		base.StopSynchronising();
		Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_physicalAttachment.ManualEnable();
		}
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
		if (monoBehaviour != null && monoBehaviour.gameObject.RequestInterface<ICarrier>() != null)
		{
			m_pickupReferral.SetHandlePickupReferree(m_backpackDispenser);
		}
		else
		{
			m_pickupReferral.SetHandlePickupReferree(null);
		}
	}

	public override void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		ServerPlayerAttachmentCarrier serverPlayerAttachmentCarrier = _carrier.AccessGameObject().RequestComponentRecursive<ServerPlayerAttachmentCarrier>();
		if (serverPlayerAttachmentCarrier != null && serverPlayerAttachmentCarrier.InspectCarriedItem(PlayerAttachTarget) == null)
		{
			IAttachment component = base.gameObject.GetComponent<IAttachment>();
			if (component.IsAttached())
			{
				component.Detach();
			}
			serverPlayerAttachmentCarrier.CarryItem(component.AccessGameObject());
		}
	}

	public override void OnDestroy()
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
