using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrontendCampaignTabOptions : FrontendMenuBehaviour
{
	[SerializeField]
	private Transform m_continueButton;

	private FrontendRootMenu m_frontendRootMenu;

	private SelectSaveDialog m_saveDialog;

	[SerializeField]
	private GameObject m_campaignOptions;

	[SerializeField]
	private GameObject m_campaignOptionsDLC;

	[SerializeField]
	private Transform m_continueButtonDLC;

	[SerializeField]
	private GameObject m_newContentIndicator;

	protected override void Awake()
	{
		base.Awake();
		if (T17FrontendFlow.Instance != null)
		{
			m_frontendRootMenu = T17FrontendFlow.Instance.m_Rootmenu;
			m_saveDialog = m_frontendRootMenu.SearchAllForMenuOfType<SelectSaveDialog>();
		}
		m_continueButton = m_continueButtonDLC;
		m_campaignOptions.SetActive(false);
		m_campaignOptionsDLC.SetActive(true);
		if (m_continueButton != null)
		{
			MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
			int num = -1;
			if (metaGameProgress != null)
			{
				num = metaGameProgress.GetLastSaveSlot(-1);
			}
			m_continueButton.gameObject.SetActive(num != -1);
		}
		Selectable[] componentsInChildren = base.transform.GetComponentsInChildren<Selectable>();
		if (componentsInChildren.Length > 0)
		{
			m_BorderSelectables.selectOnUp = componentsInChildren[0];
		}
	}

	public void OnContinueGameClicked()
	{
		int lastSaveSlot = GameUtils.GetMetaGameProgress().GetLastSaveSlot(-1);
		StartCoroutine(m_saveDialog.LoadSlot(-1, lastSaveSlot));
	}

	public void OnNewGameClicked(GameObject _invoker)
	{
		if (m_saveDialog != null && m_frontendRootMenu != null)
		{
			m_saveDialog.Mode = SaveDialogMode.NewGame;
			m_saveDialog.DLC = -1;
			m_frontendRootMenu.OpenFrontendMenu(m_saveDialog);
		}
	}

	public void OnLoadGameClicked(GameObject _invoker)
	{
		if (m_saveDialog != null && m_frontendRootMenu != null)
		{
			m_saveDialog.Mode = SaveDialogMode.LoadGame;
			m_saveDialog.DLC = -1;
			m_frontendRootMenu.OpenFrontendMenu(m_saveDialog);
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		RefreshNewContentIndicator();
		return true;
	}

	protected override void Update()
	{
		base.Update();
		RefreshNewContentIndicator();
	}

	private void RefreshNewContentIndicator()
	{
		if (m_newContentIndicator != null)
		{
			m_newContentIndicator.SetActive(false);
		}
		DLCManager dLCManager = GameUtils.RequireManager<DLCManager>();
		SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
		MetaGameProgress metaGameProgress = saveManager.GetMetaGameProgress();
		if (!(metaGameProgress != null))
		{
			return;
		}
		List<DLCFrontendData> list = new List<DLCFrontendData>(dLCManager.AllDlc);
		list.RemoveAll((DLCFrontendData x) => x == null || !x.IsAvailableOnThisPlatform() || !x.m_ShowOnDlcPage);
		for (int num = 0; num < list.Count; num++)
		{
			if (!metaGameProgress.GetDLCSeen(list[num]))
			{
				m_newContentIndicator.SetActive(true);
				break;
			}
		}
	}
}
