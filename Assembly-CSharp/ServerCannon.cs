using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCannon : ServerSynchroniserBase, ITriggerReceiver
{
	private Cannon m_cannon;

	private CannonMessage m_message = new CannonMessage();

	private bool m_flying;

	private IServerCannonHandler[] m_handlers;

	private GenericVoid<bool> OnInteractionEnd;

	private GameObject m_loadedObject;

	private PilotRotation m_pilotRotation;

	private Generic<bool> m_readyToLaunchCallback;

	private bool m_readyToLaunch;

	private ServerInteractable m_buttonInteractable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = (Cannon)synchronisedObject;
		Cannon cannon = m_cannon;
		cannon.EndCannonRoutine = (GenericVoid<GameObject>)Delegate.Combine(cannon.EndCannonRoutine, new GenericVoid<GameObject>(EndCannonRoutine));
		m_handlers = m_cannon.GetComponents<IServerCannonHandler>();
		m_pilotRotation = base.gameObject.RequestComponentRecursive<PilotRotation>();
		m_buttonInteractable = m_cannon.m_button.RequireComponent<ServerInteractable>();
		m_buttonInteractable.RegisterTriggerCallbacks(ButtonInteractCallback);
	}

	public override void UpdateSynchronising()
	{
		if (m_loadedObject != null && !m_readyToLaunch && m_readyToLaunchCallback != null)
		{
			m_readyToLaunch = m_readyToLaunchCallback();
			if (m_readyToLaunch && m_cannon.m_button != null && !string.IsNullOrEmpty(m_cannon.m_enableTrigger))
			{
				m_cannon.m_button.SendTrigger(m_cannon.m_enableTrigger);
			}
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.Cannon;
	}

	public void ButtonInteractCallback(GameObject _interacter, Vector2 _directionXZ)
	{
		if (m_loadedObject != null && _interacter.RequestComponents<PlayerIDProvider>() != null && !m_flying)
		{
			ServerMessenger.Achievement(_interacter, 802);
		}
	}

	public void Load(GameObject _obj, GenericVoid<bool> EndInteractionCallback)
	{
		m_readyToLaunch = false;
		m_loadedObject = _obj;
		OnInteractionEnd = EndInteractionCallback;
		IServerCannonHandler handler = GetHandler(_obj);
		if (handler != null)
		{
			handler.Load(_obj);
		}
		m_message.m_loadedObject = _obj;
		m_message.m_state = CannonMessage.CannonState.Load;
		SendServerEvent(m_message);
	}

	public void Unload(GameObject _obj)
	{
		IServerCannonHandler handler = GetHandler(_obj);
		if (handler != null)
		{
			handler.Unload(_obj);
		}
		_obj.transform.SetPositionAndRotation(m_cannon.m_exitPoint.position, m_cannon.m_exitPoint.rotation);
		EndInteraction(true);
		m_message.m_state = CannonMessage.CannonState.Unload;
		m_message.m_loadedObject = _obj;
		SendServerEvent(m_message);
	}

	public IServerCannonHandler GetHandler(GameObject _obj)
	{
		for (int i = 0; i < m_handlers.Length; i++)
		{
			if (m_handlers[i].CanHandle(_obj))
			{
				return m_handlers[i];
			}
		}
		return null;
	}

	public void EndCannonRoutine(GameObject _obj)
	{
		m_flying = false;
		IServerCannonHandler handler = GetHandler(_obj);
		if (handler != null)
		{
			handler.ExitCannonRoutine(_obj);
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_cannon.m_launchTrigger && !m_flying)
		{
			EndInteraction(false);
			m_message.m_state = CannonMessage.CannonState.Launched;
			m_flying = true;
			m_message.m_loadedObject = m_loadedObject;
			if (m_pilotRotation != null)
			{
				m_message.m_angle = m_pilotRotation.m_transformToRotate.eulerAngles.y;
			}
			SendServerEvent(m_message);
		}
	}

	public bool IsFlying()
	{
		return m_flying;
	}

	private void EndInteraction(bool enableComponents)
	{
		if (m_cannon.m_button != null && !string.IsNullOrEmpty(m_cannon.m_disableTrigger))
		{
			m_cannon.m_button.SendTrigger(m_cannon.m_disableTrigger);
		}
		if (OnInteractionEnd != null)
		{
			OnInteractionEnd(enableComponents);
			OnInteractionEnd = null;
		}
	}

	public void SetReadyToLaunchCallback(Generic<bool> _callback)
	{
		m_readyToLaunchCallback = _callback;
	}
}
