using UnityEngine;

[RequireComponent(typeof(T17Button))]
public class DlcThemeSelectButton : ThemeSelectButton
{
	[SerializeField]
	private DLCFrontendData m_dlcData;

	[SerializeField]
	private GameObject m_unpurchasedOverlay;

	private bool m_purchased;

	public DLCFrontendData DLCData
	{
		get
		{
			return m_dlcData;
		}
	}

	public bool Purchased
	{
		get
		{
			return m_purchased;
		}
		set
		{
			m_purchased = value;
			RefreshButton();
		}
	}

	public void RefreshButton()
	{
		if (m_unpurchasedOverlay != null)
		{
			m_unpurchasedOverlay.SetActive(!m_purchased);
		}
	}
}
