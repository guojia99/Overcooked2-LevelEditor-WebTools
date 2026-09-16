using System;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Flow/FrontEndFlow")]
public class FrontEndFlow : MonoBehaviour
{
	[Serializable]
	public class Screen
	{
		public string ScreenName = string.Empty;

		public int PlayerCount = -1;

		public SceneInfo[] Scenes = new SceneInfo[0];
	}

	[Serializable]
	public class SceneInfo
	{
		public SceneDirectoryData.SceneDirectoryEntry SceneDirectoryData;

		[HideInInspector]
		public bool Completed;

		[HideInInspector]
		public bool Unlocked;
	}

	[SerializeField]
	private SceneDirectoryData m_sceneDirectory;

	[SerializeField]
	private float m_kerchunkInterval = 0.12f;

	[SerializeField]
	private FrontendGUI m_frontendGUI;

	[SerializeField]
	private GameObject m_gameSessionPrefab;

	private Screen[] m_screens = new Screen[0];
}
