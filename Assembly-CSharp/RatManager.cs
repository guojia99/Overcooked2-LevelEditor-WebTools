using UnityEngine;

public class RatManager : Manager
{
	[SerializeField]
	private GameObject m_ratPrefab;

	[SerializeField]
	private float m_ratSpawnTime = 5f;

	[SerializeField]
	private float m_droppedIngredientCooldownTime = 2f;

	[SerializeField]
	private float m_minimumSpawnDistance = 5f;
}
