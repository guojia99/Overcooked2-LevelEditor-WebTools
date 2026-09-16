using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientSwitchStation : ClientSynchroniserBase
{
	private SwitchStation m_switchStation;

	private ClientInteractable m_interactable;

	private ClientAttachStation m_attachStation;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_switchStation = (SwitchStation)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<ClientInteractable>();
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(AllowItemPlacement);
	}

	private void OnItemAdded(IClientAttachment _attachment)
	{
		if (m_interactable != null)
		{
			m_interactable.enabled = false;
		}
	}

	private void OnItemRemoved(IClientAttachment _attachment)
	{
		if (m_interactable != null)
		{
			m_interactable.enabled = true;
		}
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return _context.m_source != PlacementContext.Source.Player;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
			m_attachStation.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
	}
}
