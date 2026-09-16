using UnityEngine;

public interface IAnticipateInteractionNotifications
{
	void OnInteractionAnticipationStart(InteractionType _type, GameObject _player);

	void OnInteractionAnticipationEnded(InteractionType _type, GameObject _player);
}
