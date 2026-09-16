using UnityEngine;

public class NewContentPopup : FrontendMenuBehaviour
{
	[SerializeField]
	private T17Text m_name;

	[SerializeField]
	private T17Text m_description;

	[SerializeField]
	private T17Image m_image;

	[SerializeField]
	private T17Button m_confirmButton;

	[SerializeField]
	private T17Button m_storeButton;

	[SerializeField]
	private RectTransform m_seasonPass;

	[HideInInspector]
	public PopupData m_popupData;

	private PlayerManager m_playerManager;

	protected override void Awake()
	{
		base.Awake();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _p, GamepadUser _n)
	{
		if (base.CachedEventSystem != null)
		{
			base.CachedEventSystem.SetSelectedGameObject(m_confirmButton.GetGameobject());
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		switch (m_popupData.m_kind)
		{
		case PopupData.Kind.DLC:
		{
			DLCFrontendData dlcData = m_popupData.m_dlcData;
			m_name.SetLocalisedTextCatchAll(dlcData.m_NameLocalizationKey);
			m_description.SetLocalisedTextCatchAll(dlcData.m_DescriptionLocalizationKey);
			m_image.sprite = dlcData.m_PopupImage;
			DLCManager dLCManager = GameUtils.RequestManager<DLCManager>();
			m_storeButton.gameObject.SetActive(!dLCManager.IsDLCAvailable(m_popupData.m_dlcData));
			m_seasonPass.gameObject.SetActive(dlcData.m_IsSeasonPassDLC);
			break;
		}
		case PopupData.Kind.Update:
			m_name.SetLocalisedTextCatchAll(m_popupData.m_nameLocalisationKey);
			m_description.SetLocalisedTextCatchAll(m_popupData.m_descriptionLocalisationKey);
			m_image.sprite = m_popupData.m_image;
			m_storeButton.gameObject.SetActive(false);
			m_seasonPass.gameObject.SetActive(false);
			break;
		}
		return true;
	}

	public void OnPopupConfirm()
	{
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
		Hide();
	}

	public void OnPopupStore()
	{
		DLCManager dLCManager = GameUtils.RequireManager<DLCManager>();
		dLCManager.ShowDLCStorePage(m_popupData.m_dlcData);
	}
}
