using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWorkstation : ServerSynchroniserBase
{
	private class Interacter
	{
		public GameObject m_object;

		public CallbackVoid m_callback;

		private float m_actionLength;

		private float m_actionTime;

		private float m_actionTimer;

		public Interacter(GameObject _object, float _delay, float _duration, CallbackVoid _callback)
		{
			m_object = _object;
			m_callback = _callback;
			m_actionLength = _duration;
			m_actionTime = _delay;
		}

		public void Update(float _dt)
		{
			float num = m_actionTimer + _dt;
			if (m_actionTimer < m_actionTime && num >= m_actionTime)
			{
				m_callback();
			}
			if (num >= m_actionLength)
			{
				num -= m_actionLength;
			}
			m_actionTimer = num;
		}
	}

	private Workstation m_workable;

	private WorkstationMessage m_data = new WorkstationMessage();

	private List<Interacter> m_interacters = new List<Interacter>();

	private ServerWorkableItem m_item;

	private ServerAttachStation m_attachStation;

	private ServerInteractable m_interactable;

	public override EntityType GetEntityType()
	{
		return EntityType.Workstation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_workable = (Workstation)synchronisedObject;
		m_attachStation = base.gameObject.GetComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemovedItem);
		m_attachStation.RegisterAllowItemPickup(CanPickupItem);
		m_interactable = base.gameObject.GetComponent<ServerInteractable>();
		m_interactable.RegisterCallbacks(OnInteracterAdded, OnInteracterRemoved);
		m_interactable.SetInteractionSuppressed(true);
	}

	private void SynchroniseInteractionState(Interacter _interactor, bool _active)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_interactor.m_object.gameObject);
		if (entry != null)
		{
			m_data.m_interacting = _active;
			m_data.m_interactor = entry;
			if (_active)
			{
				EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(m_item.gameObject);
				m_data.m_item = entry2;
			}
			else
			{
				m_data.m_item = null;
			}
			SendServerEvent(m_data);
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if ((bool)m_item)
		{
			m_item.enabled = true;
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if ((bool)m_item)
		{
			m_item.enabled = false;
		}
	}

	public override void OnDestroy()
	{
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterAllowItemPickup(CanPickupItem);
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemovedItem);
		}
		if (m_interactable != null)
		{
			m_interactable.UnregisterCallbacks(OnInteracterAdded, OnInteracterRemoved);
		}
		base.OnDestroy();
	}

	private bool CanPickupItem()
	{
		if (base.enabled && (bool)m_item)
		{
			return m_item.HasFinished() || m_item.GetProgress() == 0f;
		}
		return true;
	}

	private bool InteractionIsSticky()
	{
		return m_interacters.Count == 0 || (m_item != null && !m_item.HasFinished());
	}

	public override void UpdateSynchronising()
	{
		if (m_interacters.Count != 0)
		{
			for (int i = 0; i < m_interacters.Count; i++)
			{
				Interacter interacter = m_interacters[i];
				float deltaTime = TimeManager.GetDeltaTime(interacter.m_object);
				interacter.Update(deltaTime);
			}
		}
	}

	private void OnItemAdded(IAttachment _iHoldable)
	{
		ServerWorkableItem serverWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ServerWorkableItem>();
		if ((bool)serverWorkableItem)
		{
			m_item = serverWorkableItem;
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void OnItemRemovedItem(IAttachment _iHoldable)
	{
		ServerWorkableItem serverWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ServerWorkableItem>();
		if ((bool)serverWorkableItem)
		{
			m_interactable.SetInteractionSuppressed(true);
			m_item = null;
		}
	}

	private void OnInteracterAdded(GameObject _interacter, Vector2 _directionXZ)
	{
		AnimationEventData animationEventData = _interacter.RequireComponentRecursive<AnimationEventData>();
		float o_clipDuration;
		float o_triggerTime;
		animationEventData.GetTriggerData("Chop", "Impact", out o_clipDuration, out o_triggerTime);
		Interacter interacter = new Interacter(_interacter, o_triggerTime, o_clipDuration, delegate
		{
			OnChop(_interacter.transform);
		});
		m_interacters.Add(interacter);
		m_interactable.SetStickyInteractionCallback(InteractionIsSticky);
		SynchroniseInteractionState(interacter, true);
	}

	private void OnChop(Transform _interacter)
	{
		if ((bool)m_item)
		{
			Vector2 normalized = _interacter.forward.XZ().normalized;
			m_attachStation.RotateForDirection(normalized);
			m_item.DoWork(m_attachStation, _interacter.gameObject);
		}
	}

	public void OnInteracterRemoved(GameObject _interacter)
	{
		Interacter interacter = m_interacters.Find((Interacter x) => x.m_object == _interacter);
		m_interacters.Remove(interacter);
		if (m_interacters.Count == 0)
		{
			m_interactable.SetStickyInteractionCallback(null);
		}
		SynchroniseInteractionState(interacter, false);
	}
}
