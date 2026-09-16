using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientInteractable : ClientSynchroniserBase
{
	private Generic<bool> m_stickyInteractionCallback;

	private Interactable m_interactable;

	private bool m_interactionSuppressed;

	private List<GameObject> m_interacters = new List<GameObject>();

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

	public virtual bool InteractionIsSticky()
	{
		return m_stickyInteractionCallback == null || m_stickyInteractionCallback();
	}

	public void SetStickyInteractionCallback(Generic<bool> _callback)
	{
		m_stickyInteractionCallback = _callback;
	}

	public virtual bool CanInteract(GameObject _interacter)
	{
		return (m_interacters.Count == 0 || m_interactable.m_allowMultipleInteracters || m_interacters.Contains(_interacter)) && !m_interactionSuppressed && base.enabled && m_interactable.enabled;
	}

	public void SetInteractionSuppressed(bool _suppressed)
	{
		m_interactionSuppressed = _suppressed;
	}

	public void AddInteractor(GameObject _interactor)
	{
		m_interacters.Add(_interactor);
	}

	public void RemoveInteractor(GameObject _interactor)
	{
		m_interacters.Remove(_interactor);
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
