using UnityEngine;

public class MeteorManager : MonoBehaviour
{
	[SerializeField]
	private GameObject m_meteorPrefab;

	[SerializeField]
	private Point3 m_minGridIndex;

	[SerializeField]
	private Point3 m_maxGridIndex;

	[SerializeField]
	private float m_minTimePeriod;

	[SerializeField]
	private float m_maxTimePeriod;

	private GridManager m_gridManager;

	private float m_timer;

	private void Awake()
	{
		ResetTimer();
		m_gridManager = GameUtils.GetGridManager(base.transform);
	}

	private void ResetTimer()
	{
		m_timer = Random.Range(m_minTimePeriod, m_maxTimePeriod);
	}

	private void Update()
	{
		m_timer -= TimeManager.GetDeltaTime(base.gameObject.layer);
		if (m_timer < 0f)
		{
			int x = Random.Range(m_minGridIndex.X, m_maxGridIndex.X + 1);
			int y = Random.Range(m_minGridIndex.Y, m_maxGridIndex.Y + 1);
			int z = Random.Range(m_minGridIndex.Z, m_maxGridIndex.Z + 1);
			Vector3 posFromGridLocation = m_gridManager.GetPosFromGridLocation(new GridIndex(x, y, z));
			m_meteorPrefab.Instantiate(posFromGridLocation, Quaternion.identity);
			ResetTimer();
		}
	}
}
