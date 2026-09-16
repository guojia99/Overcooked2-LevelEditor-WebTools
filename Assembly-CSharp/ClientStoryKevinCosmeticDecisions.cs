using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientStoryKevinCosmeticDecisions : ClientSynchroniserBase
{
	private StoryKevinCosmeticDecisions m_cosmeticDecisions;

	private ClientInteractable m_interactable;

	private static readonly int m_PettingAnimHash = Animator.StringToHash("IsPetted");

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cosmeticDecisions = (StoryKevinCosmeticDecisions)synchronisedObject;
		m_interactable = base.gameObject.RequireComponent<ClientInteractable>();
	}

	private void Update()
	{
		if (m_cosmeticDecisions != null && m_cosmeticDecisions.m_animator != null)
		{
			bool value = m_interactable != null && m_interactable.InteractorCount() > 0;
			m_cosmeticDecisions.m_animator.SetBool(m_PettingAnimHash, value);
		}
	}
}
