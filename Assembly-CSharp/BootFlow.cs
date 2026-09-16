using System.Collections;
using AssetBundles;
using UnityEngine;

public class BootFlow : MonoBehaviour
{
	public string m_SceneToLoad = string.Empty;

	public ProgressBarUI m_ProgressBar;

	private SceneLoaderHelper m_SLH;

	public GameObject m_splashScreen;

	private void Awake()
	{
	}

	private IEnumerator Start()
	{
		AssetBundleLoadManifestOperation request = AssetBundleManager.Initialize();
		while (request.MoveNext())
		{
			yield return null;
		}
		m_SLH = base.gameObject.AddComponent<SceneLoaderHelper>();
		m_SLH.LoadLevelAsync(m_SceneToLoad, true);
	}

	private void Update()
	{
		if (m_ProgressBar != null)
		{
			if (m_SLH != null)
			{
				m_ProgressBar.SetValue(m_SLH.GetProgress());
			}
			else
			{
				m_ProgressBar.SetValue(0f);
			}
		}
	}
}
