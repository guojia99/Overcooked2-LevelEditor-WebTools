using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(T17GridLayoutGroup))]
public class ThemeSelectRootMenu : CarouselRootMenu
{
	[SerializeField]
	private UIPlayerRootMenu m_playerRootMenu;

	private DLCManager m_dlcManager;

	protected override void Start()
	{
		base.Start();
		m_dlcManager = GameUtils.RequireManager<DLCManager>();
		DLCManagerBase.DLCUpdatedEvent = (GenericVoid)Delegate.Combine(DLCManagerBase.DLCUpdatedEvent, new GenericVoid(OnDLCUpdated));
		RefreshDLCButtons();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		DLCManagerBase.DLCUpdatedEvent = (GenericVoid)Delegate.Remove(DLCManagerBase.DLCUpdatedEvent, new GenericVoid(OnDLCUpdated));
	}

	protected override CarouselButton GetInitialButton()
	{
		CarouselButton buttonForTheme = GetButtonForTheme(SceneDirectoryData.LevelTheme.Random);
		return (!(buttonForTheme != null)) ? base.GetInitialButton() : buttonForTheme;
	}

	protected override void OnButtonFocusChanged(CarouselButton _button)
	{
		base.OnButtonFocusChanged(_button);
		if (_button != null)
		{
			Navigation navigation = _button.Button.navigation;
			if (m_playerRootMenu != null && m_playerRootMenu.CanFocusOnFirstPlayer(true))
			{
				navigation.selectOnDown = m_BorderSelectables.selectOnDown;
			}
			else
			{
				navigation.selectOnDown = null;
			}
			_button.Button.navigation = navigation;
		}
	}

	public Sprite GetSpriteForTheme(SceneDirectoryData.LevelTheme _theme)
	{
		ThemeSelectButton buttonForTheme = GetButtonForTheme(_theme);
		if (buttonForTheme != null)
		{
			return buttonForTheme.ThemeSprite;
		}
		return null;
	}

	public ThemeSelectButton GetButtonForTheme(SceneDirectoryData.LevelTheme _theme)
	{
		CarouselButton[] buttons = base.Buttons;
		for (int i = 0; i < buttons.Length; i++)
		{
			ThemeSelectButton themeSelectButton = buttons[i] as ThemeSelectButton;
			if (themeSelectButton != null && themeSelectButton.Theme == _theme)
			{
				return themeSelectButton;
			}
		}
		return null;
	}

	public ThemeSelectButton GetRandomTheme()
	{
		ThemeSelectButton[] array = base.Buttons.ConvertAll((CarouselButton x) => (ThemeSelectButton)x);
		return array.FindAll((ThemeSelectButton x) => x.Theme != SceneDirectoryData.LevelTheme.Random).GetRandomElement();
	}

	public void DisallowTheme(SceneDirectoryData.LevelTheme _theme)
	{
		ThemeSelectButton buttonForTheme = GetButtonForTheme(_theme);
		DisallowButton(buttonForTheme);
	}

	protected override bool IsButtonInteractable(CarouselButton _button)
	{
		DlcThemeSelectButton dlcThemeSelectButton = _button as DlcThemeSelectButton;
		if (dlcThemeSelectButton == null || dlcThemeSelectButton.Purchased)
		{
			return base.IsButtonInteractable(_button);
		}
		return false;
	}

	private void OnDLCUpdated()
	{
		RefreshDLCButtons();
	}

	private void RefreshDLCButtons()
	{
		CarouselButton[] buttons = base.Buttons;
		for (int i = 0; i < buttons.Length; i++)
		{
			DlcThemeSelectButton dlcThemeSelectButton = buttons[i] as DlcThemeSelectButton;
			if (dlcThemeSelectButton != null)
			{
				dlcThemeSelectButton.Purchased = dlcThemeSelectButton.DLCData == null || m_dlcManager.IsDLCAvailable(dlcThemeSelectButton.DLCData);
			}
		}
	}
}
