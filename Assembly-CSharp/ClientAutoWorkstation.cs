using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientAutoWorkstation : ClientSynchroniserBase
{
	private class WorkAction
	{
		public TriggerCallback Component;

		public TriggerCallback.Callback Method;

		private float m_actionLength;

		private float m_actionTime;

		private float m_actionTimer;

		public WorkAction(TriggerCallback _component, float _delay, float _duration, TriggerCallback.Callback _method)
		{
			Component = _component;
			Method = _method;
			m_actionLength = _duration;
			m_actionTime = _delay;
		}

		public void Update(float _dt)
		{
			float num = m_actionTimer + _dt;
			if (m_actionTimer < m_actionTime && num >= m_actionTime)
			{
				Method();
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

	private WorkAction m_workAction;

	private ClientInteractable m_interactable;

	private ClientAttachStation[] m_attachStations = new ClientAttachStation[0];

	private ClientWorkableItem[] m_workableItems = new ClientWorkableItem[0];

	public override EntityType GetEntityType()
	{
		return EntityType.AutoWorkstation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_autoWorkstation = (AutoWorkstation)synchronisedObject;
		m_interactable = base.gameObject.RequestComponent<ClientInteractable>();
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(true);
		}
		m_attachStations = base.gameObject.RequestComponentsInImmediateChildren<ClientAttachStation>();
		m_workableItems = new ClientWorkableItem[m_attachStations.Length];
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			int index = i;
			m_attachStations[i].RegisterOnItemAdded(delegate(IClientAttachment x)
			{
				OnItemAdded(x, index);
			});
			m_attachStations[i].RegisterOnItemRemoved(delegate(IClientAttachment x)
			{
				OnItemRemoved(x, index);
			});
			m_attachStations[i].RegisterAllowItemPickup(() => CanPickupItem(index));
			m_attachStations[i].RegisterAllowItemPlacement((GameObject x, PlacementContext y) => CanPlaceItem(x, y, index));
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		AutoWorkstationMessage autoWorkstationMessage = (AutoWorkstationMessage)serialisable;
		if (autoWorkstationMessage.m_working)
		{
			Array.Resize(ref m_workableItems, autoWorkstationMessage.m_items.Length);
			for (int i = 0; i < autoWorkstationMessage.m_items.Length; i++)
			{
				if (autoWorkstationMessage.m_items[i] != null && autoWorkstationMessage.m_items[i].m_GameObject != null)
				{
					m_workableItems[i] = autoWorkstationMessage.m_items[i].m_GameObject.RequireComponent<ClientWorkableItem>();
				}
			}
			StartWorking();
		}
		else
		{
			StopWorking();
		}
	}

	protected override void OnDestroy()
	{
		for (int i = 0; i < m_attachStations.Length; i++)
		{
			ClientAttachStation clientAttachStation = m_attachStations[i];
		}
		base.OnDestroy();
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
			if (m_interactable != null && m_interactable.InteractorCount() != 0)
			{
				m_workAction.Reset();
			}
			else
			{
				StopWorking();
			}
		}
	}

	private bool CanPickupItem(int _stationIndex)
	{
		if (base.enabled && m_workAction == null)
		{
			ClientAttachStation clientAttachStation = m_attachStations[_stationIndex];
			if (clientAttachStation != null && clientAttachStation.HasItem())
			{
				ClientWorkableItem clientWorkableItem = m_workableItems[_stationIndex];
				if (clientWorkableItem != null)
				{
					return clientWorkableItem.HasFinished() || clientWorkableItem.GetProgress() == 0f;
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

	private void OnItemAdded(IClientAttachment _iHoldable, int _stationIndex)
	{
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void OnItemRemoved(IClientAttachment _iHoldable, int _stationIndex)
	{
		ClientWorkableItem clientWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ClientWorkableItem>();
		if (!(clientWorkableItem != null))
		{
			return;
		}
		m_workableItems[_stationIndex] = null;
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
			if (m_workableItems[i] != null)
			{
				return true;
			}
		}
		return false;
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
		TriggerCallback triggerCallback = base.gameObject.RequestComponent<TriggerCallback>();
		if (triggerCallback != null)
		{
			AnimationEventData animationEventData = base.gameObject.RequireComponentRecursive<AnimationEventData>();
			float o_clipDuration;
			float o_triggerTime;
			if (flag)
			{
				animationEventData.GetTriggerData("Chop", "Impact", out o_clipDuration, out o_triggerTime);
				m_workAction = new WorkAction(triggerCallback, o_triggerTime, o_clipDuration, OnChop);
			}
			else
			{
				animationEventData.GetTriggerData("Jam", "Impact", out o_clipDuration, out o_triggerTime);
				m_workAction = new WorkAction(triggerCallback, o_triggerTime, o_clipDuration, OnJam);
			}
			triggerCallback.RegisterCallback("Impact", m_workAction.Method);
		}
		Animator animator = base.gameObject.RequestComponentInImmediateChildren<Animator>();
		if (animator != null)
		{
			animator.SetBool("IsChopping", true);
			animator.SetBool("IsJammed", !flag);
		}
	}

	private void StopWorking()
	{
		if (m_workAction != null)
		{
			TriggerCallback triggerCallback = base.gameObject.RequestComponent<TriggerCallback>();
			if (triggerCallback != null)
			{
				triggerCallback.UnregisterCallback("Impact", m_workAction.Method);
			}
			m_workAction = null;
		}
		for (int i = 0; i < m_workableItems.Length; i++)
		{
			m_workableItems[i] = null;
		}
		Animator animator = base.gameObject.RequestComponentInImmediateChildren<Animator>();
		if (animator != null)
		{
			animator.SetBool("IsChopping", false);
			animator.SetBool("IsJammed", false);
		}
	}

	private void OnChop()
	{
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		int num = m_attachStations.Length;
		for (int i = 0; i < num; i++)
		{
			ClientAttachStation station = m_attachStations[i];
			ClientWorkableItem clientWorkableItem = m_workableItems[i];
			if (clientWorkableItem != null)
			{
				clientWorkableItem.DoWork(station, base.gameObject, m_autoWorkstation.m_choppingPower);
			}
			if (overcookedAchievementManager != null)
			{
				overcookedAchievementManager.IncStat(702, 1f, ControlPadInput.PadNum.One);
			}
		}
	}

	private void OnJam()
	{
	}
}
