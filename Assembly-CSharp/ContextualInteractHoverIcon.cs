using UnityEngine;

public class ContextualInteractHoverIcon : ButtonHoverIcon, IAnticipateInteractionNotifications
{
	private int m_interactionCount;

	public void OnInteractionAnticipationStart(InteractionType _type, GameObject _player)
	{
		if (_type == InteractionType.Interact)
		{
			m_interactionCount++;
			SetVisibility(true);
		}
	}

	public void OnInteractionAnticipationEnded(InteractionType _type, GameObject _player)
	{
		if (_type == InteractionType.Interact)
		{
			m_interactionCount--;
			if (m_interactionCount == 0)
			{
				SetVisibility(false);
			}
		}
	}
}
