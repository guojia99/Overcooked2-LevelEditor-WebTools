using UnityEngine;

namespace GameModes.Horde
{
	[RequireComponent(typeof(PlateStation))]
	[RequireComponent(typeof(Interactable))]
	public class HordeTarget : MonoBehaviour
	{
		[SerializeField]
		private Transform m_targetTransform;

		[Range(0.5f, 5f)]
		[SerializeField]
		private float m_targetRadius = 0.5f;

		[SerializeField]
		private Transform m_spawnTransform;

		[Range(0.5f, 5f)]
		[SerializeField]
		private float m_spawnRadius = 0.5f;

		public Transform TargetTransform
		{
			get
			{
				return (!(m_targetTransform != null)) ? base.transform : m_targetTransform;
			}
		}

		public float TargetRadius
		{
			get
			{
				return m_targetRadius;
			}
		}

		public Transform SpawnTransform
		{
			get
			{
				return (!(m_spawnTransform != null)) ? base.transform : m_spawnTransform;
			}
		}

		public float SpawnRadius
		{
			get
			{
				return m_spawnRadius;
			}
		}
	}
}
