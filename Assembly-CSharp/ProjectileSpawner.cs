using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
	public enum FireMode
	{
		Direct = 0,
		Parabolic = 1
	}

	[SerializeField]
	[AssignChild("SpawnPoint", Editorbility.NonEditable)]
	public Transform m_spawnPoint;

	[SerializeField]
	public GameObject m_projectilePrefab;

	[SerializeField]
	public FireMode m_fireMode;

	[SerializeField]
	public float m_airTime;

	[SerializeField]
	public bool m_bUseTransformPositions;

	[SerializeField]
	public Vector3[] m_targetPositions;

	[SerializeField]
	public Transform[] m_transformTargetPositions;

	[SerializeField]
	public GameOneShotAudioTag m_spawnAudioTag = GameOneShotAudioTag.FireProjectiles;

	[Space]
	[SerializeField]
	public string m_fireTrigger;

	[SerializeField]
	public string m_fireAnimTrigger;

	[SerializeField]
	public string m_reachedTargetTrigger;

	[SerializeField]
	public string m_collidedTrigger;

	[Space]
	[SerializeField]
	public bool m_randomTargetOrder = true;

	[SerializeField]
	public bool m_alignTargetsToGrid = true;
}
