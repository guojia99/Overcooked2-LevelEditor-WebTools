using UnityEngine;
using UnityEngine.Serialization;

namespace GameModes.Horde
{
	public class HordeEnemy : MonoBehaviour
	{
		[FormerlySerializedAs("m_attackFrequencySeconds")]
		[SerializeField]
		public float m_attackTargetFrequencySeconds = 1f;

		[SerializeField]
		public int m_targetDamage = 25;

		[SerializeField]
		public float m_attackKitchenFrequencySeconds = 1f;

		[SerializeField]
		public int m_kitchenDamage = 10;

		[SerializeField]
		public int m_recipeCount = 1;

		[SerializeField]
		public float m_movementSpeed = 1f;

		[SerializeField]
		public AnimationCurve m_movementCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
	}
}
