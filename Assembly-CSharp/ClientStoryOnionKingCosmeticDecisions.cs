using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientStoryOnionKingCosmeticDecisions : ClientSynchroniserBase
{
	private StoryOnionKingCosmeticDecisions m_cosmeticDecisions;

	private ClientTriggerDialogue m_triggerDialogue;

	private static readonly int m_Talking = Animator.StringToHash("IsTalking");

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cosmeticDecisions = (StoryOnionKingCosmeticDecisions)synchronisedObject;
		m_triggerDialogue = base.gameObject.RequireComponent<ClientTriggerDialogue>();
	}

	private void Update()
	{
		if (m_cosmeticDecisions != null && m_cosmeticDecisions.m_animator != null)
		{
			bool value = m_triggerDialogue != null && m_triggerDialogue.IsSpeaking();
			m_cosmeticDecisions.m_animator.SetBool(m_Talking, value);
		}
	}
}
