using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCutsceneController : ServerSynchroniserBase
{
	private CutsceneController m_controller;

	private readonly CutsceneStateMessage m_data = new CutsceneStateMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.Cutscene;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controller = (CutsceneController)synchronisedObject;
		m_controller.RegisterSkipCallback(OnCutsceneSkipped);
	}

	private void OnCutsceneSkipped()
	{
		SendServerEvent(m_data);
	}
}
