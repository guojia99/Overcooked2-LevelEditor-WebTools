using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerWithCooldown : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerWithCooldown m_triggerWithCooldownBase;

	private float m_timeSinceLastTrigger;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerWithCooldownBase = (TriggerWithCooldown)synchronisedObject;
		m_timeSinceLastTrigger = m_triggerWithCooldownBase.m_Cooldown;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		m_timeSinceLastTrigger += TimeManager.GetDeltaTime(base.gameObject);
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_triggerWithCooldownBase.m_InTrigger && m_timeSinceLastTrigger >= m_triggerWithCooldownBase.m_Cooldown)
		{
			base.gameObject.SendTrigger(m_triggerWithCooldownBase.m_OutTrigger);
			m_timeSinceLastTrigger = 0f;
		}
	}
}
