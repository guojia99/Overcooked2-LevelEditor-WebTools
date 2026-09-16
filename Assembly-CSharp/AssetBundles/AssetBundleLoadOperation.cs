using System.Collections;

namespace AssetBundles
{
	public abstract class AssetBundleLoadOperation : IEnumerator
	{
		public delegate void OperationCompleteDelegate();

		public OperationCompleteDelegate OperationComplete;

		public object Current
		{
			get
			{
				return null;
			}
		}

		public bool MoveNext()
		{
			bool flag = IsDone();
			if (flag && OperationComplete != null)
			{
				OperationComplete();
				OperationComplete = null;
			}
			return !flag;
		}

		public void Reset()
		{
		}

		public abstract bool Update();

		public abstract bool IsDone();
	}
}
