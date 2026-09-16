using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssetBundles
{
	public class AssetBundleLoadLevelOperation : AssetBundleLoadLevelOperationBase
	{
		protected string m_AssetBundleName;

		protected string m_LevelName;

		protected bool m_IsAdditive;

		protected string m_DownloadingError;

		protected AsyncOperation m_Request;

		public AssetBundleLoadLevelOperation(string assetbundleName, string levelName, bool isAdditive)
		{
			m_AssetBundleName = assetbundleName;
			m_LevelName = levelName;
			m_IsAdditive = isAdditive;
		}

		public override bool Update()
		{
			if (m_Request != null)
			{
				return false;
			}
			LoadedAssetBundle loadedAssetBundle = AssetBundleManager.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);
			if (loadedAssetBundle != null)
			{
				if (OnAsyncOperationStarted != null)
				{
					AsyncOperation asyncOperation = null;
					asyncOperation = ((!m_IsAdditive) ? SceneManager.LoadSceneAsync(m_LevelName) : SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Additive));
					OnAsyncOperationStarted(asyncOperation);
					m_Request = asyncOperation;
					OnAsyncOperationStarted = null;
				}
				else if (m_IsAdditive)
				{
					m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Additive);
				}
				else
				{
					m_Request = SceneManager.LoadSceneAsync(m_LevelName);
				}
				return false;
			}
			return true;
		}

		public override float GetProgress()
		{
			if (m_Request != null)
			{
				return m_Request.progress;
			}
			return 0f;
		}

		public override bool IsDone()
		{
			if (m_Request == null && m_DownloadingError != null)
			{
				Debug.LogError(m_DownloadingError);
				return true;
			}
			return m_Request != null && m_Request.isDone;
		}
	}
}
