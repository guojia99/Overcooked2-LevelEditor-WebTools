using UnityEngine;

public class CookingHandler : MonoBehaviour
{
	[SerializeField]
	public float m_cookingtime = 10f;

	[SerializeField]
	public CookingStepData m_cookingType;

	[SerializeField]
	public CookingStationType m_stationType;

	[SerializeField]
	public CookingUIController m_cookingUIPrefab;

	[SerializeField]
	public Vector3 m_cookingUIPrefabOffset = Vector3.zero;

	public CookedCompositeOrderNode.CookingProgress GetCookedOrderState(float _cookingProgress)
	{
		if (_cookingProgress > 2f * m_cookingtime)
		{
			return CookedCompositeOrderNode.CookingProgress.Burnt;
		}
		if (_cookingProgress > m_cookingtime)
		{
			return CookedCompositeOrderNode.CookingProgress.Cooked;
		}
		return CookedCompositeOrderNode.CookingProgress.Raw;
	}
}
