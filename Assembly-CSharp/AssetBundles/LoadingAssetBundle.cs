using UnityEngine;

namespace AssetBundles
{
	public class LoadingAssetBundle
	{
		public AssetBundleCreateRequest m_AssetBundleCreateRequest;

		public int m_ReferencedCount;

		public LoadingAssetBundle(AssetBundleCreateRequest assetBundleCreateRequest)
		{
			m_AssetBundleCreateRequest = assetBundleCreateRequest;
			m_ReferencedCount = 0;
		}
	}
}
