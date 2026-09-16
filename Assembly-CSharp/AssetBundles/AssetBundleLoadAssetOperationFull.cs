using System;
using UnityEngine;

namespace AssetBundles
{
	public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation
	{
		protected string m_AssetBundleName;

		protected string m_AssetName;

		protected string m_DownloadingError;

		protected Type m_Type;

		protected AssetBundleRequest m_Request;

		public AssetBundleLoadAssetOperationFull(string bundleName, string assetName, Type type)
		{
			m_AssetBundleName = bundleName;
			m_AssetName = assetName;
			m_Type = type;
		}

		public override T GetAsset<T>()
		{
			if (m_Request != null && m_Request.isDone)
			{
				return m_Request.asset as T;
			}
			return (T)null;
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
				m_Request = loadedAssetBundle.m_AssetBundle.LoadAssetAsync(m_AssetName, m_Type);
				return false;
			}
			return true;
		}

		public override bool IsDone()
		{
			if (m_Request == null && m_DownloadingError != null)
			{
				return true;
			}
			return m_Request != null && m_Request.isDone;
		}
	}
}
