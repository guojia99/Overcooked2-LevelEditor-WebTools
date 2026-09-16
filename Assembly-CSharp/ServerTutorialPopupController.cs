using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTutorialPopupController : ServerSynchroniserBase
{
	private TutorialPopupController m_controller;

	private TutorialDismissMessage m_data = new TutorialDismissMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.TutorialPopup;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controller = (TutorialPopupController)synchronisedObject;
		m_controller.RegisterDismissCallback(OnTutorialDismissed);
	}

	private void OnTutorialDismissed()
	{
		SendServerEvent(m_data);
	}
}
