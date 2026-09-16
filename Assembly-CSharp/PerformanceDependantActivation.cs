using UnityEngine;

public class PerformanceDependantActivation : MonoBehaviour
{
	[ArrayNames("GetArrayName")]
	public bool[] IsActive;

	public static string GetArrayName(int _index)
	{
		return QualitySettings.names.TryAtIndex(_index);
	}

	private void Awake()
	{
		int qualityLevel = QualitySettings.GetQualityLevel();
		bool active = IsActive.TryAtIndex(qualityLevel, false);
		base.gameObject.SetActive(active);
	}
}
