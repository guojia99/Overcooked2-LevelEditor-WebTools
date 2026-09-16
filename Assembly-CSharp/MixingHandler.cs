using UnityEngine;

public class MixingHandler : MonoBehaviour
{
	[SerializeField]
	public float m_mixingTime = 10f;

	[SerializeField]
	public CookingStepData m_mixingType;

	[SerializeField]
	public CookingUIController m_progressUIPrefab;

	public MixedCompositeOrderNode.MixingProgress GetMixedOrderState(float _mixingProgress)
	{
		if (_mixingProgress > 2f * m_mixingTime)
		{
			return MixedCompositeOrderNode.MixingProgress.OverMixed;
		}
		if (_mixingProgress >= m_mixingTime)
		{
			return MixedCompositeOrderNode.MixingProgress.Mixed;
		}
		return MixedCompositeOrderNode.MixingProgress.Unmixed;
	}
}
