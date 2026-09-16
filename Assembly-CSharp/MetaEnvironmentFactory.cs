using UnityEngine;

public class MetaEnvironmentFactory : MonoBehaviour
{
	[SerializeField]
	private GameObject m_gameMetaEnvironmentPrefab;

	public void Awake()
	{
		if (GameUtils.GetGameMetaEnvironment() == null)
		{
			m_gameMetaEnvironmentPrefab.InstantiateOnParent(null);
		}
	}
}
