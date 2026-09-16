using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/IngredientContainer")]
public class IngredientContainer : MonoBehaviour
{
	[SerializeField]
	public int m_capacity = 3;

	[SerializeField]
	public Collider m_onSurfaceTriggerZone;

	public int GetCapacity()
	{
		return m_capacity;
	}
}
