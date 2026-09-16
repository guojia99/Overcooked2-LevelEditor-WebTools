using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerInteractable : ServerSynchroniserBase
{
	public delegate void BeginInteractCallback(GameObject _interacter, Vector2 _directionXZ);

	public delegate void EndInteractCallback(GameObject _interacter);

	private Interactable m_interactable;

	private List<GameObject> m_interacters = new List<GameObject>();

	private BeginInteractCallback m_addedInteracter = delegate
	{
	};

	private BeginInteractCallback m_triggerCallbacks = delegate
	{
	};

	private List<Generic<bool, GameObject>> m_canInteractCallbacks = new List<Generic<bool, GameObject>>();

	private EndInteractCallback m_removedInteracter = delegate
	{
	};

	private bool m_interactionSuppressed;

	private Generic<bool> m_stickyInteractionCallback;

	public bool UsePlacementButton
	{
		get
		{
			return m_interactable.m_usePlacementButton;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_interactable = (Interactable)synchronisedObject;
	}

	public virtual bool CanInteract(GameObject _interacter)
	{
		return base.enabled && m_interactable.enabled && !m_interactionSuppressed && !m_canInteractCallbacks.CallForResult(false, _interacter) && (m_interactable.m_allowMultipleInteracters || m_interacters.Count == 0 || m_interacters.Contains(_interacter));
	}

	public void SetInteractionSuppressed(bool _suppressed)
	{
		m_interactionSuppressed = _suppressed;
	}

	public void RegisterCallbacks(BeginInteractCallback _addedInteractor, EndInteractCallback _removedInteractor)
	{
		m_addedInteracter = (BeginInteractCallback)Delegate.Combine(m_addedInteracter, _addedInteractor);
		m_removedInteracter = (EndInteractCallback)Delegate.Combine(m_removedInteracter, _removedInteractor);
	}

	public void UnregisterCallbacks(BeginInteractCallback _addedInteractor, EndInteractCallback _removedInteractor)
	{
		m_addedInteracter = (BeginInteractCallback)Delegate.Remove(m_addedInteracter, _addedInteractor);
		m_removedInteracter = (EndInteractCallback)Delegate.Remove(m_removedInteracter, _removedInteractor);
	}

	public void RegisterCanInteractCallbacks(Generic<bool, GameObject> _trigger)
	{
		m_canInteractCallbacks.Add(_trigger);
	}

	public void UnregisterCanInteractCallbacks(Generic<bool, GameObject> _trigger)
	{
		m_canInteractCallbacks.Remove(_trigger);
	}

	public void RegisterTriggerCallbacks(BeginInteractCallback _trigger)
	{
		m_triggerCallbacks = (BeginInteractCallback)Delegate.Combine(m_triggerCallbacks, _trigger);
	}

	public void UnregisterTriggerCallbacks(BeginInteractCallback _trigger)
	{
		m_triggerCallbacks = (BeginInteractCallback)Delegate.Remove(m_triggerCallbacks, _trigger);
	}

	public void TriggerInteract(GameObject _interacter, Vector2 _directionXZ)
	{
		m_triggerCallbacks(_interacter, _directionXZ);
		if (m_interactable.m_onInteractImpulseTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_interactable.m_onInteractImpulseTrigger);
		}
	}

	public void BeginInteract(GameObject _interacter, Vector2 _directionXZ)
	{
		if (m_interacters.Count == 0 && m_interactable.m_onInteractStartedTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_interactable.m_onInteractStartedTrigger);
		}
		m_interacters.Add(_interacter);
		m_addedInteracter(_interacter, _directionXZ);
	}

	public void EndInteract(GameObject _interacter)
	{
		if (m_interacters.Count <= 1 && m_interactable.m_onInteractEndedTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_interactable.m_onInteractEndedTrigger);
		}
		m_interacters.Remove(_interacter);
		m_removedInteracter(_interacter);
	}

	public virtual bool InteractionIsSticky()
	{
		return m_stickyInteractionCallback != null && m_stickyInteractionCallback();
	}

	public void SetStickyInteractionCallback(Generic<bool> _callback)
	{
		m_stickyInteractionCallback = _callback;
	}

	public bool IsBeingInteractedWith()
	{
		return m_interacters.Count > 0;
	}

	public bool AllowMultipleInteractors()
	{
		return m_interactable.m_allowMultipleInteracters;
	}

	public int InteractorCount()
	{
		return m_interacters.Count;
	}
}
