using UnityEngine;

public class VisibilityFromOnlineStatus : MonoBehaviour
{
	public bool m_visibleOffline;

	public bool m_visibleOnline;

	public bool m_visibleToHost;

	public bool m_visibleToClient;

	private void Start()
	{
		base.gameObject.SetActive(ShouldBeVisible());
	}

	private bool ShouldBeVisible()
	{
		if (!ConnectionStatus.IsInSession())
		{
			return m_visibleOffline;
		}
		return m_visibleOnline && ((!ConnectionStatus.IsHost()) ? m_visibleToClient : m_visibleToHost);
	}

	private void Update()
	{
		bool flag = ShouldBeVisible();
		if (flag != base.enabled)
		{
			base.gameObject.SetActive(flag);
		}
	}
}
