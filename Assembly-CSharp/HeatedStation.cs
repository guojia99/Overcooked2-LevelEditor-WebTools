using UnityEngine;

public class HeatedStation : MonoBehaviour
{
	[SerializeField]
	public float m_dissipationRate;

	[SerializeField]
	public StatValidationList m_burnAchievementFilter;

	[AssignResource("DLC07_Coal", Editorbility.NonEditable)]
	[SerializeField]
	public ItemOrderNode m_coalOrderNode;

	private const float c_heatThresholdHigh = 0.66f;

	private const float c_heatThresholdModerate = 0.33f;

	public HeatRange GetHeat(float _heat)
	{
		if (_heat >= 0.66f)
		{
			return HeatRange.High;
		}
		if (_heat >= 0.33f)
		{
			return HeatRange.Moderate;
		}
		return HeatRange.Low;
	}
}
