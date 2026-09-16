using System;
using UnityEngine;

[Serializable]
public class KeepParticleAnimatorData : MonoBehaviour
{
	[SerializeField]
	public float simulationSpeed = 1f;

	[SerializeField]
	public float startLifetime = 1f;

	[SerializeField]
	public int rateOverTime = 100;

	[SerializeField]
	public Color startColor = Color.white;
}
