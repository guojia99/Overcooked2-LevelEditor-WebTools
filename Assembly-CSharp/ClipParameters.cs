using System;
using UnityEngine;

[Serializable]
public class ClipParameters
{
	[Range(0f, 256f)]
	public int Priority = 128;

	[Range(0f, 1f)]
	public float Volume = 0.5f;

	[Range(-3f, 3f)]
	public float Pitch = 1f;

	[Range(0f, 1f)]
	public float RandomPitchVariance;

	public float m_fadeInTime;

	public float m_fadeOutTime;
}
