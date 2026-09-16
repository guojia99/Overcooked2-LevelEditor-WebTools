using System;
using UnityEngine;
using UnityEngine.UI;

public class DlcSelectButton : CarouselButton
{
	public enum FlipState
	{
		Front = 0,
		Back = 1
	}

	private readonly string c_storeButtonText = "Text.DLC.Store";

	private readonly string c_newButtonText = "Text.DLC.NewGame";

	private readonly string c_continueButtonText = "Text.DLC.ContinueGame";

	private readonly string c_loadButtonText = "Text.DLC.LoadGame";

	private readonly string c_chefsButtonText = "Text.DLC.GotoChefs";

	[SerializeField]
	private GameObject m_front;

	[SerializeField]
	private GameObject m_back;

	[SerializeField]
	private Image m_imageFront;

	[SerializeField]
	private Image m_imageBack;

	[SerializeField]
	private T17Text m_titleTextFront;

	[SerializeField]
	private T17Text m_titleTextBack;

	[SerializeField]
	private T17Text m_descriptionText;

	[SerializeField]
	private T17Button m_topButton;

	[SerializeField]
	private T17Button m_middleButton;

	[SerializeField]
	private T17Button m_bottomButton;

	[SerializeField]
	private GameObject m_newContentIndicator;

	private FrontendDLCMenu m_buttonHandler;

	private DLCFrontendData m_dlcData;

	private bool m_isOwned;

	private bool m_hasSaveData;

	public FlipState m_flipState;

	private Animator m_flipAnimator;

	protected override void Initialise()
	{
		base.Initialise();
	}

	public void Setup(DLCFrontendData data)
	{
		m_flipAnimator = GetComponent<Animator>();
		m_dlcData = data;
		if (m_dlcData == null || !m_dlcData.IsAvailableOnThisPlatform())
		{
			base.gameObject.SetActive(false);
			return;
		}
		DLCManager dLCManager = GameUtils.RequireManager<DLCManager>();
		m_isOwned = dLCManager.IsDLCAvailable(m_dlcData);
		if (data.m_type == DLCType.Levels)
		{
			int lastSaveSlot = GameUtils.GetMetaGameProgress().GetLastSaveSlot(m_dlcData.m_DLCID);
			m_hasSaveData = lastSaveSlot != -1;
		}
		else
		{
			m_hasSaveData = false;
		}
		base.Initialise();
		SetupUI();
		T17Button t17Button = base.Button as T17Button;
		t17Button.OnButtonDeselect = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button.OnButtonDeselect, new T17Button.T17ButtonDelegate(OnButtonDeselect));
	}

	public void SetButtonCallbackHandler(FrontendDLCMenu handler)
	{
		m_buttonHandler = handler;
	}

	private void SetupUI()
	{
		if (m_imageFront == null || m_imageBack == null || m_titleTextFront == null || m_titleTextBack == null || m_descriptionText == null)
		{
			return;
		}
		m_imageFront.sprite = m_dlcData.m_PreviewImage;
		m_titleTextFront.SetLocalisedTextCatchAll(m_dlcData.m_NameLocalizationKey);
		m_titleTextBack.SetLocalisedTextCatchAll(m_dlcData.m_NameLocalizationKey);
		m_descriptionText.SetLocalisedTextCatchAll(m_dlcData.m_DescriptionLocalizationKey);
		if (m_topButton == null || m_middleButton == null || m_bottomButton == null)
		{
			return;
		}
		if (!m_isOwned)
		{
			SetupButton(m_topButton, null);
			SetupButton(m_middleButton, null);
			SetupButton(m_bottomButton, c_storeButtonText);
		}
		else
		{
			switch (m_dlcData.m_type)
			{
			case DLCType.Levels:
				if (m_hasSaveData)
				{
					SetupButton(m_topButton, c_continueButtonText);
				}
				else
				{
					SetupButton(m_topButton, null);
				}
				SetupButton(m_middleButton, c_newButtonText);
				SetupButton(m_bottomButton, c_loadButtonText);
				break;
			case DLCType.Avatars:
				SetupButton(m_topButton, null);
				SetupButton(m_middleButton, null);
				SetupButton(m_bottomButton, c_chefsButtonText);
				break;
			}
		}
		RefreshNewContentIndicator();
	}

	private void SetupButton(T17Button button, string label)
	{
		if (!(button != null))
		{
			return;
		}
		if (string.IsNullOrEmpty(label))
		{
			button.gameObject.SetActive(false);
			return;
		}
		button.gameObject.SetActive(true);
		T17Text componentInChildren = button.gameObject.GetComponentInChildren<T17Text>();
		if (componentInChildren != null)
		{
			componentInChildren.SetLocalisedTextCatchAll(label);
		}
	}

	private void Update()
	{
		RefreshNewContentIndicator();
	}

	private void RefreshNewContentIndicator()
	{
		SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
		MetaGameProgress metaGameProgress = saveManager.GetMetaGameProgress();
		if (metaGameProgress != null)
		{
			bool dLCSeen = metaGameProgress.GetDLCSeen(m_dlcData);
			m_newContentIndicator.SetActive(!dLCSeen);
		}
	}

	public void SetFlipState(FlipState flipState)
	{
		m_flipState = flipState;
		switch (m_flipState)
		{
		case FlipState.Front:
			if (m_flipAnimator != null)
			{
				m_flipAnimator.SetTrigger("PostcardFlip");
			}
			base.Button.interactable = true;
			m_back.SetActive(false);
			m_front.SetActive(true);
			break;
		case FlipState.Back:
			if (m_flipAnimator != null)
			{
				m_flipAnimator.SetTrigger("PostcardFlip");
			}
			base.Button.interactable = false;
			m_back.SetActive(true);
			m_front.SetActive(false);
			break;
		}
	}

	public Selectable GetFirstActiveButton()
	{
		if (m_back != null && m_back.gameObject.activeSelf)
		{
			if (m_topButton != null && m_topButton.isActiveAndEnabled && m_topButton.interactable)
			{
				return m_topButton;
			}
			if (m_middleButton != null && m_middleButton.isActiveAndEnabled && m_middleButton.interactable)
			{
				return m_middleButton;
			}
			if (m_bottomButton != null && m_bottomButton.isActiveAndEnabled && m_bottomButton.interactable)
			{
				return m_bottomButton;
			}
		}
		return base.Button;
	}

	public void OnTopButtonClicked()
	{
		if (m_buttonHandler != null)
		{
			m_buttonHandler.OnContinueGameButtonPressed(m_dlcData);
		}
	}

	public void OnMiddleButtonClicked()
	{
		if (m_buttonHandler != null)
		{
			m_buttonHandler.OnNewGameButtonPressed(m_dlcData);
		}
	}

	public void OnBottomButtonClicked()
	{
		if (!m_isOwned)
		{
			if (m_buttonHandler != null)
			{
				m_buttonHandler.ShowStorePage(m_dlcData);
			}
		}
		else if (m_dlcData.m_type == DLCType.Avatars)
		{
			if (m_buttonHandler != null)
			{
				m_buttonHandler.GotoChefSelectionScreen(m_dlcData);
			}
		}
		else if (m_dlcData.m_type == DLCType.Levels && m_buttonHandler != null)
		{
			m_buttonHandler.OnLoadGameButtonPressed(m_dlcData);
		}
	}

	private void OnButtonDeselect(T17Button sender)
	{
		SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
		MetaGameProgress metaGameProgress = saveManager.GetMetaGameProgress();
		if (metaGameProgress != null)
		{
			metaGameProgress.SetDLCSeen(m_dlcData);
		}
		RefreshNewContentIndicator();
	}
}
