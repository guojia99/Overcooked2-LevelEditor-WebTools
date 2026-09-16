using UnityEngine;

[ExecutionDependency(typeof(IGridLocation))]
public class GridAutoParenting : MonoBehaviour
{
	private GridManager m_gridManager;

	[SerializeField]
	private bool m_keepWorldPosition = true;

	private void Awake()
	{
		m_gridManager = GameUtils.GetGridManager(base.transform);
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(base.transform.position);
		GameObject gridOccupant = m_gridManager.GetGridOccupant(gridLocationFromPos);
		if (gridOccupant != null && gridOccupant.transform != null)
		{
			base.transform.SetParent(gridOccupant.transform, m_keepWorldPosition);
		}
	}
}
