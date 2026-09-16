using UnityEngine;
using UnityEngine.Serialization;

namespace GameModes.Horde
{
	[RequireComponent(typeof(HordeLockable))]
	public class HordeLockable : MonoBehaviour
	{
		[FormerlySerializedAs("m_cost")]
		[SerializeField]
		public int m_unlockCost = 50;

		[SerializeField]
		public Collider m_collider;

		[SerializeField]
		public HordeLockable[] m_lockables;
	}
}
