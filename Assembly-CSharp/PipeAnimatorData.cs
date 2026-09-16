using System;
using UnityEngine;

[Serializable]
public class PipeAnimatorData : MonoBehaviour
{
	[SerializeField]
	public LinearPath m_path;

	[SerializeField]
	[Range(0f, 1f)]
	public float m_position;

	[SerializeField]
	public float m_displacement;

	[SerializeField]
	public float m_falloff;
}
