public class DlcSelectRootMenu : CarouselRootMenu
{
	protected override bool IsButtonInteractable(CarouselButton _button)
	{
		DlcSelectButton dlcSelectButton = _button as DlcSelectButton;
		if (dlcSelectButton != null)
		{
			return dlcSelectButton.m_flipState == DlcSelectButton.FlipState.Front;
		}
		return true;
	}

	protected override CarouselButton GetInitialButton()
	{
		return base.Buttons[0];
	}
}
