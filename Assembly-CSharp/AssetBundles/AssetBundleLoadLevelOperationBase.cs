using UnityEngine;

namespace AssetBundles
{
	public abstract class AssetBundleLoadLevelOperationBase : AssetBundleLoadOperation
	{
		public delegate void StartAsyncOperationDelegate(AsyncOperation op);

		public StartAsyncOperationDelegate OnAsyncOperationStarted;

		public abstract float GetProgress();
	}
}
