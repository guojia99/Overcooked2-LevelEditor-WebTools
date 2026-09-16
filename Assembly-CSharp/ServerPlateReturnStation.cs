using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlateReturnStation : ServerSynchroniserBase
{
	private PlateReturnStation m_returnStation;

	private bool m_isDirtyReturn;

	private bool m_pendingInitialise;

	private ServerAttachStation m_attachStation;

	private ServerPlateStackBase m_stack;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachStation = base.gameObject.GetComponent<ServerAttachStation>();
		m_attachStation.RegisterAllowItemPlacement(CanAddItem);
		m_attachStation.RegisterAllowItemPickup(CanRemoveItem);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_returnStation = (PlateReturnStation)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_returnStation.m_stackPrefab);
		m_pendingInitialise = true;
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterAllowItemPlacement(CanAddItem);
			m_attachStation.UnregisterAllowItemPickup(CanRemoveItem);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
		}
	}

	public override void UpdateSynchronising()
	{
		if (!m_pendingInitialise)
		{
			return;
		}
		m_pendingInitialise = false;
		if (m_returnStation.m_startingPlateNumber > 0)
		{
			CreateStack();
			for (int i = 0; i < m_returnStation.m_startingPlateNumber; i++)
			{
				m_stack.AddToStack();
			}
		}
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		if (_context.m_source == PlacementContext.Source.Player)
		{
			return false;
		}
		if (m_stack != null)
		{
			return false;
		}
		if (m_attachStation.HasItem())
		{
			return false;
		}
		return true;
	}

	private bool CanRemoveItem()
	{
		return m_stack == null || m_stack.GetSize() > 0;
	}

	private void OnItemRemoved(IAttachment _iHoldable)
	{
		if (m_stack != null && _iHoldable.AccessGameObject() == m_stack.gameObject)
		{
			m_stack = null;
		}
	}

	public bool CanReturnPlate()
	{
		GameObject gameObject = m_attachStation.InspectItem();
		return gameObject == null || (m_stack != null && gameObject == m_stack.gameObject);
	}

	public void ReturnPlate()
	{
		if (m_stack == null)
		{
			CreateStack();
		}
		m_stack.AddToStack();
	}

	private void CreateStack()
	{
		GameObject gameObject = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_returnStation.m_stackPrefab);
		m_attachStation.AddItem(gameObject, base.transform.rotation * Vector2.up);
		m_stack = gameObject.RequireInterface<ServerPlateStackBase>();
	}

	public PlatingStepData GetPlatingStep()
	{
		if (m_returnStation.m_stackPrefab != null)
		{
			PlateStackBase plateStackBase = m_returnStation.m_stackPrefab.RequestComponent<PlateStackBase>();
			if (plateStackBase != null)
			{
				return plateStackBase.GetPlatingStep();
			}
		}
		return null;
	}
}
