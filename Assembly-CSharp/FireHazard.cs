using UnityEngine;

[RequireComponent(typeof(Flammable))]
[RequireComponent(typeof(CapsuleCollider))]
public class FireHazard : HazardBase
{
	[SerializeField]
	public float m_lifetime;

	[SerializeField]
	public float m_destroyTime;
}
