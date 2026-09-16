using UnityEngine;

namespace AssetBundles
{
	public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation
	{
		public abstract T GetAsset<T>() where T : Object;
	}
}
