using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAutoWorkstation : ServerSynchroniserBase, ITriggerReceiver
{
	private class WorkAction
	{
		public CallbackVoid m_callback;

		private float m_actionLength;

		private float m_actionTime;

		private float m_actionTimer;

		public WorkAction(float _delay, float _duration, CallbackVoid _callback)
		{
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
			m_actionTimer = num;
		}

		public void Reset()
		{
			m_actionTimer = 0f;
		}

		public bool IsFinished()
		{
			return m_actionTimer >= m_actionLength;
		}
	}

	private AutoWorkstation m_autoWorkstation;

	private AutoWorkstationMessage m_data = new AutoWorkstationMessage();

	private WorkAction m_workAction;

	private ServerInteractable m_interactable;

	private ServerAttachStation[] m_attachStations = new ServerAttachStation[0];

	private ServerWorkableItem[] m_workableItems = new ServerWorkableItem[0];

	private List<GameObject> m_interacters = new List<GameObject>();

	public override EntityType GetEntityType()
	{
		return EntityType.AutoWorkstation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_autoWorkstation = (AutoWorkstation)synchronisedObject;
		m_interactable = base.gameObject.GetComponent<ServerInteractable>();
		if (m_interactable != null)
		{
			m_interactable.RegisterCallbacks(OnInteracterAdded, OnInteracterRemoved);
			m_interactable.SetInteractionSuppressed(true);
		}
		m_attachStations = base.gameObject.RequestComponentsInImmediateChildren<ServerAttachStation>();
		m_workableItems = new ServerWorkableItem[m_attachStations.Length];
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			int index = i;
			m_attachStations[i].RegisterOnItemAdded(delegate(IAttachment x)
			{
				OnItemAdded(x, index);
			});
			m_attachStations[i].RegisterOnItemRemoved(delegate(IAttachment x)
			{
				OnItemRemovedItem(x, index);
			});
			m_attachStations[i].RegisterAllowItemPickup(() => CanPickupItem(index));
			m_attachStations[i].RegisterAllowItemPlacement((GameObject x, PlacementContext y) => CanPlaceItem(x, y, index));
		}
	}

	private void SynchroniseInteractionState(bool _active)
	{
		m_data.m_working = _active;
		if (_active)
		{
			Array.Resize(ref m_data.m_items, m_workableItems.Length);
			for (int i = 0; i < m_workableItems.Length; i++)
			{
				if (m_workableItems[i] != null)
				{
					EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_workableItems[i].gameObject);
					m_data.m_items[i] = entry;
				}
				else
				{
					m_data.m_items[i] = null;
				}
			}
		}
		else
		{
			for (int j = 0; j < m_data.m_items.Length; j++)
			{
				m_data.m_items[j] = null;
			}
		}
		SendServerEvent(m_data);
	}

	public override void OnDestroy()
	{
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			ServerAttachStation serverAttachStation = m_attachStations[i];
		}
		if (m_interactable != null)
		{
			m_interactable.UnregisterCallbacks(OnInteracterAdded, OnInteracterRemoved);
		}
		base.OnDestroy();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		for (int i = 0; i < m_workableItems.Length; i++)
		{
			if (m_workableItems[i] != null)
			{
				m_workableItems[i].enabled = true;
			}
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		for (int i = 0; i < m_workableItems.Length; i++)
		{
			if (m_workableItems[i] != null)
			{
				m_workableItems[i].enabled = false;
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_workAction == null)
		{
			return;
		}
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		m_workAction.Update(deltaTime);
		if (m_workAction.IsFinished())
		{
			if (m_interacters.Count != 0)
			{
				m_workAction.Reset();
			}
			else
			{
				StopWorking();
			}
		}
	}

	private void StartWorking()
	{
		bool flag = true;
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			if (m_workableItems[i] == null && m_attachStations[i] != null && m_attachStations[i].HasItem())
			{
				flag = false;
				break;
			}
		}
		AnimationEventData animationEventData = base.gameObject.RequireComponentRecursive<AnimationEventData>();
		float o_clipDuration;
		float o_triggerTime;
		if (flag)
		{
			animationEventData.GetTriggerData("Chop", "Impact", out o_clipDuration, out o_triggerTime);
			m_workAction = new WorkAction(o_triggerTime, o_clipDuration, OnChop);
		}
		else
		{
			animationEventData.GetTriggerData("Jam", "Impact", out o_clipDuration, out o_triggerTime);
			m_workAction = new WorkAction(o_triggerTime, o_clipDuration, OnJam);
		}
		SynchroniseInteractionState(true);
	}

	private void StopWorking()
	{
		m_workAction = null;
		SynchroniseInteractionState(false);
		if (m_autoWorkstation.m_workFinishedTrigger != string.Empty)
		{
			if (m_autoWorkstation.m_workFinishedTarget != null)
			{
				m_autoWorkstation.m_workFinishedTarget.SendTrigger(m_autoWorkstation.m_workFinishedTrigger);
			}
			else
			{
				base.gameObject.SendTrigger(m_autoWorkstation.m_workFinishedTrigger);
			}
		}
	}

	private void OnChop()
	{
		int num = m_attachStations.Length;
		for (int i = 0; i < num; i++)
		{
			ServerAttachStation station = m_attachStations[i];
			ServerWorkableItem serverWorkableItem = m_workableItems[i];
			if (serverWorkableItem != null)
			{
				serverWorkableItem.DoWork(station, base.gameObject, m_autoWorkstation.m_choppingPower);
			}
		}
	}

	private void OnJam()
	{
	}

	private bool CanPickupItem(int _stationIndex)
	{
		if (m_autoWorkstation.enabled && m_workAction != null)
		{
			ServerAttachStation serverAttachStation = m_attachStations[_stationIndex];
			if (serverAttachStation != null && serverAttachStation.HasItem())
			{
				ServerWorkableItem serverWorkableItem = m_workableItems[_stationIndex];
				if (serverWorkableItem != null)
				{
					return serverWorkableItem.HasFinished() || serverWorkableItem.GetProgress() == 0f;
				}
			}
		}
		return true;
	}

	private bool CanPlaceItem(GameObject _item, PlacementContext _context, int _stationIndex)
	{
		if (_context.m_source == PlacementContext.Source.Player)
		{
			return !m_autoWorkstation.enabled || m_workAction == null;
		}
		return true;
	}

	private void OnItemAdded(IAttachment _iHoldable, int _stationIndex)
	{
		ServerWorkableItem serverWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ServerWorkableItem>();
		if (serverWorkableItem != null)
		{
			m_workableItems[_stationIndex] = serverWorkableItem;
		}
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void OnItemRemovedItem(IAttachment _iHoldable, int _stationIndex)
	{
		ServerWorkableItem serverWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ServerWorkableItem>();
		if (serverWorkableItem != null)
		{
			m_workableItems[_stationIndex] = null;
		}
		int num = 0;
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			if (m_attachStations[i] != null && m_attachStations[i].HasItem())
			{
				num++;
			}
		}
		if (num == 0 && m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(true);
		}
	}

	private bool InteractionIsSticky()
	{
		for (int i = 0; i < m_workableItems.Length; i++)
		{
			if (m_workableItems[i] != null && !m_workableItems[i].HasFinished())
			{
				return true;
			}
		}
		return false;
	}

	private void OnInteracterAdded(GameObject _interacter, Vector2 _directionXZ)
	{
		if (m_interacters.Count == 0)
		{
			StartWorking();
			m_interactable.SetStickyInteractionCallback(InteractionIsSticky);
		}
		m_interacters.Add(_interacter);
	}

	public void OnInteracterRemoved(GameObject _interacter)
	{
		m_interacters.Remove(_interacter);
		if (m_interacters.Count == 0)
		{
			m_interactable.SetStickyInteractionCallback(null);
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (m_autoWorkstation.enabled && m_autoWorkstation.m_workTrigger == _trigger && m_workAction == null)
		{
			StartWorking();
		}
	}
}
