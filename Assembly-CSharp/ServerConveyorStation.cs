using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerConveyorStation : ServerSynchroniserBase
{
	private ConveyorStation m_conveyor;

	private ConveyorStationMessage m_data = new ConveyorStationMessage();

	private ServerTabletopConveyenceReceiver m_selfReceiver;

	private ServerAttachStation m_attachStation;

	private GridManager m_gridManager;

	private GridIndex m_gridIndex;

	private IConveyenceReceiver m_adjacentReceiver;

	private CallbackBool m_conveyStateChanged = delegate
	{
	};

	private List<Generic<bool>> m_allowConveyCallbacks = new List<Generic<bool>>();

	private bool m_pendingAdjacentUpdate;

	private IAttachment m_responsibleForItem;

	private bool m_conveying;

	private IAttachment m_item;

	private bool m_itemBeingDestroyed;

	private IEnumerator m_conveyingAwayRoutine;

	public override EntityType GetEntityType()
	{
		return EntityType.ConveyorStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_attachStation = base.gameObject.RequireComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_selfReceiver = base.gameObject.RequireComponent<ServerTabletopConveyenceReceiver>();
		m_selfReceiver.RegisterRefreshedConveyToCallback(RefreshStartConveying);
		m_pendingAdjacentUpdate = true;
	}

	public void RegisterConveyStateChangedCallback(CallbackBool _callback)
	{
		m_conveyStateChanged = (CallbackBool)Delegate.Combine(m_conveyStateChanged, _callback);
	}

	public void UnregisterConveyStateChangedCallback(CallbackBool _callback)
	{
		m_conveyStateChanged = (CallbackBool)Delegate.Remove(m_conveyStateChanged, _callback);
	}

	public void RegisterAllowConveyCallback(Generic<bool> _allowConveyCallback)
	{
		m_allowConveyCallbacks.Add(_allowConveyCallback);
	}

	public void UnregisterAllowConveyCallback(Generic<bool> _allowConveyCallback)
	{
		m_allowConveyCallbacks.Remove(_allowConveyCallback);
	}

	private void Awake()
	{
		m_conveyor = base.gameObject.RequireComponent<ConveyorStation>();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		StaticGridLocation staticGridLocation = base.gameObject.RequireComponent<StaticGridLocation>();
		m_gridManager = staticGridLocation.AccessGridManager;
		m_gridIndex = staticGridLocation.GridIndex;
		UpdateAdjacentReceiver();
	}

	public override void UpdateSynchronising()
	{
		if (m_pendingAdjacentUpdate)
		{
			m_pendingAdjacentUpdate = false;
			UpdateAdjacentReceiver();
		}
	}

	private void OnItemAdded(IAttachment _attachment)
	{
		m_item = _attachment;
		m_itemBeingDestroyed = false;
		LimitedQuantityItem limitedQuantityItem = m_item.AccessGameObject().RequestComponent<LimitedQuantityItem>();
		if (limitedQuantityItem != null)
		{
			limitedQuantityItem.RegisterImpendingDestructionNotification(ImpendingDestruction);
		}
		m_responsibleForItem = m_item;
		StartCoroutine(DelayRefreshStartConveying());
	}

	private void ImpendingDestruction(GameObject _destroying)
	{
		m_itemBeingDestroyed = true;
	}

	private IEnumerator DelayRefreshStartConveying()
	{
		yield return null;
		RefreshStartConveying();
		m_selfReceiver.RefreshConveyTo();
	}

	private void OnItemRemoved(IAttachment _attachment)
	{
		IAttachment item = m_item;
		IAttachment responsibleForItem = m_responsibleForItem;
		LimitedQuantityItem limitedQuantityItem = m_item.AccessGameObject().RequestComponent<LimitedQuantityItem>();
		if (limitedQuantityItem != null)
		{
			limitedQuantityItem.UnregisterImpendingDestructionNotification(ImpendingDestruction);
		}
		m_itemBeingDestroyed = false;
		m_item = null;
		m_responsibleForItem = null;
		if (responsibleForItem != null)
		{
			EndConveyence();
		}
	}

	public void UpdateAdjacentReceiver()
	{
		if (null != m_gridManager)
		{
			SetAdjacentReceiver(GetAdjacentReceiver());
		}
	}

	private void SetAdjacentReceiver(IConveyenceReceiver _receiver)
	{
		if (m_adjacentReceiver != _receiver)
		{
			if (m_adjacentReceiver != null)
			{
				m_adjacentReceiver.UnregisterRefreshedConveyToCallback(RefreshStartConveying);
			}
			m_adjacentReceiver = _receiver;
			if (m_adjacentReceiver != null)
			{
				m_adjacentReceiver.RegisterRefreshedConveyToCallback(RefreshStartConveying);
			}
			StartCoroutine(DelayRefreshStartConveying());
		}
	}

	public bool CanConveyToAdjacent()
	{
		return m_adjacentReceiver != null && m_adjacentReceiver.CanConveyTo(m_item) && !m_allowConveyCallbacks.CallForResult(false);
	}

	public bool IsConveying()
	{
		return m_conveying;
	}

	private void SetIsConveying(bool _isConveying)
	{
		m_conveying = _isConveying;
		m_conveyStateChanged(m_conveying);
	}

	private void RefreshStartConveying()
	{
		if (CanConveyToAdjacent() && m_item != null && !m_itemBeingDestroyed && !m_selfReceiver.IsReceiving() && !m_conveying)
		{
			SetIsConveying(true);
			m_adjacentReceiver.InformStartingConveyToMe();
			m_data.m_receiverEntityID = (m_adjacentReceiver as ServerSynchroniserBase).GetEntityId();
			m_data.m_itemEntityID = (m_item as ServerSynchroniserBase).GetEntityId();
			float num = ClientTime.Time();
			m_data.m_arriveTime = num + 1f / GetConveySpeed();
			SendServerEvent(m_data);
			StartCoroutine(ConveyTo(m_adjacentReceiver));
		}
	}

	public IEnumerator ConveyTo(IConveyenceReceiver _receiver)
	{
		m_conveyingAwayRoutine = _receiver.ConveyToMe(this, m_item);
		yield return StartCoroutine(m_conveyingAwayRoutine);
		EndConveyence();
	}

	public void EndConveyence()
	{
		if (m_conveyingAwayRoutine != null)
		{
			StopCoroutine(m_conveyingAwayRoutine);
		}
		SetIsConveying(false);
		if (m_adjacentReceiver != null)
		{
			m_adjacentReceiver.InformEndingConveyToMe();
		}
		RefreshStartConveying();
		m_conveyingAwayRoutine = null;
	}

	public GameObject TakeResponsibilityForItem()
	{
		m_responsibleForItem = null;
		return m_attachStation.TakeItem();
	}

	public IConveyenceReceiver GetAdjacentReceiver()
	{
		GridIndex nextGridIndex = GetNextGridIndex();
		GameObject gridOccupant = m_gridManager.GetGridOccupant(nextGridIndex);
		if (gridOccupant != null)
		{
			return gridOccupant.RequestInterface<IConveyenceReceiver>();
		}
		return null;
	}

	public GridIndex GetNextGridIndex()
	{
		int num = (int)Mathf.Round(0f - base.transform.right.x);
		int num2 = (int)Mathf.Round(0f - base.transform.right.z);
		switch (m_conveyor.m_conveyanceDirectionXZ)
		{
		case ConveyorStation.XZDirection.Leftwards:
			return new GridIndex(m_gridIndex.X - num, m_gridIndex.Y, m_gridIndex.Z - num2);
		case ConveyorStation.XZDirection.Rightwards:
			return new GridIndex(m_gridIndex.X + num, m_gridIndex.Y, m_gridIndex.Z + num2);
		default:
			return new GridIndex(int.MaxValue, int.MaxValue, int.MaxValue);
		}
	}

	public float GetConveySpeed()
	{
		return m_conveyor.m_conveySpeed;
	}
}
