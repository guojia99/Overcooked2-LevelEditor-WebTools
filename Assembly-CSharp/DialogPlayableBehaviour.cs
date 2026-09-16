using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class DialogPlayableBehaviour : PlayableBehaviour
{
	private DialogueController.Dialogue m_dialogue;

	private Transform m_followTarget;

	private Vector2 m_anchor = Vector2.zero;

	private Vector2 m_pivot = Vector2.zero;

	private float m_rotation;

	private DialogMixerBehaviour m_mixer;

	private ClientDialogueController m_controller;

	private IEnumerator m_dialogueRoutine;

	private bool m_finished;

	public DialogMixerBehaviour Mixer
	{
		set
		{
			m_mixer = value;
		}
	}

	public void Setup(DialogueController.Dialogue _dialogue, Transform _followTarget)
	{
		m_dialogue = _dialogue;
		m_followTarget = _followTarget;
	}

	public void Setup(DialogueController.Dialogue _dialogue, Vector2 _anchor, Vector2 _pivot, float _rotation = 0f)
	{
		m_dialogue = _dialogue;
		m_anchor = _anchor;
		m_pivot = _pivot;
		m_rotation = _rotation;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		if (m_mixer == null || !m_mixer.HasPendingLoop(this))
		{
			m_mixer.CompletePendingLoop(this);
			OnLoopStarted(playable);
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		base.OnBehaviourPause(playable, info);
		if (m_mixer == null || !m_mixer.HasPendingLoop(this))
		{
			OnLoopEnded(playable);
		}
	}

	public void OnLoopStarted(Playable playable)
	{
		if (m_dialogue == null || m_dialogue.DialogueUIPrefab == null || m_dialogue.DialogueScript == null || m_dialogue.DialogueScript.Length == 0)
		{
			return;
		}
		if (m_controller == null)
		{
			PlayableDirector playableDirector = playable.GetGraph().GetResolver() as PlayableDirector;
			m_controller = playableDirector.gameObject.RequestComponent<ClientDialogueController>();
			if (m_controller == null)
			{
				return;
			}
		}
		m_finished = false;
		if (m_followTarget != null)
		{
			m_dialogueRoutine = m_controller.StartDialogue(m_dialogue, m_followTarget);
		}
		else
		{
			m_dialogueRoutine = m_controller.StartDialogue(m_dialogue, m_anchor, m_pivot, m_rotation);
		}
	}

	public void OnLoopEnded(Playable playable)
	{
		if (m_controller != null)
		{
			m_controller.Shutdown(m_dialogue);
			m_dialogueRoutine = null;
		}
	}

	public bool IsLoopActive()
	{
		return !m_finished;
	}

	public override void ProcessFrame(Playable playable, FrameData info, object playerData)
	{
		if (m_dialogueRoutine != null && !m_dialogueRoutine.MoveNext())
		{
			m_finished = true;
		}
	}
}
