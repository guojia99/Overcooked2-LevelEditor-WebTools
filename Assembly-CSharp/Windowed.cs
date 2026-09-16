using Steamworks;
using UnityEngine;

internal class Windowed : Option<Windowed.States>, OptionsData.IInit, OptionsData.IEnabled
{
	public enum States
	{
		Off = 0,
		On = 1
	}

	private bool m_fullscreen = true;

	public override string Label
	{
		get
		{
			return "OptionType.Windowed";
		}
	}

	public override OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.Graphics;
		}
	}

	public bool IsEnabled()
	{
		return !SteamUtils.IsSteamInBigPictureMode();
	}

	public void Init()
	{
	}

	protected override States GetState()
	{
		if (m_fullscreen)
		{
			return States.Off;
		}
		return States.On;
	}

	protected override void SetState(States _state)
	{
		m_fullscreen = ((_state != States.On) ? true : false);
	}

	public override void Commit()
	{
		if (Screen.fullScreen != m_fullscreen)
		{
			Screen.fullScreen = m_fullscreen;
		}
	}
}
