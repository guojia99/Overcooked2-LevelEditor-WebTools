using UnityEngine;

public class ContextualPickupHoverIcon : ButtonHoverIcon, IAnticipateInteractionNotifications
{
	private int m_interactionCount;

	public void OnInteractionAnticipationStart(InteractionType _type, GameObject _player)
	{
		if (_type == InteractionType.Pickup)
		{
			m_interactionCount++;
			SetVisibility(true);
		}
	}

	public void OnInteractionAnticipationEnded(InteractionType _type, GameObject _player)
	{
		if (_type == InteractionType.Pickup)
		{
			m_interactionCount--;
			if (m_interactionCount == 0)
			{
				SetVisibility(false);
			}
		}
	}
}
