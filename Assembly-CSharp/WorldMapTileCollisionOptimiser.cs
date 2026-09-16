using UnityEngine;

[ExecuteInEditMode]
public class WorldMapTileCollisionOptimiser : MonoBehaviour
{
	[SerializeField]
	[AssignChild("Collision", Editorbility.NonEditable)]
	private GameObject m_collider;

	[SerializeField]
	[AssignChild("Tile", Editorbility.NonEditable)]
	private GameObject m_rootMesh;

	private GridManager m_gridManager;

	private GridIndex m_gridIndex;

	private void Awake()
	{
		if (Application.isPlaying)
		{
			base.enabled = false;
		}
		else
		{
			RefreshObjects();
		}
	}

	private void Update()
	{
		if (m_gridManager == null)
		{
			m_gridManager = GameUtils.GetGridManager(base.transform);
			return;
		}
		GridIndex unclampedGridLocationFromPos = m_gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
		if (unclampedGridLocationFromPos != m_gridIndex)
		{
			Optimise(unclampedGridLocationFromPos);
			m_gridIndex = unclampedGridLocationFromPos;
		}
	}

	private void RefreshObjects()
	{
		if (m_collider == null || !m_collider.IsInHierarchyOf(base.gameObject))
		{
			m_collider = base.gameObject.RequestChildRecursive("Collision");
		}
		if (m_rootMesh == null || !m_rootMesh.IsInHierarchyOf(base.gameObject))
		{
			m_rootMesh = base.gameObject.RequestChildRecursive("Tile");
		}
	}

	private void Optimise(GridIndex _index)
	{
		int num = Mathf.Abs(_index.Y % 2);
		if (m_rootMesh != null)
		{
			float z = m_rootMesh.transform.localScale.z;
			if ((z < 0f && num == 0) || (z > 0f && num == 1))
			{
				m_rootMesh.transform.localScale = m_rootMesh.transform.localScale.WithZ(0f - z);
			}
		}
		if (m_collider != null)
		{
			m_collider.transform.localScale = Vector3.one;
		}
	}
}
