using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTabletopConveyenceReceiver : ClientSynchroniserBase, IClientConveyenceReceiver
{
	private ClientAttachStation m_attachStation;

	private bool m_receiving;

	private CallbackVoid m_refreshedConveyToCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(AllowPlacement);
		AttachStation attachStation = base.gameObject.RequireComponent<AttachStation>();
		attachStation.SetClientSidePredictionEnabled(true);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_attachStation)
		{
			m_attachStation.UnregisterAllowItemPlacement(AllowPlacement);
		}
	}

	private bool AllowPlacement(GameObject _object, PlacementContext _context)
	{
		return !m_receiving;
	}

	private void OnItemAdded(IClientAttachment _attachment)
	{
		m_refreshedConveyToCallback();
	}

	private void OnItemRemoved(IClientAttachment _attachment)
	{
		m_refreshedConveyToCallback();
	}

	public void InformStartingConveyToMe()
	{
		m_receiving = true;
		m_refreshedConveyToCallback();
	}

	public void InformEndingConveyToMe()
	{
		m_receiving = false;
		m_refreshedConveyToCallback();
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	public void RegisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
		m_refreshedConveyToCallback = (CallbackVoid)Delegate.Combine(m_refreshedConveyToCallback, _callback);
	}

	public void UnregisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
		m_refreshedConveyToCallback = (CallbackVoid)Delegate.Remove(m_refreshedConveyToCallback, _callback);
	}
}
