using System;
using UnityEngine;

[Serializable]
public class AudioDirectoryData : ScriptableObject
{
	[Serializable]
	public class OneShotAudioDirectoryEntry : AudioDirectoryEntry
	{
		public GameOneShotAudioTag Tag;
	}

	[Serializable]
	public class LoopingAudioDirectoryEntry : AudioDirectoryEntry
	{
		public GameLoopingAudioTag Tag;

		public AudioClip StartClip;

		public AudioClip EndClip;
	}

	public OneShotAudioDirectoryEntry[] OneShotAudio = new OneShotAudioDirectoryEntry[0];

	public LoopingAudioDirectoryEntry[] LoopingAudio = new LoopingAudioDirectoryEntry[0];
}
