using UnityEngine;
using UnityEngine.UI;

public class T17DialogBox : MonoBehaviour, IT17EventHelper
{
	public delegate void DialogEvent();

	public delegate void DialogEventInfo(T17DialogBox dialog = null);

	public enum Symbols
	{
		Unassigned = 0,
		Warning = 1,
		Error = 2,
		Spinner = 3
	}

	public DialogEvent OnConfirm;

	public DialogEvent OnDecline;

	public DialogEvent OnCancel;

	public DialogEventInfo OnUpdate;

	public T17Button m_ConfirmButton;

	public T17Button m_DeclineButton;

	public T17Button m_CancelButton;

	public T17Text m_Title;

	public T17Text m_Message;

	public bool IsActive;

	private Transform m_ParentToOnHide;

	public T17EventSystem m_EventSystemForGamer;

	private GameObject m_ObjectSelectedBeforeShow;

	private bool m_hasConfirm;

	private bool m_hasDecline;

	private bool m_hasCancel;

	[SerializeField]
	private Animator m_animator;

	private bool m_canPause;

	private Suppressor m_activeSpinner;

	public void Initialize(string title, string message, string confirmBtn = "", string declineBtn = "", string cancelBtn = "", Symbols symbol = Symbols.Warning, bool bLocalizeTitle = true, bool bLocalizeMessage = true, bool bCanPause = false)
	{
		m_hasConfirm = !string.IsNullOrEmpty(confirmBtn);
		m_hasDecline = !string.IsNullOrEmpty(declineBtn);
		m_hasCancel = !string.IsNullOrEmpty(cancelBtn);
		m_canPause = bCanPause;
		if (m_animator != null)
		{
			if (!TimeManager.IsPaused(base.gameObject) || ShouldUnpause())
			{
				m_animator.speed = 1f;
			}
			else
			{
				m_animator.speed = 0f;
			}
		}
		T17FrontendFlow instance = T17FrontendFlow.Instance;
		if (instance != null)
		{
			FrontendRootMenu rootmenu = instance.m_Rootmenu;
			if (rootmenu != null)
			{
				rootmenu.SetLegendText("Text.Menu.LegendAccept");
			}
		}
		if (m_ConfirmButton != null)
		{
			m_ConfirmButton.gameObject.SetActive(m_hasConfirm);
			Navigation navigation = m_ConfirmButton.navigation;
			if (m_hasDecline)
			{
				navigation.selectOnRight = m_DeclineButton;
			}
			else if (m_hasCancel)
			{
				navigation.selectOnRight = m_CancelButton;
			}
			else
			{
				navigation.selectOnRight = null;
			}
			m_ConfirmButton.navigation = navigation;
			if (string.IsNullOrEmpty(confirmBtn))
			{
				confirmBtn = "Text.Dialog.Prompt.Yes";
			}
			SetButtonText(m_ConfirmButton, confirmBtn);
		}
		OnConfirm = null;
		OnUpdate = null;
		if (m_DeclineButton != null)
		{
			m_DeclineButton.gameObject.SetActive(m_hasDecline);
			Navigation navigation2 = m_DeclineButton.navigation;
			if (m_hasConfirm)
			{
				navigation2.selectOnLeft = m_ConfirmButton;
			}
			else
			{
				navigation2.selectOnLeft = null;
			}
			if (m_hasCancel)
			{
				navigation2.selectOnRight = m_CancelButton;
			}
			else
			{
				navigation2.selectOnRight = null;
			}
			m_DeclineButton.navigation = navigation2;
			if (string.IsNullOrEmpty(declineBtn))
			{
				declineBtn = "Text.Dialog.Prompt.No";
			}
			SetButtonText(m_DeclineButton, declineBtn);
		}
		OnDecline = null;
		if (m_CancelButton != null)
		{
			m_CancelButton.gameObject.SetActive(m_hasCancel);
			Navigation navigation3 = m_CancelButton.navigation;
			if (m_hasDecline)
			{
				navigation3.selectOnLeft = m_DeclineButton;
			}
			else if (m_hasConfirm)
			{
				navigation3.selectOnLeft = m_ConfirmButton;
			}
			else
			{
				navigation3.selectOnLeft = null;
			}
			m_CancelButton.navigation = navigation3;
			if (string.IsNullOrEmpty(cancelBtn))
			{
				cancelBtn = "Text.Dialog.Prompt.Cancel";
			}
			SetButtonText(m_CancelButton, cancelBtn);
		}
		OnCancel = null;
		if (m_Message != null)
		{
			m_Message.text = message;
			m_Message.m_bNeedsLocalization = bLocalizeMessage;
			m_Message.SetNewPlaceHolder(message);
			m_Message.SetNewLocalizationTag(message);
		}
		if (m_Title != null)
		{
			m_Title.text = title;
			m_Title.m_bNeedsLocalization = bLocalizeTitle;
			m_Title.SetNewPlaceHolder(title);
			m_Title.SetNewLocalizationTag(title);
		}
		SetSymbol(symbol);
	}

	public void SetSymbol(Symbols symbol)
	{
		if (m_activeSpinner != null)
		{
			m_activeSpinner.Release();
			m_activeSpinner = null;
		}
		switch (symbol)
		{
		case Symbols.Unassigned:
			break;
		case Symbols.Error:
			m_activeSpinner = SpinnerIconManager.Instance.Show(SpinnerIconManager.SpinnerIconType.Error, this);
			break;
		case Symbols.Warning:
			m_activeSpinner = SpinnerIconManager.Instance.Show(SpinnerIconManager.SpinnerIconType.Warning, this);
			break;
		case Symbols.Spinner:
			m_activeSpinner = SpinnerIconManager.Instance.Show(SpinnerIconManager.SpinnerIconType.SpinnerDialog, this);
			break;
		}
	}

	public void SetMessage(string strMessage, bool bLocalizeMessage)
	{
		if (m_Message != null)
		{
			m_Message.text = strMessage;
			m_Message.m_bNeedsLocalization = bLocalizeMessage;
			m_Message.SetNewPlaceHolder(strMessage);
			m_Message.SetNewLocalizationTag(strMessage);
		}
	}

	public void Show()
	{
		T17DialogBoxManager.RequestDialogShow(this, OnAllowedToShow);
	}

	private void OnAllowedToShow()
	{
		base.gameObject.SetActive(true);
		IsActive = true;
		if (null != m_EventSystemForGamer)
		{
			m_ObjectSelectedBeforeShow = m_EventSystemForGamer.GetPendingSelectedGameObject();
			if (m_ObjectSelectedBeforeShow == null)
			{
				m_ObjectSelectedBeforeShow = m_EventSystemForGamer.GetLastRequestedSelectedGameobject();
			}
			if (m_ObjectSelectedBeforeShow == null)
			{
				m_ObjectSelectedBeforeShow = m_EventSystemForGamer.currentSelectedGameObject;
			}
		}
		Focus();
	}

	public void Focus()
	{
		if (null != m_EventSystemForGamer)
		{
			if (!base.gameObject.activeInHierarchy || !IsActive)
			{
				Hide();
				return;
			}
			GameObject gameObject = m_EventSystemForGamer.GetPendingSelectedGameObject();
			if (gameObject == null || gameObject.IsInHierarchyOf(base.gameObject))
			{
				gameObject = m_EventSystemForGamer.GetLastRequestedSelectedGameobject();
				if (gameObject == null || gameObject.IsInHierarchyOf(base.gameObject))
				{
					gameObject = null;
				}
			}
			if (gameObject != null && m_ObjectSelectedBeforeShow != gameObject)
			{
				m_ObjectSelectedBeforeShow = gameObject;
			}
			GameObject gameObject2 = null;
			m_EventSystemForGamer.SetSelectedGameObject(null);
			if (m_DeclineButton != null && m_DeclineButton.gameObject.activeSelf)
			{
				gameObject2 = m_DeclineButton.gameObject;
			}
			else if (m_CancelButton != null && m_CancelButton.gameObject.activeSelf)
			{
				gameObject2 = m_CancelButton.gameObject;
			}
			else if (m_ConfirmButton != null && m_ConfirmButton.gameObject.activeSelf)
			{
				gameObject2 = m_ConfirmButton.gameObject;
			}
			if (gameObject2 != null)
			{
				bool flag = true;
				T17StandaloneInputModule t17StandaloneInputModule = (T17StandaloneInputModule)m_EventSystemForGamer.currentInputModule;
				if (t17StandaloneInputModule != null && t17StandaloneInputModule.WasUsingMouse && (m_EventSystemForGamer.currentSelectedGameObject == null || m_EventSystemForGamer.currentSelectedGameObject == gameObject2))
				{
					flag = false;
				}
				if (flag)
				{
					m_EventSystemForGamer.SetSelectedGameObject(gameObject2.gameObject);
				}
			}
			return;
		}
		T17EventSystem t17EventSystem = null;
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser(EngagementSlot.One);
		if (user != null)
		{
			t17EventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
			if (t17EventSystem != null)
			{
				SetEventSystem(t17EventSystem);
			}
		}
	}

	public bool HasButtons()
	{
		return m_hasConfirm || m_hasDecline || m_hasCancel;
	}

	public bool HasFocus()
	{
		bool result = false;
		if (null != m_EventSystemForGamer)
		{
			GameObject lastRequestedSelectedGameobject = m_EventSystemForGamer.GetLastRequestedSelectedGameobject();
			if (null != lastRequestedSelectedGameobject)
			{
				result = lastRequestedSelectedGameobject == m_DeclineButton.gameObject || lastRequestedSelectedGameobject == m_CancelButton.gameObject || lastRequestedSelectedGameobject == m_ConfirmButton.gameObject;
			}
		}
		return result;
	}

	public void ReparentToOnHide(Transform parent)
	{
		m_ParentToOnHide = parent;
	}

	public void Hide()
	{
		base.gameObject.SetActive(false);
		if (m_activeSpinner != null)
		{
			m_activeSpinner.Release();
			m_activeSpinner = null;
		}
		IsActive = false;
		if (m_ParentToOnHide != null)
		{
			if (base.transform.parent.childCount <= 2)
			{
				base.transform.parent.gameObject.SetActive(false);
			}
			base.transform.SetParent(m_ParentToOnHide, false);
			base.transform.localScale = Vector3.one;
			base.transform.localPosition = Vector3.zero;
		}
		SetSelectedGameobjectToPrepopup();
		T17DialogBoxManager.ReleaseMe(this);
		m_ObjectSelectedBeforeShow = null;
	}

	public void SetSelectedGameobjectToPrepopup()
	{
		if (m_EventSystemForGamer != null)
		{
			m_EventSystemForGamer.ForceDeselectSelectionObject();
			if (m_ObjectSelectedBeforeShow != null && m_ObjectSelectedBeforeShow.activeInHierarchy)
			{
				m_EventSystemForGamer.SetSelectedGameObject(m_ObjectSelectedBeforeShow);
			}
		}
	}

	private void SetButtonText(T17Button button, string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			T17Text componentInChildren = button.GetComponentInChildren<T17Text>(true);
			if (componentInChildren != null)
			{
				componentInChildren.SetNewPlaceHolder(text);
				componentInChildren.SetNewLocalizationTag(text);
			}
		}
	}

	private bool IsPaused()
	{
		if (m_animator != null)
		{
			return m_animator.speed < 0.001f;
		}
		return false;
	}

	public void Confirm()
	{
		Hide();
		if (OnConfirm != null)
		{
			OnConfirm();
		}
	}

	public void Decline()
	{
		Hide();
		if (OnDecline != null)
		{
			OnDecline();
		}
	}

	public void Cancel()
	{
		Hide();
		if (OnCancel != null)
		{
			OnCancel();
		}
	}

	private void Update()
	{
		if (ShouldUnpause() && m_animator != null)
		{
			m_animator.speed = 1f;
		}
		if (OnUpdate != null)
		{
			OnUpdate(this);
		}
	}

	private bool ShouldUnpause()
	{
		return IsPaused() && (!TimeManager.IsPaused(base.gameObject) || !m_canPause);
	}

	public GameObject GetGameobject()
	{
		return base.gameObject;
	}

	public void SetEventSystem(T17EventSystem gamersEventSystem = null)
	{
		m_EventSystemForGamer = gamersEventSystem;
		if (m_ConfirmButton != null)
		{
			m_ConfirmButton.SetEventSystem(gamersEventSystem);
		}
		if (m_DeclineButton != null)
		{
			m_DeclineButton.SetEventSystem(gamersEventSystem);
		}
		if (m_CancelButton != null)
		{
			m_CancelButton.SetEventSystem(gamersEventSystem);
		}
	}

	public T17EventSystem GetDomain()
	{
		return m_EventSystemForGamer;
	}
}
