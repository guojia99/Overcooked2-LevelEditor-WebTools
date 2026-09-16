using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTeleportal : ServerSynchroniserBase
{
	private Teleportal m_teleportal;

	public static TeleportalMessage m_data = new TeleportalMessage();

	private const float c_facingTeleportAngleMax = 45f;

	private ITeleportalSender[] m_senders = new ITeleportalSender[0];

	private ITeleportalReceiver[] m_selfReceivers = new ITeleportalReceiver[0];

	private ITeleportalReceiver[] m_exitReceivers = new ITeleportalReceiver[0];

	private Collider m_collider;

	private TriggerRecorder m_triggerRecorder;

	private CallbackBool m_canTeleportStateChanged = delegate
	{
	};

	private CallbackBool m_teleportStateChanged = delegate
	{
	};

	private List<ITeleportable> m_recentlyTeleported = new List<ITeleportable>();

	private List<IEnumerator> m_receiveRoutines = new List<IEnumerator>();

	private IEnumerator m_teleportRoutine;

	private bool m_teleporting;

	private bool m_cooldown;

	public override EntityType GetEntityType()
	{
		return EntityType.Teleportal;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_teleportal = (Teleportal)synchronisedObject;
		m_senders = base.gameObject.RequestInterfaces<ITeleportalSender>();
		m_selfReceivers = base.gameObject.RequestInterfaces<ITeleportalReceiver>();
		for (int i = 0; i < m_selfReceivers.Length; i++)
		{
			m_selfReceivers[i].RegisterAllowTeleportCallback(() => !IsTeleporting() && !IsReceiving());
			m_selfReceivers[i].RegisterStartedTeleportCallback(OnReceiverStartedTeleport);
			m_selfReceivers[i].RegisterFinishedTeleportCallback(OnReceiverFinishedTeleport);
		}
		if (m_teleportal.m_exitPortal != null)
		{
			m_exitReceivers = m_teleportal.m_exitPortal.RequestInterfaces<ITeleportalReceiver>();
		}
	}

	private void SynchroniseTeleportalState()
	{
		if (MultiplayerController.IsSynchronisationActive())
		{
			m_data.Initialise_State(base.enabled, m_teleporting);
			SendServerEvent(m_data);
		}
	}

	private void SendTeleportFrom(ITeleportalSender _sender, ITeleportalReceiver _receiver, ITeleportable _object)
	{
		m_data.Initialise_StartTeleport(_sender, _receiver, _object);
		SendServerEvent(m_data);
	}

	private void SendTeleportTo(ITeleportalReceiver _receiver, ITeleportalSender _sender, ITeleportable _object)
	{
		m_data.Initialise_EndTeleport(_receiver, _sender, _object);
		SendServerEvent(m_data);
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

	public bool CanTeleport(ITeleportable _object)
	{
		if (!m_teleporting && !m_cooldown && (m_teleportal.m_allowImmediateReteleport || !m_recentlyTeleported.Contains(_object)))
		{
			ITeleportalSender senderForObject = GetSenderForObject(_object);
			ITeleportalReceiver receiverForObject = GetReceiverForObject(_object);
			return receiverForObject != null && senderForObject != null && senderForObject.CanTeleport(_object);
		}
		return false;
	}

	public bool IsTeleporting()
	{
		return m_teleporting;
	}

	public bool IsReceiving()
	{
		int num = m_selfReceivers.FindIndex_Generic((int x, ITeleportalReceiver y) => y.IsReceiving());
		return num >= 0;
	}

	private ITeleportalSender GetSenderForObject(ITeleportable _object)
	{
		Predicate<ITeleportalSender> matchFunction = (ITeleportalSender _sender) => _sender.CanHandleTeleport(_object);
		int num = m_senders.FindIndex_Predicate(matchFunction);
		return (num < 0) ? null : m_senders[num];
	}

	private ITeleportalReceiver GetReceiverForObject(ITeleportable _object)
	{
		Predicate<ITeleportalReceiver> matchFunction = (ITeleportalReceiver _receiver) => _receiver.CanHandleTeleport(_object);
		int num = m_exitReceivers.FindIndex_Predicate(matchFunction);
		return (num < 0) ? null : m_exitReceivers[num];
	}

	private void Awake()
	{
		m_teleportal = base.gameObject.GetComponent<Teleportal>();
		m_collider = base.gameObject.GetComponent<Collider>();
		m_triggerRecorder = base.gameObject.RequireComponent<TriggerRecorder>();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		m_canTeleportStateChanged(true);
		SynchroniseTeleportalState();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		m_canTeleportStateChanged(false);
		SynchroniseTeleportalState();
	}

	public override void UpdateSynchronising()
	{
		if (m_recentlyTeleported.Count > 0)
		{
			List<Collider> collisions = m_triggerRecorder.GetRecentCollisions();
			Predicate<ITeleportable> match = delegate(ITeleportable _object)
			{
				if (_object == null || (MonoBehaviour)_object == null)
				{
					return true;
				}
				GameObject obj = ((MonoBehaviour)_object).gameObject;
				return IsFacingPortal(obj) || !collisions.Exists((Collider x) => x.transform.IsChildOf(obj.transform));
			};
			m_recentlyTeleported.RemoveAll(match);
		}
		if (m_receiveRoutines.Count > 0)
		{
			Predicate<IEnumerator> match2 = (IEnumerator _teleportRoutine) => !_teleportRoutine.MoveNext();
			m_receiveRoutines.RemoveAll(match2);
		}
	}

	public void Teleport(ITeleportable _object)
	{
		ITeleportalSender senderForObject = GetSenderForObject(_object);
		ITeleportalReceiver receiverForObject = GetReceiverForObject(_object);
		_object.StartTeleport(senderForObject, receiverForObject);
		m_teleportRoutine = TeleportFrom(_object, senderForObject, receiverForObject);
		StartCoroutine(m_teleportRoutine);
		m_teleporting = true;
		m_teleportStateChanged(true);
		SynchroniseTeleportalState();
	}

	private IEnumerator TeleportFrom(ITeleportable _object, ITeleportalSender _sender, ITeleportalReceiver _receiver)
	{
		SendTeleportFrom(_sender, _receiver, _object);
		IEnumerator routine = _sender.TeleportFromMe(this, _receiver, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_receiveRoutines.Add(TeleportTo(_object, _sender, _receiver));
		EndTeleport(_object);
	}

	private IEnumerator TeleportTo(ITeleportable _object, ITeleportalSender _sender, ITeleportalReceiver _receiver)
	{
		IEnumerator routine = CoroutineUtils.TimerRoutine(m_teleportal.m_receiveDelay, base.gameObject.layer);
		while (routine.MoveNext())
		{
			yield return null;
		}
		while (!_receiver.CanTeleportTo(_object))
		{
			yield return null;
		}
		SendTeleportTo(_receiver, _sender, _object);
		routine = _receiver.TeleportToMe(this, _sender, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		_object.EndTeleport(_receiver, _sender);
	}

	public void EndTeleport(ITeleportable _object)
	{
		if (m_teleportRoutine != null)
		{
			StopCoroutine(m_teleportRoutine);
		}
		m_teleporting = false;
		m_teleportStateChanged(false);
		SynchroniseTeleportalState();
		StartCoroutine(PostTeleportCooldown());
		if (!m_teleportal.m_allowImmediateReteleport)
		{
			m_recentlyTeleported.Add(_object);
		}
		m_teleportRoutine = null;
	}

	private IEnumerator PostTeleportCooldown()
	{
		m_cooldown = true;
		IEnumerator timer = CoroutineUtils.TimerRoutine(m_teleportal.m_cooldownTime, base.gameObject.layer);
		while (timer.MoveNext())
		{
			yield return null;
		}
		m_cooldown = false;
	}

	private bool IsPointWithinConeXZ(Vector3 _point, Vector3 _coneOrigin, Vector3 _coneDirection, float _coneAngle)
	{
		Vector3 vector = (_coneOrigin - _point).WithY(0f).SafeNormalised(Vector3.zero);
		if (vector.sqrMagnitude > 0f)
		{
			float num = Vector3.Angle(_coneDirection, -vector);
			return num < _coneAngle;
		}
		return false;
	}

	private bool IsInTeleportArc(GameObject _object)
	{
		return IsPointWithinConeXZ(_object.transform.position, m_collider.bounds.center, base.transform.right, m_teleportal.m_teleportArc);
	}

	private bool IsFacingPortal(GameObject _object)
	{
		return IsPointWithinConeXZ(m_collider.bounds.center, _object.transform.position, _object.transform.forward, 45f);
	}

	private void OnReceiverStartedTeleport(ITeleportable _object)
	{
	}

	private void OnReceiverFinishedTeleport(ITeleportable _object)
	{
		StartCoroutine(PostTeleportCooldown());
		if (!m_teleportal.m_allowImmediateReteleport)
		{
			m_recentlyTeleported.Add(_object);
		}
	}

	private ITeleportable FindTeleportable(Transform _transform)
	{
		ITeleportable teleportable = _transform.gameObject.RequestInterface<ITeleportable>();
		if (teleportable != null && teleportable.CanTeleport(this))
		{
			return teleportable;
		}
		if (_transform.parent != null)
		{
			return FindTeleportable(_transform.parent);
		}
		return null;
	}

	private void OnTriggerStay(Collider collider)
	{
		if (m_teleportRoutine == null && !IsTeleporting() && !IsReceiving())
		{
			ITeleportable teleportable = FindTeleportable(collider.transform);
			if (teleportable != null && CanTeleport(teleportable) && IsInTeleportArc(collider.gameObject))
			{
				Teleport(teleportable);
			}
		}
	}
}
