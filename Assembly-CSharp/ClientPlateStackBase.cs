using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlateStackBase : ClientSynchroniserBase
{
	protected PlateStackBase m_plateStack;

	private GenericVoid<GameObject> m_plateAdded = delegate
	{
	};

	private GenericVoid<GameObject> m_plateRemoved = delegate
	{
	};

	protected ClientStack m_stack;

	public void RegisterOnPlateAdded(GenericVoid<GameObject> _added)
	{
		m_plateAdded = (GenericVoid<GameObject>)Delegate.Combine(m_plateAdded, _added);
	}

	public void UnregisterOnPlateAdded(GenericVoid<GameObject> _added)
	{
		m_plateAdded = (GenericVoid<GameObject>)Delegate.Remove(m_plateAdded, _added);
	}

	public void RegisterOnPlateRemoved(GenericVoid<GameObject> _removed)
	{
		m_plateRemoved = (GenericVoid<GameObject>)Delegate.Combine(m_plateRemoved, _removed);
	}

	public void UnregisterOnPlateRemoved(GenericVoid<GameObject> _removed)
	{
		m_plateRemoved = (GenericVoid<GameObject>)Delegate.Remove(m_plateRemoved, _removed);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PlateStack;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_plateStack = (PlateStackBase)synchronisedObject;
		m_stack = base.gameObject.RequireComponent<ClientStack>();
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_plateStack.m_platePrefab, PlateSpawned);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PlateRemoved();
	}

	protected virtual void PlateSpawned(GameObject _object)
	{
		m_plateAdded(_object);
	}

	protected virtual void PlateRemoved()
	{
		m_plateRemoved(null);
	}

	protected void NotifyPlateAdded(GameObject _plate)
	{
		m_plateAdded(_plate);
	}

	protected void NotifyPlateRemoved(GameObject _plate)
	{
		m_plateRemoved(_plate);
	}

	public int GetCount()
	{
		if (m_stack != null)
		{
			return m_stack.GetSize();
		}
		return 0;
	}
}
