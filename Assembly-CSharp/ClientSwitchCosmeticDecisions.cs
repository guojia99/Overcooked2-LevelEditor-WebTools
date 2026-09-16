using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientSwitchCosmeticDecisions : ClientSynchroniserBase
{
	private SwitchCosmeticDecisions m_decisions;

	private Interactable m_interactable;

	private bool m_active;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_decisions = (SwitchCosmeticDecisions)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<Interactable>();
		m_active = m_interactable.enabled;
		UpdateVisuals(false);
	}

	private void Update()
	{
		if (!(m_interactable == null) && m_active != m_interactable.enabled)
		{
			UpdateVisuals(true);
		}
	}

	protected void UpdateVisuals(bool _playAudio)
	{
		m_active = m_interactable.enabled;
		if (m_active)
		{
			m_decisions.m_buttonBit.sharedMaterial = m_decisions.m_activeMaterial;
			if (_playAudio)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.SwitchOn, base.gameObject.layer);
			}
		}
		else
		{
			m_decisions.m_buttonBit.sharedMaterial = m_decisions.m_inactiveMaterial;
			if (_playAudio)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.SwitchOff, base.gameObject.layer);
			}
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		m_active = m_interactable != null && m_interactable.enabled;
	}
}
