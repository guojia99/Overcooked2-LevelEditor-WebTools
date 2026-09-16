using UnityEngine;

public class TriggerCreateHazard : MonoBehaviour
{
	[SerializeField]
	private GameObject m_hazardPrefab;

	[SerializeField]
	private string m_spawnTrigger;

	[SerializeField]
	private bool m_alignToGrid = true;

	private GameObject CreateHazard(GameObject _prefab, Transform _transform, bool _alignToGrid = true)
	{
		GridManager gridManager = GameUtils.GetGridManager(_transform);
		GridIndex gridLocationFromPos = gridManager.GetGridLocationFromPos(_transform.position);
		GameObject gridOccupant = gridManager.GetGridOccupant(gridLocationFromPos);
		if (gridOccupant == null || gridOccupant.RequestInterface<HazardBase>() != null)
		{
			GameObject gameObject = _prefab.Instantiate(_transform.position, Quaternion.identity);
			if (_alignToGrid)
			{
				gameObject.transform.position = gridManager.GetPosFromGridLocation(gridLocationFromPos);
			}
			return gameObject;
		}
		return null;
	}

	private void OnTrigger(string _message)
	{
		if (m_spawnTrigger == _message)
		{
			CreateHazard(m_hazardPrefab, base.transform, m_alignToGrid);
		}
	}
}
