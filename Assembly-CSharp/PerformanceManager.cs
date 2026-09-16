using UnityEngine;

public class PerformanceManager : MonoBehaviour
{
	private enum ScenePerformanceType
	{
		Kitchen = 0,
		StartScreen = 1
	}

	[SerializeField]
	private GamePerformanceSettingsFile m_gamePerformanceSettings;

	[SerializeField]
	private ScenePerformanceType m_sceneType;
}
