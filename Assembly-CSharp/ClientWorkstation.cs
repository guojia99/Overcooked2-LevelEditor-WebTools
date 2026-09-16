using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWorkstation : ClientSynchroniserBase
{
	private class Interacter
	{
		public TriggerCallback Component;

		public TriggerCallback.Callback Method;

		private float m_actionLength;

		private float m_actionTime;

		private float m_actionTimer;

		public Interacter(TriggerCallback _component, float _delay, float _duration, TriggerCallback.Callback _method)
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
			if (num >= m_actionLength)
			{
				num -= m_actionLength;
			}
			m_actionTimer = num;
		}
	}

	private Workstation m_workstation;

	private ClientInteractable m_interactable;

	private ClientAttachStation m_attachStation;

	private ParticleSystem m_chopPFXInstance;

	private List<Interacter> m_interacters = new List<Interacter>();

	private ClientWorkableItem m_item;

	public override EntityType GetEntityType()
	{
		return EntityType.Workstation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_workstation = (Workstation)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<ClientInteractable>();
		m_interactable.SetInteractionSuppressed(true);
		m_attachStation = base.gameObject.GetComponent<ClientAttachStation>();
		m_attachStation.RegisterAllowItemPickup(CanPickupItem);
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		if (m_workstation.m_chopPFX != null)
		{
			AttachStation component = base.gameObject.GetComponent<AttachStation>();
			GameObject gameObject = m_workstation.m_chopPFX.InstantiateOnParent(base.transform, false);
			gameObject.transform.localPosition = component.GetAttachPoint(gameObject).localPosition;
			m_chopPFXInstance = gameObject.RequireComponent<ParticleSystem>();
			m_chopPFXInstance.Stop();
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		WorkstationMessage workstationMessage = (WorkstationMessage)serialisable;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(workstationMessage.m_interactorHeader.m_uEntityID);
		GameObject interacter = ((entry == null) ? null : entry.m_GameObject);
		if (workstationMessage.m_interacting)
		{
			GameObject obj = EntitySerialisationRegistry.GetEntry(workstationMessage.m_itemHeader.m_uEntityID).m_GameObject;
			StartWorking(interacter, obj.RequireComponent<ClientWorkableItem>());
		}
		else
		{
			StopWorking(interacter);
		}
	}

	protected override void OnDestroy()
	{
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterAllowItemPickup(CanPickupItem);
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
		}
		base.OnDestroy();
	}

	private void StartWorking(GameObject _interacter, ClientWorkableItem _item)
	{
		m_item = _item;
		TriggerCallback callbackComponent = _interacter.RequestComponent<TriggerCallback>();
		if (callbackComponent != null)
		{
			AnimationEventData animationEventData = _interacter.RequireComponentRecursive<AnimationEventData>();
			float o_clipDuration;
			float o_triggerTime;
			animationEventData.GetTriggerData("Chop", "Impact", out o_clipDuration, out o_triggerTime);
			if (m_chopPFXInstance != null)
			{
				m_chopPFXInstance.transform.localPosition = m_attachStation.GetAttachPoint(_item.gameObject).localPosition;
				m_chopPFXInstance.Play();
			}
			Interacter interacter = new Interacter(callbackComponent, o_triggerTime, o_clipDuration, delegate
			{
				OnChop(callbackComponent.transform);
			});
			callbackComponent.RegisterCallback(m_workstation.m_chopTrigger, interacter.Method);
			m_interacters.Add(interacter);
			m_interactable.SetStickyInteractionCallback(InteractionIsSticky);
		}
	}

	public bool InteractionIsSticky()
	{
		return m_interacters.Count == 0 || m_item != null;
	}

	public bool IsBeingUsed()
	{
		return m_interacters.Count > 0;
	}

	private void OnChop(Transform _interacter)
	{
		if ((bool)m_item)
		{
			m_item.DoWork(m_attachStation, _interacter.gameObject);
			PlayerIDProvider playerIDProvider = _interacter.gameObject.RequestComponent<PlayerIDProvider>();
			if (playerIDProvider != null)
			{
				GameUtils.TriggerNXRumble(playerIDProvider.GetID(), GameOneShotAudioTag.Chop);
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_interacters.Count != 0)
		{
			for (int i = 0; i < m_interacters.Count; i++)
			{
				Interacter interacter = m_interacters[i];
				float deltaTime = TimeManager.GetDeltaTime(interacter.Component.gameObject);
				interacter.Update(deltaTime);
			}
		}
	}

	public void StopWorking(GameObject _interacter)
	{
		TriggerCallback callbackComponent = ((!(_interacter != null)) ? null : _interacter.RequestComponent<TriggerCallback>());
		Interacter interacter = m_interacters.Find((Interacter x) => x.Component == callbackComponent);
		interacter.Component.UnregisterCallback(m_workstation.m_chopTrigger, interacter.Method);
		m_interacters.Remove(interacter);
		if (m_chopPFXInstance != null)
		{
			m_chopPFXInstance.Stop();
		}
		if (m_interacters.Count == 0)
		{
			m_item = null;
			m_interactable.SetStickyInteractionCallback(null);
		}
	}

	private bool CanPickupItem()
	{
		if (base.enabled && (bool)m_item)
		{
			return m_item.HasFinished() || m_item.GetProgress() == 0f;
		}
		return true;
	}

	private void OnItemAdded(IClientAttachment _iHoldable)
	{
		ClientWorkableItem clientWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ClientWorkableItem>();
		if ((bool)clientWorkableItem)
		{
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void OnItemRemoved(IClientAttachment _iHoldable)
	{
		ClientWorkableItem clientWorkableItem = _iHoldable.AccessGameObject().RequestComponent<ClientWorkableItem>();
		if ((bool)clientWorkableItem)
		{
			m_interactable.SetInteractionSuppressed(true);
		}
	}
}
