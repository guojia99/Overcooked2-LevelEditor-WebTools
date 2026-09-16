using System;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
	[Serializable]
	public class Dialogue
	{
		[SelfAssignID(true)]
		public int UniqueID;

		[AssignResource("DialogueUI", Editorbility.Editable)]
		public SpeechDialogueUIController DialogueUIPrefab;

		public DialogueAudio DialogueAudio = new DialogueAudio();

		public string[] DialogueScript;
	}

	[Serializable]
	public class DialogueAudio
	{
		public enum SoundType
		{
			None = 0,
			OneShot = 1,
			Loop = 2
		}

		public SoundType AudioType;

		[HideInInspectorTest("AudioType", SoundType.OneShot)]
		public GameOneShotAudioTag OneShotTag = GameOneShotAudioTag.Blank;

		[HideInInspectorTest("AudioType", SoundType.Loop)]
		public GameLoopingAudioTag LoopTag = GameLoopingAudioTag.COUNT;
	}

	private GenericVoid<Dialogue, int> m_dialogueStateCallback = delegate
	{
	};

	public void RegisterDialogueStateCallback(GenericVoid<Dialogue, int> _callback)
	{
		m_dialogueStateCallback = (GenericVoid<Dialogue, int>)Delegate.Combine(m_dialogueStateCallback, _callback);
	}

	public void UnRegisterDialogueStateCallback(GenericVoid<Dialogue, int> _callback)
	{
		m_dialogueStateCallback = (GenericVoid<Dialogue, int>)Delegate.Remove(m_dialogueStateCallback, _callback);
	}

	public void OnDialogueStateChanged(Dialogue _dialogue, int _state)
	{
		m_dialogueStateCallback(_dialogue, _state);
	}

	public static void StartDialogueSFX(DialogueAudio _data, int _layer)
	{
		switch (_data.AudioType)
		{
		case DialogueAudio.SoundType.OneShot:
			GameUtils.TriggerAudio(_data.OneShotTag, _layer);
			break;
		case DialogueAudio.SoundType.Loop:
			GameUtils.StartAudio(_data.LoopTag, _data, _layer);
			break;
		}
	}

	public static void StopDialogueSFX(DialogueAudio _data)
	{
		DialogueAudio.SoundType audioType = _data.AudioType;
		if (audioType == DialogueAudio.SoundType.Loop)
		{
			GameUtils.StopAudio(_data.LoopTag, _data);
		}
	}
}
