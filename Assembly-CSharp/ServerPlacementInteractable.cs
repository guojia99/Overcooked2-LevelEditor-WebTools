using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlacementInteractable : ServerSynchroniserBase, IHandlePlacement, IBaseHandlePlacement
{
	private VoidGeneric<GameObject, Vector2> m_triggerCallbacks;

	private List<Generic<bool, GameObject>> m_canInteractCallbacks = new List<Generic<bool, GameObject>>();

	private bool m_interactionSuppressed;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		if (m_interactionSuppressed)
		{
			return false;
		}
		for (int i = 0; i < m_canInteractCallbacks.Count; i++)
		{
			if (!m_canInteractCallbacks[i](_carrier.AccessGameObject()))
			{
				return false;
			}
		}
		return true;
	}

	public int GetPlacementPriority()
	{
		return 1;
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		if (m_triggerCallbacks != null)
		{
			m_triggerCallbacks(_carrier.AccessGameObject(), _directionXZ);
		}
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public void RegisterTriggerCallback(VoidGeneric<GameObject, Vector2> _triggerCallback)
	{
		m_triggerCallbacks = (VoidGeneric<GameObject, Vector2>)Delegate.Combine(m_triggerCallbacks, _triggerCallback);
	}

	public void UnregisterTriggerCallback(VoidGeneric<GameObject, Vector2> _triggerCallback)
	{
		m_triggerCallbacks = (VoidGeneric<GameObject, Vector2>)Delegate.Remove(m_triggerCallbacks, _triggerCallback);
	}

	public void RegisterCanInteractCallback(Generic<bool, GameObject> _canInteractCallback)
	{
		m_canInteractCallbacks.Add(_canInteractCallback);
	}

	public void UnregisterCanInteractCallback(Generic<bool, GameObject> _canInteractCallback)
	{
		m_canInteractCallbacks.Remove(_canInteractCallback);
	}

	public void SetInteractionSurpressed(bool _surpressed)
	{
		m_interactionSuppressed = _surpressed;
	}
}
