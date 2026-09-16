using System;
using AssetBundles;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class SceneLoaderHelper : MonoBehaviour
{
	private AssetBundleLoadLevelOperationBase m_LoadOp;

	private AsyncOperation m_asyncOp;

	private bool m_bAllowSceneActivation;

	private string m_LastSceneLoaded = "null";

	public bool ActivateSceneWhenLoaded
	{
		get
		{
			return m_bAllowSceneActivation;
		}
		set
		{
			m_bAllowSceneActivation = value;
			if (m_asyncOp != null)
			{
				m_asyncOp.allowSceneActivation = m_bAllowSceneActivation;
			}
		}
	}

	public void LoadLevelAsync(string sceneName, bool bActivateWhenLoaded, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
	{
		if (m_LastSceneLoaded != sceneName)
		{
			m_LastSceneLoaded = sceneName;
			m_bAllowSceneActivation = bActivateWhenLoaded;
			m_LoadOp = AssetBundleManager.LoadLevelAsync(sceneName.ToLowerInvariant(), sceneName, loadSceneMode == LoadSceneMode.Additive);
			AssetBundleLoadLevelOperationBase loadOp = m_LoadOp;
			loadOp.OnAsyncOperationStarted = (AssetBundleLoadLevelOperationBase.StartAsyncOperationDelegate)Delegate.Combine(loadOp.OnAsyncOperationStarted, new AssetBundleLoadLevelOperationBase.StartAsyncOperationDelegate(OnAsyncOperationStart));
			if (loadSceneMode != LoadSceneMode.Single || base.gameObject.GetComponent<LoadingScreenFlow>() == null)
			{
			}
			StartCoroutine(m_LoadOp);
			m_asyncOp = null;
		}
	}

	private void OnAsyncOperationStart(AsyncOperation op)
	{
		m_asyncOp = op;
		m_asyncOp.allowSceneActivation = m_bAllowSceneActivation;
	}

	public float GetProgress()
	{
		if (m_asyncOp != null)
		{
			return m_asyncOp.progress;
		}
		if (m_LoadOp != null)
		{
			return m_LoadOp.GetProgress();
		}
		return 0f;
	}
}
