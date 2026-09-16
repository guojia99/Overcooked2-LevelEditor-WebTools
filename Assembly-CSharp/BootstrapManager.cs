using UnityEngine;

public class BootstrapManager : Manager
{
	[SerializeField]
	private GameObject m_gameMetaEnvironmentPrefab;

	[SerializeField]
	private GameObject m_gameSessionPrefab;

	public GameObject GameSessionPrefab
	{
		get
		{
			return m_gameSessionPrefab;
		}
	}

	private void Awake()
	{
		EnsureSetup();
	}

	public virtual void EnsureSetup()
	{
		if (GameUtils.GetGameMetaEnvironment() == null)
		{
			GameObject gameObject = m_gameMetaEnvironmentPrefab.InstantiateOnParent(null);
			for (int i = 0; i < gameObject.transform.childCount; i++)
			{
				gameObject.transform.GetChild(i).gameObject.SendMessage("BootstrapAwake", SendMessageOptions.DontRequireReceiver);
			}
		}
		if (GameUtils.GetGameSession() == null && m_gameSessionPrefab != null)
		{
			m_gameSessionPrefab.InstantiateOnParent(null);
			StartCoroutine(GameUtils.GetGameSession().LoadSession());
		}
	}
}
