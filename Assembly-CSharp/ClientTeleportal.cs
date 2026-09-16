using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportal : ClientSynchroniserBase
{
	private Teleportal m_teleportal;

	private IClientTeleportalSender[] m_senders = new IClientTeleportalSender[0];

	private IClientTeleportalReceiver[] m_selfReceivers = new IClientTeleportalReceiver[0];

	private IClientTeleportalReceiver[] m_exitReceivers = new IClientTeleportalReceiver[0];

	private Animator m_animator;

	private Queue<IEnumerator> m_teleportRoutines = new Queue<IEnumerator>();

	private List<IEnumerator> m_receiveRoutines = new List<IEnumerator>();

	private CallbackBool m_canTeleportStateChanged = delegate
	{
	};

	private CallbackBool m_teleportStateChanged = delegate
	{
	};

	private bool m_teleporting;

	private bool m_canTeleport;

	public override EntityType GetEntityType()
	{
		return EntityType.Teleportal;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_teleportal = (Teleportal)synchronisedObject;
		m_senders = base.gameObject.RequestInterfaces<IClientTeleportalSender>();
		m_selfReceivers = base.gameObject.RequestInterfaces<IClientTeleportalReceiver>();
		for (int i = 0; i < m_selfReceivers.Length; i++)
		{
			m_selfReceivers[i].RegisterStartedTeleportCallback(OnReceiverStartedTeleport);
			m_selfReceivers[i].RegisterCanTeleportToCallback(() => !IsTeleporting() && !IsReceiving());
		}
		if (m_teleportal.m_exitPortal != null)
		{
			m_exitReceivers = m_teleportal.m_exitPortal.RequestInterfaces<IClientTeleportalReceiver>();
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TeleportalMessage teleportalMessage = (TeleportalMessage)serialisable;
		switch (teleportalMessage.m_msgType)
		{
		case TeleportalMessage.MsgType.PortalState:
			if (teleportalMessage.m_canTeleport != m_canTeleport)
			{
				m_canTeleport = teleportalMessage.m_canTeleport;
				m_canTeleportStateChanged(teleportalMessage.m_canTeleport);
			}
			if (teleportalMessage.m_isTeleporting != m_teleporting)
			{
				m_teleporting = teleportalMessage.m_isTeleporting;
				m_teleportStateChanged(teleportalMessage.m_isTeleporting);
			}
			break;
		case TeleportalMessage.MsgType.TeleportStart:
		{
			IClientTeleportable clientTeleportable2 = teleportalMessage.m_object.RequireInterface<IClientTeleportable>();
			IEnumerator item2 = TeleportFrom(clientTeleportable2, teleportalMessage.m_clientSender, teleportalMessage.m_clientReceiver);
			m_teleportRoutines.Enqueue(item2);
			break;
		}
		case TeleportalMessage.MsgType.TeleportEnd:
		{
			IClientTeleportable clientTeleportable = teleportalMessage.m_object.RequireInterface<IClientTeleportable>();
			IEnumerator item = TeleportTo(clientTeleportable, teleportalMessage.m_clientReceiver, teleportalMessage.m_clientSender);
			m_receiveRoutines.Add(item);
			break;
		}
		}
	}

	public void RegisterCanTeleportChangedCallback(CallbackBool _callback)
	{
		m_canTeleportStateChanged = (CallbackBool)Delegate.Combine(m_canTeleportStateChanged, _callback);
	}

	public void UnregisterCanTeleportChangedCallback(CallbackBool _callback)
	{
		m_canTeleportStateChanged = (CallbackBool)Delegate.Remove(m_canTeleportStateChanged, _callback);
	}

	public void RegisterTeleportStateChangedCallback(CallbackBool _callback)
	{
		m_teleportStateChanged = (CallbackBool)Delegate.Combine(m_teleportStateChanged, _callback);
	}

	public void UnregisterTeleportStateChangedCallback(CallbackBool _callback)
	{
		m_teleportStateChanged = (CallbackBool)Delegate.Remove(m_teleportStateChanged, _callback);
	}

	public bool CanTeleport(IClientTeleportable _object)
	{
		return m_canTeleport;
	}

	public bool IsTeleporting()
	{
		return m_teleporting;
	}

	public bool IsReceiving()
	{
		int num = m_selfReceivers.FindIndex_Generic((int x, IClientTeleportalReceiver y) => y.IsReceiving());
		return num >= 0;
	}

	private void Awake()
	{
		m_teleportal = base.gameObject.GetComponent<Teleportal>();
		m_animator = base.gameObject.GetComponent<Animator>();
	}

	public override void UpdateSynchronising()
	{
		if (m_teleportRoutines.Count > 0)
		{
			IEnumerator enumerator = m_teleportRoutines.Peek();
			if (enumerator == null || !enumerator.MoveNext())
			{
				m_teleportRoutines.Dequeue();
			}
		}
		if (m_receiveRoutines.Count > 0)
		{
			Predicate<IEnumerator> match = (IEnumerator _routine) => !_routine.MoveNext();
			m_receiveRoutines.RemoveAll(match);
		}
	}

	private IEnumerator TeleportFrom(IClientTeleportable _object, IClientTeleportalSender _sender, IClientTeleportalReceiver _receiver)
	{
		while (_object.IsTeleporting() || !_object.CanTeleport(this))
		{
			yield return null;
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.TeleportIn, base.gameObject.layer);
		_object.StartTeleportFrom(_sender, _receiver);
		IEnumerator routine = _sender.TeleportFromMe(this, _receiver, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		_object.EndTeleportFrom(_sender, _receiver);
	}

	private IEnumerator TeleportTo(IClientTeleportable _object, IClientTeleportalReceiver _receiver, IClientTeleportalSender _sender)
	{
		while (!_object.IsTeleported() || !_object.CanTeleport(this) || !_receiver.CanTeleportTo(_object))
		{
			yield return null;
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.TeleportOut, base.gameObject.layer);
		_object.StartTeleportTo(_receiver, _sender);
		IEnumerator routine = _receiver.TeleportToMe(this, _sender, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		_object.EndTeleportTo(_receiver, _sender);
	}

	private void OnReceiverStartedTeleport(IClientTeleportable _object)
	{
	}
}
