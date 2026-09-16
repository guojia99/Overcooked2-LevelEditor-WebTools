using UnityEngine;

public class FrontendGUI : ScrollingListUIController
{
	[SerializeField]
	private FrontendListEntry.NameData[] m_nameData;

	public void SetNames(FrontendListEntry.NameData[] _nameData)
	{
		if (!IsSame(m_nameData, _nameData))
		{
			m_nameData = _nameData;
			OnSetNames();
		}
	}

	private bool IsSame(FrontendListEntry.NameData[] _a, FrontendListEntry.NameData[] _b)
	{
		if (_a.Length == _b.Length)
		{
			for (int i = 0; i < _a.Length; i++)
			{
				if (!_a[i].Equals(_b[i]))
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	protected override NameData[] GetNameData()
	{
		return m_nameData.ConvertAll((FrontendListEntry.NameData x) => x);
	}
}
