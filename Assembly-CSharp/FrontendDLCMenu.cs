using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FrontendDLCMenu : FrontendMenuBehaviour
{
	private const int c_MinShownDLCButtons = 5;

	[SerializeField]
	[AssignChildRecursive("CarouselRootMenu", Editorbility.Editable)]
	private CarouselRootMenu m_packSelectMenu;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	private DLCManager m_dlcManager;

	private DlcSelectButton[] m_cardButtons = new DlcSelectButton[0];

	private DlcSelectButton m_flippedCard;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
		m_dlcManager = GameUtils.RequireManager<DLCManager>();
		m_cardButtons = m_packSelectMenu.gameObject.RequestComponentsRecursive<DlcSelectButton>();
		if (m_packSelectMenu != null)
		{
			m_packSelectMenu.CarouselButtonClicked += OnPackSelected;
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		DLCManagerBase.DLCUpdatedEvent = (GenericVoid)Delegate.Remove(DLCManagerBase.DLCUpdatedEvent, new GenericVoid(OnDLCUpdated));
		if (m_packSelectMenu != null)
		{
			m_packSelectMenu.CarouselButtonClicked -= OnPackSelected;
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_packSelectMenu == null || !m_packSelectMenu.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		for (int i = 0; i < m_cardButtons.Length; i++)
		{
			m_cardButtons[i].SetButtonCallbackHandler(this);
			m_cardButtons[i].SetFlipState(DlcSelectButton.FlipState.Front);
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		RefreshDLCButtons();
		DLCManagerBase.DLCUpdatedEvent = (GenericVoid)Delegate.Combine(DLCManagerBase.DLCUpdatedEvent, new GenericVoid(OnDLCUpdated));
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (m_packSelectMenu != null && !m_packSelectMenu.Hide(restoreInvokerState, isTabSwitch))
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
		DLCManagerBase.DLCUpdatedEvent = (GenericVoid)Delegate.Remove(DLCManagerBase.DLCUpdatedEvent, new GenericVoid(OnDLCUpdated));
		return true;
	}

	public override void Close()
	{
		if (m_flippedCard != null)
		{
			SetFlippedCard(null);
		}
		else
		{
			base.Close();
		}
	}

	private void SetFlippedCard(DlcSelectButton card, bool autoSelect = true)
	{
		DlcSelectButton flippedCard = m_flippedCard;
		if (flippedCard != null)
		{
			flippedCard.SetFlipState(DlcSelectButton.FlipState.Front);
		}
		if (card != null)
		{
			m_flippedCard = card;
			m_flippedCard.SetFlipState(DlcSelectButton.FlipState.Back);
			if (autoSelect)
			{
				ReselectCard(m_flippedCard);
			}
		}
		else
		{
			m_flippedCard = null;
			if (autoSelect)
			{
				ReselectCard(flippedCard);
			}
		}
	}

	protected override void Update()
	{
		base.Update();
		if (m_flippedCard != m_packSelectMenu.GetCurrentButton())
		{
			SetFlippedCard(null, false);
			if (m_flippedCard != null && m_flippedCard.m_flipState != DlcSelectButton.FlipState.Front)
			{
				SetFlippedCard((DlcSelectButton)m_packSelectMenu.GetCurrentButton());
			}
		}
	}

	private void ReselectCard(DlcSelectButton card)
	{
		Selectable firstActiveButton = card.GetFirstActiveButton();
		if (firstActiveButton != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(null);
			m_CachedEventSystem.SetSelectedGameObject(firstActiveButton.gameObject);
		}
		else if (m_BorderSelectables.selectOnUp != null && m_bSelectTopElementOnShow)
		{
			m_CachedEventSystem.SetSelectedGameObject(null);
			m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.gameObject);
		}
	}

	private void RefreshDLCButtons()
	{
		List<DLCFrontendData> list = new List<DLCFrontendData>(m_dlcManager.AllDlc);
		list.RemoveAll((DLCFrontendData x) => x == null || !x.IsAvailableOnThisPlatform() || !x.m_ShowOnDlcPage);
		int num = Mathf.CeilToInt(5f / (float)list.Count) * list.Count;
		int num2;
		for (num2 = 0; num2 < num; num2++)
		{
			m_cardButtons[num2].Setup(list[num2 % list.Count]);
		}
		for (; num2 < m_cardButtons.Length; num2++)
		{
			m_cardButtons[num2].Setup(null);
		}
	}

	private void OnDLCUpdated()
	{
		RefreshDLCButtons();
	}

	private void OnPackSelected(CarouselButton button)
	{
		SetFlippedCard((DlcSelectButton)button);
	}

	public void ShowStorePage(DLCFrontendData dlcData)
	{
		m_dlcManager.ShowDLCStorePage(dlcData);
	}

	public void GotoChefSelectionScreen(DLCFrontendData data)
	{
		T17FrontendFlow.Instance.AutoOpenChefSelectionMenu(data);
		m_flippedCard = null;
		Close();
	}

	public void OnContinueGameButtonPressed(DLCFrontendData dlcData)
	{
		int lastSaveSlot = GameUtils.GetMetaGameProgress().GetLastSaveSlot(dlcData.m_DLCID);
		SelectSaveDialog selectSaveDialog = T17FrontendFlow.Instance.gameObject.RequireComponentRecursive<SelectSaveDialog>();
		StartCoroutine(selectSaveDialog.LoadSlot(dlcData.m_DLCID, lastSaveSlot));
	}

	public void OnNewGameButtonPressed(DLCFrontendData dlcData)
	{
		SelectSaveDialog selectSaveDialog = T17FrontendFlow.Instance.gameObject.RequireComponentRecursive<SelectSaveDialog>();
		selectSaveDialog.Mode = SaveDialogMode.NewGame;
		selectSaveDialog.DLC = dlcData.m_DLCID;
		selectSaveDialog.Show(m_CurrentGamepadUser, this, null, false);
	}

	public void OnLoadGameButtonPressed(DLCFrontendData dlcData)
	{
		SelectSaveDialog selectSaveDialog = T17FrontendFlow.Instance.gameObject.RequireComponentRecursive<SelectSaveDialog>();
		selectSaveDialog.Mode = SaveDialogMode.LoadGame;
		selectSaveDialog.DLC = dlcData.m_DLCID;
		selectSaveDialog.Show(m_CurrentGamepadUser, this, null, false);
	}
}
