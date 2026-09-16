using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu]
public class RumbleDirectory : ScriptableObject
{
	[Serializable]
	public class OneShotDirectoryEntry : RumbleDirectoryEntry
	{
		public GameOneShotAudioTag Tag;
	}

	[Serializable]
	public class LoopingDirectoryEntry : RumbleDirectoryEntry
	{
		public GameLoopingAudioTag Tag;
	}

	public OneShotDirectoryEntry[] OneShot = new OneShotDirectoryEntry[0];

	public LoopingDirectoryEntry[] Looping = new LoopingDirectoryEntry[0];
}
