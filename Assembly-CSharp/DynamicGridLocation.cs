public class DynamicGridLocation : StaticGridLocation
{
	private bool m_occupying = true;

	private void Update()
	{
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(base.transform.position);
		if (!(m_gridIndex != gridLocationFromPos))
		{
			return;
		}
		if (m_occupying)
		{
			if (m_gridManager.GetGridOccupant(m_gridIndex) == base.gameObject)
			{
				m_gridManager.DeoccupyGrid(m_gridIndex);
			}
			m_occupying = false;
		}
		if (m_gridManager.GetGridOccupant(gridLocationFromPos) == null)
		{
			m_occupying = true;
			m_gridManager.OccupyGrid(base.gameObject, gridLocationFromPos);
			m_gridIndex = gridLocationFromPos;
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_gridManager != null && m_gridManager.GetGridOccupant(m_gridIndex) == null)
		{
			m_gridManager.OccupyGrid(base.gameObject, m_gridIndex);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_gridManager != null && m_gridManager.GetGridOccupant(m_gridIndex) == base.gameObject)
		{
			m_gridManager.DeoccupyGrid(m_gridIndex);
		}
	}
}
