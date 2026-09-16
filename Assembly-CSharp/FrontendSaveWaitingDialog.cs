using UnityEngine;

public class FrontendSaveWaitingDialog : FrontendMenuBehaviour
{
	[SerializeField]
	private T17Text m_Timer;

	[SerializeField]
	private GameObject m_legend;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
	}

	public override bool Show(GamepadUser _currentGamer, BaseMenuBehaviour _parent, GameObject _invoker, bool _hideInvoker = true)
	{
		if (!base.Show(_currentGamer, _parent, _invoker, _hideInvoker))
		{
			return false;
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		if ((bool)m_legend)
		{
			m_legend.SetActive(false);
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		if (m_legend != null)
		{
			m_legend.SetActive(true);
		}
		return true;
	}

	protected override void Update()
	{
		base.Update();
		if (m_Timer != null && T17FrontendFlow.Instance != null && ConnectionStatus.IsInSession())
		{
			float clientCountdown = T17FrontendFlow.Instance.ClientCountdown;
			clientCountdown = Mathf.Max(Mathf.FloorToInt(clientCountdown + 0.99f), 0f);
			m_Timer.text = clientCountdown.ToString();
		}
	}
}
