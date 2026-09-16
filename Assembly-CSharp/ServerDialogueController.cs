using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerDialogueController : ServerSynchroniserBase
{
	private DialogueController m_dialogueController;

	private DialogueStateMessage m_data = new DialogueStateMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.Dialogue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_dialogueController = (DialogueController)synchronisedObject;
		m_dialogueController.RegisterDialogueStateCallback(OnDialogueStateChanged);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		m_dialogueController.UnRegisterDialogueStateCallback(OnDialogueStateChanged);
	}

	private void OnDialogueStateChanged(DialogueController.Dialogue _dialogue, int _state)
	{
		m_data.Initialise(_dialogue, _state);
		SendServerEvent(m_data);
	}
}
