using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentObject : MonoBehaviour
{
	public enum PersistType
	{
		Persist = 0,
		DontPersist = 1
	}

	[SerializeField]
	public PersistType m_defaultBehaviour;

	[SerializeField]
	[SceneName]
	private string[] m_persistingLevels = new string[0];

	[SerializeField]
	[SceneName]
	private string[] m_unpersistingLevels = new string[0];

	private bool m_destroyOnLoad;

	private void Awake()
	{
		Object.DontDestroyOnLoad(base.gameObject);
		m_destroyOnLoad = ShouldDestroyOnNextLoad(SceneManager.GetActiveScene().name);
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	public void AddPersistingLevel(string _levelName)
	{
		if (!m_persistingLevels.Contains(_levelName))
		{
			ArrayUtils.PushBack(ref m_persistingLevels, _levelName);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (m_destroyOnLoad)
		{
			Object.Destroy(base.gameObject);
		}
		m_destroyOnLoad = ShouldDestroyOnNextLoad(SceneManager.GetActiveScene().name);
	}

	private bool ShouldDestroyOnNextLoad(string _currentLevel)
	{
		if (m_persistingLevels.Contains(_currentLevel))
		{
			return false;
		}
		if (m_unpersistingLevels.Contains(_currentLevel))
		{
			return true;
		}
		return m_defaultBehaviour == PersistType.DontPersist;
	}
}
