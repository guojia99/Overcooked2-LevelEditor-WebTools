using UnityEngine;

[ExecuteInEditMode]
public class EditorGridSnap : MonoBehaviour
{
	[SerializeField]
	private bool m_constrainX = true;

	[SerializeField]
	private bool m_constrainY = true;

	[SerializeField]
	private bool m_constrainZ = true;

	protected GridManager m_gridManager;

	private void Awake()
	{
		if (Application.isPlaying)
		{
			base.enabled = false;
		}
	}

	private void OnEnable()
	{
		m_gridManager = null;
	}

	protected virtual void Update()
	{
		if (m_gridManager == null)
		{
			m_gridManager = GameUtils.GetGridManager(base.transform);
			return;
		}
		GridIndex unclampedGridLocationFromPos = m_gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
		base.transform.position = VectorUtils.Select(m_gridManager.GetPosFromGridLocation(unclampedGridLocationFromPos), base.transform.position, m_constrainX, m_constrainY, m_constrainZ);
	}
}
