using UnityEngine;

public class ServerDirtyPlateStack : ServerPlateStackBase, IHandlePlacement, ICarryNotified, ISurfacePlacementNotified, IBaseHandlePlacement
{
	private ServerWashable m_washable;

	private ICarrier m_carrier;

	private ServerAttachStation m_attachStation;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		DirtyPlateStack dirtyPlateStack = (DirtyPlateStack)synchronisedObject;
		if (dirtyPlateStack.m_washedPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, dirtyPlateStack.m_washedPrefab);
			m_washable = base.gameObject.RequireComponent<ServerWashable>();
			m_washable.RegisterAllowWashCallback(AllowWash);
			m_washable.RegisterFinishedCallback(OnWashingFinished);
		}
		if (dirtyPlateStack.m_cleanPlatePrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, dirtyPlateStack.m_cleanPlatePrefab);
		}
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject obj = _carrier.InspectCarriedItem();
		DirtyPlateStack dirtyPlateStack = obj.RequestComponent<DirtyPlateStack>();
		if (dirtyPlateStack != null)
		{
			return dirtyPlateStack.GetPlatingStep() == m_plateStack.GetPlatingStep();
		}
		return false;
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject obj = _carrier.InspectCarriedItem();
		ServerStack serverStack = obj.RequireComponent<ServerStack>();
		for (int i = 0; i < serverStack.GetSize(); i++)
		{
			AddToStack();
		}
		_carrier.DestroyCarriedItem();
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public int GetPlacementPriority()
	{
		return 0;
	}

	public void OnCarryBegun(ICarrier _carrier)
	{
		if (_carrier as ServerPlayerAttachmentCarrier != null)
		{
			m_carrier = _carrier;
		}
	}

	public void OnCarryEnded(ICarrier _carrier)
	{
		m_carrier = null;
	}

	public void OnSurfacePlacement(ServerAttachStation _station)
	{
		m_attachStation = _station;
	}

	public void OnSurfaceDeplacement(ServerAttachStation _station)
	{
		m_attachStation = null;
	}

	public override void AddToStack()
	{
		base.AddToStack();
		if (m_washable != null)
		{
			Washable washable = base.gameObject.RequireComponent<Washable>();
			m_washable.SetDuration(washable.m_duration * m_stack.GetSize());
		}
	}

	private bool AllowWash()
	{
		return m_stack.GetSize() > 0;
	}

	private void OnWashingFinished()
	{
		IAttachment attachment = null;
		ServerUtensilRespawnBehaviour serverUtensilRespawnBehaviour = base.gameObject.RequestComponent<ServerUtensilRespawnBehaviour>();
		DirtyPlateStack dirtyPlateStack = m_plateStack as DirtyPlateStack;
		if (m_carrier != null && m_stack.GetSize() == 1)
		{
			GameObject obj = NetworkUtils.ServerSpawnPrefab(base.gameObject, dirtyPlateStack.m_cleanPlatePrefab);
			ServerUtensilRespawnBehaviour serverUtensilRespawnBehaviour2 = obj.RequestComponent<ServerUtensilRespawnBehaviour>();
			if (serverUtensilRespawnBehaviour != null && serverUtensilRespawnBehaviour2 != null)
			{
				serverUtensilRespawnBehaviour2.SetIdealRespawnLocation(serverUtensilRespawnBehaviour.GetIdealRespawnLocation());
			}
			attachment = obj.RequireInterface<IAttachment>();
		}
		else
		{
			GameObject obj2 = NetworkUtils.ServerSpawnPrefab(base.gameObject, dirtyPlateStack.m_washedPrefab);
			ServerUtensilRespawnBehaviour serverUtensilRespawnBehaviour3 = obj2.RequestComponent<ServerUtensilRespawnBehaviour>();
			if (serverUtensilRespawnBehaviour != null && serverUtensilRespawnBehaviour3 != null)
			{
				serverUtensilRespawnBehaviour3.SetIdealRespawnLocation(serverUtensilRespawnBehaviour.GetIdealRespawnLocation());
			}
			attachment = obj2.RequireInterface<IAttachment>();
			ServerPlateStackBase serverPlateStackBase = attachment.AccessGameObject().RequireComponent<ServerPlateStackBase>();
			for (int i = 0; i < m_stack.GetSize(); i++)
			{
				serverPlateStackBase.AddToStack();
			}
		}
		if (m_carrier != null || m_attachStation != null)
		{
			Vector3 localPosition = base.transform.localPosition;
			Quaternion localRotation = base.transform.localRotation;
			if (m_carrier != null)
			{
				ICarrier carrier = m_carrier;
				carrier.TakeItem();
				carrier.CarryItem(attachment.AccessGameObject());
			}
			else if (m_attachStation != null)
			{
				ServerAttachStation attachStation = m_attachStation;
				attachStation.TakeItem();
				attachStation.AddItem(attachment.AccessGameObject(), Vector2.up);
			}
			attachment.AccessGameObject().transform.localPosition = localPosition;
			attachment.AccessGameObject().transform.localRotation = localRotation;
		}
		else
		{
			attachment.AccessRigidbody().transform.SetPositionAndRotation(base.transform.position, base.transform.rotation);
		}
		NetworkUtils.DestroyObjectsRecursive(base.gameObject);
	}
}
