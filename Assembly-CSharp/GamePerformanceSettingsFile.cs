using UnityEngine;

public class GamePerformanceSettingsFile : ScriptableObject
{
	[ArrayNames("GetArrayName")]
	public GamePeformanceSettings[] SettingsForLevel;

	public GamePeformanceSettings FindPerformanceSettings()
	{
		int qualityLevel = QualitySettings.GetQualityLevel();
		return SettingsForLevel.TryAtIndex(qualityLevel);
	}

	public static string GetArrayName(int _index)
	{
		return QualitySettings.names.TryAtIndex(_index);
	}
}
