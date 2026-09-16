using System.Collections.Generic;
using UnityEngine;

public class FireballSpawner : MonoBehaviour
{
	private class Mover : MonoBehaviour
	{
		public Vector3 Velocity;

		private float m_prop;

		private void Update()
		{
			base.transform.position += TimeManager.GetDeltaTime(base.gameObject) * Velocity;
		}
	}

	[SerializeField]
	private Transform m_target;

	[SerializeField]
	private GameObject m_killObjectPrefab;

	[SerializeField]
	private float m_fireballSpeed = 2f;

	[Header("Animator Variables")]
	public bool FireCommmand;

	private bool m_firing;

	private List<GameObject> m_fireballs = new List<GameObject>();

	private float m_timer;

	private void Update()
	{
		if (!TimeManager.IsPaused(base.gameObject))
		{
			if (FireCommmand && !m_firing)
			{
				SpawnFireball();
				m_firing = true;
				FireCommmand = false;
			}
			else if (m_firing && !FireCommmand)
			{
				m_firing = false;
			}
		}
	}

	private void SpawnFireball()
	{
		GameObject gameObject = m_killObjectPrefab.InstantiateOnParent(null);
		gameObject.transform.localPosition = base.transform.position;
		gameObject.transform.localRotation = Quaternion.identity;
		SphereCollider sphereCollider = gameObject.RequireComponent<SphereCollider>();
		Collider[] array = Physics.OverlapSphere(gameObject.transform.TransformPoint(sphereCollider.center), sphereCollider.radius);
		for (int i = 0; i < array.Length; i++)
		{
			Physics.IgnoreCollision(array[i], sphereCollider);
		}
		ParticleSystem particleSystem = gameObject.RequestComponentRecursive<ParticleSystem>();
		if (particleSystem != null)
		{
			particleSystem.RestartPFX();
		}
		Mover mover = gameObject.AddComponent<Mover>();
		mover.Velocity = m_fireballSpeed * (m_target.position - gameObject.transform.position).SafeNormalised(Vector3.zero);
		m_fireballs.Add(gameObject);
	}
}
