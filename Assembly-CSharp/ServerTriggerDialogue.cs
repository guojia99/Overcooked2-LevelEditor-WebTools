using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerDialogue : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerDialogue m_triggerDialogue;

	private TriggerDialogueMessage m_data = new TriggerDialogueMessage();

	private IFlowController m_iFlowController;

	private bool m_isActive;

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerDialogue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDialogue = (TriggerDialogue)synchronisedObject;
		m_triggerDialogue.RegisterDialogueFinishedCallback(OnDialogueFinished);
		m_iFlowController = GameUtils.GetFlowController();
		if (m_iFlowController != null)
		{
			m_iFlowController.RoundActivatedCallback += OnRoundBegun;
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_iFlowController != null)
		{
			m_iFlowController.RoundActivatedCallback -= OnRoundBegun;
		}
	}

	private void StartDialogue()
	{
		if (!m_isActive)
		{
			m_isActive = true;
			SendServerEvent(m_data);
		}
	}

	public void OnDialogueFinished(DialogueController.Dialogue _dialogue)
	{
		if (_dialogue == m_triggerDialogue.m_dialogue)
		{
			m_isActive = false;
			base.gameObject.SendTrigger(m_triggerDialogue.m_onDialogueEndTrigger);
		}
	}

	private void OnRoundBegun()
	{
		if (m_triggerDialogue != null && m_triggerDialogue.m_startOnAwake)
		{
			StartDialogue();
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_triggerDialogue.m_trigger && m_triggerDialogue.isActiveAndEnabled)
		{
			StartDialogue();
		}
	}
}
