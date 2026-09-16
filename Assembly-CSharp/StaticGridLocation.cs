using UnityEngine;

[ExecutionDependency(typeof(CampaignKitchenLoaderManager))]
[ExecutionDependency(typeof(CompetitiveKitchenLoaderManager))]
public class StaticGridLocation : MonoBehaviour, IGridLocation
{
	protected GridManager m_gridManager;

	protected GridIndex m_gridIndex;

	public GridIndex GridIndex
	{
		get
		{
			return m_gridIndex;
		}
	}

	public GridManager AccessGridManager
	{
		get
		{
			return m_gridManager;
		}
	}

	private void Awake()
	{
		Initialise();
	}

	public void Initialise()
	{
		m_gridManager = GameUtils.GetGridManager(base.transform);
		m_gridIndex = m_gridManager.GetGridLocationFromPos(base.transform.position);
		m_gridManager.OccupyGrid(base.gameObject, m_gridIndex);
	}

	private void OnTrigger(string _message)
	{
		if (_message == "destroyed")
		{
			OnDestroy();
		}
	}

	protected virtual void OnEnable()
	{
	}

	protected virtual void OnDisable()
	{
	}

	private void OnDestroy()
	{
		if (m_gridManager != null)
		{
			if (m_gridManager.GetGridOccupant(m_gridIndex) == base.gameObject)
			{
				m_gridManager.DeoccupyGrid(m_gridIndex);
			}
			m_gridManager = null;
		}
	}

	public bool IsGridOccupant()
	{
		if (m_gridManager != null)
		{
			return m_gridManager.GetGridOccupant(m_gridIndex) == base.gameObject;
		}
		return false;
	}
}
