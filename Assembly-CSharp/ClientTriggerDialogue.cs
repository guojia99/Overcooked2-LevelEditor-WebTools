using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerDialogue : ClientSynchroniserBase
{
	private TriggerDialogue m_triggerDialogue;

	private Dictionary<PlayerControls, Suppressor> m_controlsSuppressor = new Dictionary<PlayerControls, Suppressor>();

	private ClientDialogueController m_dialogueController;

	private bool m_isSpeaking;

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerDialogue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDialogue = (TriggerDialogue)synchronisedObject;
		m_dialogueController = base.gameObject.RequireComponent<ClientDialogueController>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TriggerDialogueMessage triggerDialogueMessage = (TriggerDialogueMessage)serialisable;
		StartCoroutine(RunDialogueRoutine());
	}

	public bool IsSpeaking()
	{
		return m_isSpeaking;
	}

	private IEnumerator RunDialogueRoutine()
	{
		m_isSpeaking = true;
		SetControlsEnabled(m_triggerDialogue.m_canMoveDuringDialog);
		IEnumerator routine = null;
		routine = ((!(m_triggerDialogue.m_followObject != null)) ? m_dialogueController.StartDialogue(m_triggerDialogue.m_dialogue, m_triggerDialogue.m_anchor, m_triggerDialogue.m_pivot, m_triggerDialogue.m_rotation) : m_dialogueController.StartDialogue(m_triggerDialogue.m_dialogue, m_triggerDialogue.m_followObject));
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_dialogueController.Shutdown(m_triggerDialogue.m_dialogue);
		SetControlsEnabled(true);
		m_isSpeaking = false;
		m_triggerDialogue.OnDialogueFinished();
	}

	private void SetControlsEnabled(bool _enabled)
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
		for (int i = 0; i < array.Length; i++)
		{
			PlayerControls playerControls = array[i].RequireComponent<PlayerControls>();
			if (_enabled)
			{
				Suppressor value;
				if (m_controlsSuppressor.TryGetValue(playerControls, out value))
				{
					value.Release();
					m_controlsSuppressor.Remove(playerControls);
				}
			}
			else if (!m_controlsSuppressor.ContainsKey(playerControls))
			{
				m_controlsSuppressor.Add(playerControls, playerControls.Suppress(this));
			}
		}
	}
}
