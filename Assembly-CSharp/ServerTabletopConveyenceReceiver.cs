using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTabletopConveyenceReceiver : ServerSynchroniserBase, IConveyenceReceiver
{
	private ServerAttachStation m_attachStation;

	private bool m_receiving;

	private bool m_itemJustRemoved;

	private IAttachment m_item;

	private CallbackVoid m_refreshedConveyToCallback = delegate
	{
	};

	private List<Generic<bool>> m_allowConveyToCallbacks = new List<Generic<bool>>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_attachStation = base.gameObject.RequireComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(AllowPlacement);
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	private bool AllowPlacement(GameObject _object, PlacementContext _context)
	{
		return !m_receiving;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_itemJustRemoved)
		{
			m_itemJustRemoved = false;
			RefreshConveyTo();
		}
	}

	private void OnItemAdded(IAttachment _attachment)
	{
		m_item = _attachment;
		ServerLimitedQuantityItem component = m_item.AccessGameObject().GetComponent<ServerLimitedQuantityItem>();
		if (null != component)
		{
			component.RegisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		m_itemJustRemoved = false;
		m_refreshedConveyToCallback();
	}

	private void OnItemRemoved(IAttachment _attachment)
	{
		ServerLimitedQuantityItem component = m_item.AccessGameObject().GetComponent<ServerLimitedQuantityItem>();
		if (null != component)
		{
			component.UnregisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		m_item = null;
		m_itemJustRemoved = true;
		m_refreshedConveyToCallback();
	}

	private void OnAttachmentDestroyed(GameObject toDeDestroyed)
	{
		IAttachment attachment = toDeDestroyed.RequestInterface<IAttachment>();
		if (m_item != null && attachment == m_item)
		{
			OnItemRemoved(attachment);
		}
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

	public IEnumerator ConveyToMe(ServerConveyorStation _priorConveyor, IAttachment _object)
	{
		ServerAttachStation attachStation1 = _priorConveyor.gameObject.RequireComponent<ServerAttachStation>();
		ServerAttachStation attachStation2 = m_attachStation;
		Transform attachPoint1 = attachStation1.GetAttachPoint(_object.AccessGameObject());
		Transform attachPoint2 = attachStation2.GetAttachPoint(_object.AccessGameObject());
		float prop = 0f;
		float speed = _priorConveyor.GetConveySpeed();
		bool aborted = false;
		ServerAttachStation.OnItemAdded addedDuringConveying = delegate
		{
			aborted = true;
		};
		ServerAttachStation.OnItemRemoved removedDuringConveying = delegate
		{
			aborted = true;
		};
		attachStation2.RegisterOnItemAdded(addedDuringConveying);
		attachStation1.RegisterOnItemRemoved(removedDuringConveying);
		do
		{
			prop = Mathf.Clamp01(prop + TimeManager.GetDeltaTime(base.gameObject) * speed);
			yield return null;
		}
		while (prop < 0.5f && !aborted);
		attachStation2.UnregisterOnItemAdded(addedDuringConveying);
		attachStation1.UnregisterOnItemRemoved(removedDuringConveying);
		if (!aborted)
		{
			_priorConveyor.TakeResponsibilityForItem();
			attachStation2.AddItem(_object.AccessGameObject(), _object.AccessGameObject().transform.forward.XZ());
			aborted = false;
			attachStation2.RegisterOnItemRemoved(removedDuringConveying);
			do
			{
				prop = Mathf.Clamp01(prop + TimeManager.GetDeltaTime(base.gameObject) * speed);
				yield return null;
			}
			while (prop < 1f && !aborted);
			attachStation2.UnregisterOnItemRemoved(removedDuringConveying);
		}
	}

	public bool CanConveyTo(IAttachment _itemToConvey)
	{
		if (_itemToConvey != null)
		{
			return !m_itemJustRemoved && !m_receiving && m_attachStation.CanAttachToSelf(_itemToConvey.AccessGameObject()) && !m_allowConveyToCallbacks.CallForResult(false);
		}
		return m_item == null && !m_itemJustRemoved && !m_receiving && !m_allowConveyToCallbacks.CallForResult(false);
	}

	public void RegisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
		m_refreshedConveyToCallback = (CallbackVoid)Delegate.Combine(m_refreshedConveyToCallback, _callback);
	}

	public void UnregisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
		m_refreshedConveyToCallback = (CallbackVoid)Delegate.Remove(m_refreshedConveyToCallback, _callback);
	}

	public void RefreshConveyTo()
	{
		m_refreshedConveyToCallback();
	}

	public void RegisterAllowConveyToCallback(Generic<bool> _allowConveyCallback)
	{
		m_allowConveyToCallbacks.Add(_allowConveyCallback);
	}

	public void UnregisterAllowConveyToCallback(Generic<bool> _allowConveyCallback)
	{
		m_allowConveyToCallbacks.Remove(_allowConveyCallback);
	}
}
