using System;
using UnityEngine;

namespace AssetBundles
{
	public class AssetBundleLoadManifestOperation : AssetBundleLoadAssetOperationFull
	{
		public AssetBundleLoadManifestOperation(string bundleName, string assetName, Type type)
			: base(bundleName, assetName, type)
		{
		}

		public override bool Update()
		{
			base.Update();
			if (m_Request != null && m_Request.isDone)
			{
				AssetBundleManager.AssetBundleManifestObject = GetAsset<AssetBundleManifest>();
				return false;
			}
			return true;
		}
	}
}
