using System;
using UnityEngine;

[Serializable]
public class AudioDirectoryEntry : ClipParameters
{
	public AudioClip AudioFile;

	public bool SingleInstance;
}
