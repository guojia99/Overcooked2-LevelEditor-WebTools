using UnityEngine;

internal class VSync : Option<VSync.States>, OptionsData.IEnabled
{
	public enum States
	{
		Off = 0,
		On = 1
	}

	private int m_vsync;

	public override string Label
	{
		get
		{
			return "OptionType.VSync";
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
		return true;
	}

	protected override States GetState()
	{
		if (m_vsync == 0)
		{
			return States.Off;
		}
		return States.On;
	}

	protected override void SetState(States _state)
	{
		m_vsync = ((_state == States.On) ? 1 : 0);
	}

	public override void Commit()
	{
		if (QualitySettings.vSyncCount != m_vsync)
		{
			QualitySettings.vSyncCount = m_vsync;
		}
	}
}
