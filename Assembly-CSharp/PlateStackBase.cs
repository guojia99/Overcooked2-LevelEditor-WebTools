using UnityEngine;

[RequireComponent(typeof(Stack))]
public class PlateStackBase : MonoBehaviour
{
	[SerializeField]
	public GameObject m_platePrefab;

	public virtual PlatingStepData GetPlatingStep()
	{
		return null;
	}
}
