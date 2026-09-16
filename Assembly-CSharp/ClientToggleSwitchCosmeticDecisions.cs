using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientToggleSwitchCosmeticDecisions : ClientSynchroniserBase
{
	private ToggleSwitchCosmeticDecisions m_cosmeticDecisions;

	private Interactable m_interactable;

	private ClientTriggerToggleOnAnimator m_toggleOnAnimator;

	private int m_stateParameterHash;

	private bool m_active;

	private bool m_toggleState;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cosmeticDecisions = (ToggleSwitchCosmeticDecisions)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<Interactable>();
		m_toggleOnAnimator = base.gameObject.RequireComponent<ClientTriggerToggleOnAnimator>();
		m_stateParameterHash = Animator.StringToHash(m_cosmeticDecisions.m_stateParameter);
		m_active = m_interactable.enabled;
		m_toggleState = m_toggleOnAnimator.CurrentState;
		UpdateVisuals(false);
	}

	private void Update()
	{
		if (!(m_interactable == null) && (m_active != m_interactable.enabled || m_toggleState != m_toggleOnAnimator.CurrentState))
		{
			m_active = m_interactable.enabled;
			m_toggleState = m_toggleOnAnimator.CurrentState;
			UpdateVisuals(true);
		}
	}

	protected void UpdateVisuals(bool _playAudio)
	{
		if (m_toggleState)
		{
			m_cosmeticDecisions.m_animator.SetBool(m_stateParameterHash, true);
		}
		else
		{
			m_cosmeticDecisions.m_animator.SetBool(m_stateParameterHash, false);
		}
	}
}
