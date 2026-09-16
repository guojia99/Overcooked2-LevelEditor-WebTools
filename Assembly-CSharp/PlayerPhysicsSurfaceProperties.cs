using System;
using UnityEngine;

[Serializable]
public class PlayerPhysicsSurfaceProperties : ScriptableObject
{
	public float SpeedMultiplier = 1f;

	public float Slippiness;

	public float Slidiness;

	public bool UseOverrideGravityNormal;

	public Vector3 OverrideGravityNormal;

	public float GravityMultiplier = 1f;
}
