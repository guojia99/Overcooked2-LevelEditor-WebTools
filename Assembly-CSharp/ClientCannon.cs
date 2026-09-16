using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCannon : ClientSynchroniserBase
{
	private Cannon m_cannon;

	private ClientCannonPlayerHandler m_playerHandler;

	private PilotRotation m_pilotRotation;

	private CannonMessage m_message = new CannonMessage();

	private GameObject m_loadedObject;

	private Vector3 m_exitPosition;

	private Quaternion m_exitRotation;

	private List<IEnumerator> m_launches = new List<IEnumerator>();

	private IClientCannonHandler[] m_handlers;

	private VoidGeneric<GameObject> m_onLoadedCallback;

	private VoidGeneric<GameObject> m_onUnloadedCallback;

	private VoidGeneric<GameObject> m_onLaunchedCallback;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = (Cannon)synchronisedObject;
		m_playerHandler = m_cannon.gameObject.RequireComponent<ClientCannonPlayerHandler>();
		m_handlers = GetComponents<IClientCannonHandler>();
		m_pilotRotation = base.gameObject.RequestComponentRecursive<PilotRotation>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.Cannon;
	}

	public void Load(GameObject _obj)
	{
		m_loadedObject = _obj;
		m_exitPosition = _obj.transform.position;
		m_exitRotation = _obj.transform.rotation;
		IClientCannonHandler handler = GetHandler(_obj);
		if (handler != null)
		{
			handler.Load(_obj);
		}
		_obj.transform.position = m_cannon.m_attachPoint.position;
		_obj.transform.rotation = m_cannon.m_attachPoint.rotation;
		_obj.transform.SetParent(m_cannon.m_attachPoint, true);
		if (m_onLoadedCallback != null)
		{
			m_onLoadedCallback(_obj);
		}
	}

	public IClientCannonHandler GetHandler(GameObject _obj)
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

	public IEnumerator Unload(GameObject _obj, Vector3 _exitPosition, Quaternion _exitRotation)
	{
		if (_obj != null)
		{
			_obj.transform.position = _exitPosition;
			_obj.transform.rotation = _exitRotation;
			_obj.transform.SetParent(null, true);
		}
		m_cannon.EndCannonRoutine(_obj);
		IClientCannonHandler handler = GetHandler(_obj);
		if (handler != null)
		{
			IEnumerator exit = handler.ExitCannonRoutine(_obj, _exitPosition, _exitRotation);
			while (exit.MoveNext())
			{
				yield return null;
			}
		}
		if (m_onUnloadedCallback != null)
		{
			m_onUnloadedCallback(_obj);
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		for (int i = 0; i < m_launches.Count; i++)
		{
			if (m_launches[i] == null || !m_launches[i].MoveNext())
			{
				m_launches.RemoveAt(i);
			}
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		m_message = (CannonMessage)serialisable;
		switch (m_message.m_state)
		{
		case CannonMessage.CannonState.Launched:
		{
			Vector3 eulerAngles = m_pilotRotation.m_transformToRotate.eulerAngles;
			eulerAngles.y = m_message.m_angle;
			m_pilotRotation.m_transformToRotate.eulerAngles = eulerAngles;
			m_launches.Add(LaunchProjectile(m_message.m_loadedObject, m_cannon.m_target));
			break;
		}
		case CannonMessage.CannonState.Load:
			Load(m_message.m_loadedObject);
			break;
		case CannonMessage.CannonState.Unload:
			m_launches.Add(Unload(m_message.m_loadedObject, m_cannon.m_exitPoint.position, m_cannon.m_exitPoint.rotation));
			break;
		}
	}

	private IEnumerator LaunchProjectile(GameObject _objectToLaunch, Transform _target)
	{
		IClientCannonHandler handler = GetHandler(_objectToLaunch);
		if (handler != null)
		{
			handler.Launch(_objectToLaunch);
		}
		_objectToLaunch.transform.SetParent(null, true);
		if (m_onLaunchedCallback != null)
		{
			m_onLaunchedCallback(_objectToLaunch);
		}
		IEnumerator animation = m_cannon.m_animation.Run(_objectToLaunch, _target);
		while (animation.MoveNext())
		{
			yield return null;
		}
		if (handler != null)
		{
			handler.Land(_objectToLaunch);
		}
		m_cannon.EndCannonRoutine(_objectToLaunch);
		if (handler != null)
		{
			IEnumerator exit = handler.ExitCannonRoutine(_objectToLaunch, _target.position, _target.rotation);
			while (exit.MoveNext())
			{
				yield return null;
			}
		}
	}

	public void RegisterOnLoadedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onLoadedCallback = (VoidGeneric<GameObject>)Delegate.Combine(m_onLoadedCallback, _callback);
	}

	public void UnregisterOnLoadedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onLoadedCallback = (VoidGeneric<GameObject>)Delegate.Remove(m_onLoadedCallback, _callback);
	}

	public void RegisterOnUnloadedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onUnloadedCallback = (VoidGeneric<GameObject>)Delegate.Combine(m_onUnloadedCallback, _callback);
	}

	public void UnregisterOnUnloadedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onUnloadedCallback = (VoidGeneric<GameObject>)Delegate.Remove(m_onUnloadedCallback, _callback);
	}

	public void RegisterOnLaunchedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onLaunchedCallback = (VoidGeneric<GameObject>)Delegate.Combine(m_onLaunchedCallback, _callback);
	}

	public void UnregisterOnLaunchedCallback(VoidGeneric<GameObject> _callback)
	{
		m_onLaunchedCallback = (VoidGeneric<GameObject>)Delegate.Remove(m_onLaunchedCallback, _callback);
	}
}
