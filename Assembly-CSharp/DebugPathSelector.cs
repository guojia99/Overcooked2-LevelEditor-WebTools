using UnityEngine;

[RequireComponent(typeof(GridNavigator))]
public class DebugPathSelector : MonoBehaviour
{
	private GridNavigator m_gridNavigator;

	private void Awake()
	{
		m_gridNavigator = base.gameObject.RequireComponent<GridNavigator>();
	}

	private void Update()
	{
		if (Input.GetMouseButton(0))
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Vector3 pos = ray.origin - ray.origin.y * ray.direction / ray.direction.y;
			m_gridNavigator.MoveToTarget(pos);
		}
	}
}
