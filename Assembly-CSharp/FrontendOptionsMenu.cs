using System;
using UnityEngine;
using UnityEngine.UI;

public class FrontendOptionsMenu : FrontendMenuBehaviour
{
	[SerializeField]
	private Selectable m_PCTopSelectable;

	[SerializeField]
	private Selectable m_ConsoleTopSelectable;

	[SerializeField]
	private SafeAreaAdjuster m_SafeAreaAdjuster;

	[SerializeField]
	private T17Text m_VersionString;

	public T17ScrollView m_ScrollView;

	private ISyncUIWithOption[] m_SyncOptions;

	private Suppressor m_SafeAreaInputSupressor;

	private T17DialogBox m_dialogBox;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	private bool m_discard;

	protected override void Awake()
	{
		base.Awake();
		if (m_VersionString != null)
		{
			m_VersionString.SetNonLocalizedText("Build #" + BuildVersion.m_VersionString);
		}
		m_BorderSelectables.selectOnUp = m_PCTopSelectable;
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_SyncOptions = base.gameObject.RequestInterfacesRecursive<ISyncUIWithOption>();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		m_discard = false;
		ResetOptions();
		SyncAllOptions();
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Combine(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		return m_ScrollView.Show(currentGamer, parent, invoker, hideInvoker) && base.Show(currentGamer, parent, invoker, hideInvoker);
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		if (base.gameObject.activeSelf && !m_discard && metaGameProgress.AccessOptionsData.AnyChangesToCommit())
		{
			m_dialogBox = T17DialogBoxManager.GetDialog(false);
			m_dialogBox.Initialize("Text.Warning", "Text.Menu.UnsavedChanges.Body", "Text.Button.Discard", "Text.Button.Save", null);
			T17DialogBox dialogBox = m_dialogBox;
			dialogBox.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox.OnDecline, new T17DialogBox.DialogEvent(SaveAndClose));
			T17DialogBox dialogBox2 = m_dialogBox;
			dialogBox2.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox2.OnConfirm, new T17DialogBox.DialogEvent(ResetAndClose));
			m_dialogBox.Show();
			return false;
		}
		ResetOptions();
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Remove(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		bool flag = m_ScrollView.Hide(restoreInvokerState, isTabSwitch);
		if (m_SafeAreaAdjuster.enabled)
		{
			m_SafeAreaAdjuster.gameObject.SetActive(false);
			m_SafeAreaAdjuster.Hide();
			if (m_SafeAreaInputSupressor != null)
			{
				m_CachedEventSystem.ReleaseSuppressor(m_SafeAreaInputSupressor);
			}
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
		return flag && base.Hide(restoreInvokerState, isTabSwitch);
	}

	public void SyncAllOptions()
	{
		if (m_SyncOptions != null)
		{
			for (int i = 0; i < m_SyncOptions.Length; i++)
			{
				m_SyncOptions[i].SyncUIWithOption();
			}
		}
	}

	public void OnSaveClicked()
	{
		m_discard = true;
		SaveOptions();
	}

	private void SaveAndClose()
	{
		m_dialogBox = null;
		m_discard = false;
		SaveOptions();
		Hide();
	}

	public void ResetAndClose()
	{
		if (m_dialogBox != null && m_dialogBox.IsActive)
		{
			m_dialogBox.Hide();
		}
		m_dialogBox = null;
		m_discard = true;
		ResetOptions();
		Hide();
	}

	private void SaveOptions()
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		GameUtils.RequireManager<SaveManager>().SaveMetaProgress();
	}

	private void ResetOptions()
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		metaGameProgress.AccessOptionsData.LoadFromSave();
	}

	public void OnCancelClicked()
	{
		Hide();
	}

	public void ShowSafeAreaAdjuster()
	{
		m_SafeAreaAdjuster.gameObject.SetActive(true);
		m_SafeAreaAdjuster.Show();
		m_SafeAreaInputSupressor = m_CachedEventSystem.Disable(this);
	}

	private void OnInviteAccepted()
	{
		ResetAndClose();
	}

	protected override void Update()
	{
		base.Update();
		if (m_SafeAreaAdjuster.gameObject.activeInHierarchy && m_SafeAreaAdjuster.Completed)
		{
			m_SafeAreaAdjuster.gameObject.SetActive(false);
			m_CachedEventSystem.ReleaseSuppressor(m_SafeAreaInputSupressor);
		}
	}

	protected override void OnDestroy()
	{
		if (m_dialogBox != null)
		{
			m_dialogBox.Hide();
		}
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Remove(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		base.OnDestroy();
	}
}
