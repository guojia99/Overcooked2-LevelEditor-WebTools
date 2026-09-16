using System;
using UnityEngine;

[RequireComponent(typeof(DialogueController))]
public class TriggerDialogue : MonoBehaviour
{
	[SerializeField]
	public DialogueController.Dialogue m_dialogue;

	[Header("Positioning")]
	[SerializeField]
	public Transform m_followObject;

	[SerializeField]
	public Vector2 m_anchor = new Vector2(0.5f, 0.5f);

	[SerializeField]
	public Vector2 m_pivot = new Vector2(0.5f, 0.5f);

	[SerializeField]
	public float m_rotation;

	[Header("Settings")]
	[SerializeField]
	public bool m_canMoveDuringDialog;

	[SerializeField]
	public bool m_startOnAwake;

	[SerializeField]
	public string m_trigger;

	[SerializeField]
	public string m_onDialogueEndTrigger;

	private GenericVoid<DialogueController.Dialogue> m_dialogueFinishedCallback = delegate
	{
	};

	public string[] DialogueScript
	{
		set
		{
			m_dialogue.DialogueScript = value;
		}
	}

	public bool AutoStart
	{
		set
		{
			m_startOnAwake = value;
		}
	}

	public void RegisterDialogueFinishedCallback(GenericVoid<DialogueController.Dialogue> _callback)
	{
		m_dialogueFinishedCallback = (GenericVoid<DialogueController.Dialogue>)Delegate.Combine(m_dialogueFinishedCallback, _callback);
	}

	public void UnregisterDialogueFinishedCallback(GenericVoid<DialogueController.Dialogue> _callback)
	{
		m_dialogueFinishedCallback = (GenericVoid<DialogueController.Dialogue>)Delegate.Remove(m_dialogueFinishedCallback, _callback);
	}

	public void OnDialogueFinished()
	{
		m_dialogueFinishedCallback(m_dialogue);
	}
}
