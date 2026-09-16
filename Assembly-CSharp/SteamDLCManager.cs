using System.Collections.Generic;
using Steamworks;

public class SteamDLCManager : DLCManagerBase
{
	private AppId_t m_appId;

	private HashSet<AppId_t> m_allDlcIds = new HashSet<AppId_t>();

	protected Callback<DlcInstalled_t> m_dlcInstalledCallback;

	private void Awake()
	{
		m_appId = SteamUtils.GetAppID();
	}

	protected override void Initialise()
	{
		base.Initialise();
		m_dlcInstalledCallback = Callback<DlcInstalled_t>.Create(OnDlcInstalled);
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
	}

	protected override void Cleanup()
	{
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	public override bool ShowDLCStorePage(DLCFrontendData data)
	{
		uint result = 0u;
		if (uint.TryParse(data.productId, out result))
		{
			SteamFriends.ActivateGameOverlayToStore((AppId_t)result, EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
			return true;
		}
		return false;
	}

	public override void RefreshDLC()
	{
		base.RefreshDLC();
		int dLCCount = SteamApps.GetDLCCount();
		for (int i = 0; i < dLCCount; i++)
		{
			AppId_t pAppID;
			bool pbAvailable;
			string pchName;
			if (SteamApps.BGetDLCDataByIndex(i, out pAppID, out pbAvailable, out pchName, 128) && SteamApps.BIsDlcInstalled(pAppID) && !m_allDlcIds.Contains(pAppID))
			{
				m_allDlcIds.Add(pAppID);
				DLCItem dLCItem = new DLCItem();
				dLCItem.name = pchName;
				dLCItem.productId = pAppID.ToString();
				AddDLCItem(dLCItem);
			}
		}
		if (DLCManagerBase.DLCUpdatedEvent != null)
		{
			DLCManagerBase.DLCUpdatedEvent();
		}
	}

	private void OnDlcInstalled(DlcInstalled_t param)
	{
		RefreshDLC();
	}

	private void OnEngagementChanged(EngagementSlot slot, GamepadUser userBefore, GamepadUser userAfter)
	{
		if (slot == EngagementSlot.One && userBefore == null && userAfter != null)
		{
			RefreshDLC();
		}
	}
}
