using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlacementCollisionSwapper : MonoBehaviour, ICarryNotified
{
	[SerializeField]
	public PhysicMaterial m_materialWhenPlaced;

	private Collider m_collider;

	private PhysicMaterial m_defaultMaterial;

	private bool m_carried;

	private bool m_onSurface;

	private void Awake()
	{
		m_collider = FindCollider();
		m_defaultMaterial = m_collider.sharedMaterial;
	}

	private Collider FindCollider()
	{
		Collider[] array = base.gameObject.RequestComponents<Collider>();
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].isTrigger)
			{
				return array[i];
			}
		}
		return null;
	}

	private void UpdateLayer()
	{
		if (m_carried)
		{
			m_collider.sharedMaterial = m_materialWhenPlaced;
		}
		else
		{
			m_collider.sharedMaterial = m_defaultMaterial;
		}
	}

	public void OnCarryBegun(ICarrier _carrier)
	{
		m_carried = true;
		UpdateLayer();
	}

	public void OnCarryEnded(ICarrier _carrier)
	{
		m_carried = false;
		UpdateLayer();
	}
}
