using UnityEngine;

internal class Quality : Option<Quality.Levels>
{
	public enum Levels
	{
		SuperQuick = 0,
		Quick = 1,
		Standard = 2,
		Dreamy = 3
	}

	private int m_level;

	public override string Label
	{
		get
		{
			return "OptionType.Quality";
		}
	}

	public override OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.Graphics;
		}
	}

	protected override Levels GetState()
	{
		return (Levels)m_level;
	}

	protected override void SetState(Levels _state)
	{
		m_level = (int)_state;
	}

	public override void Commit()
	{
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		VSync vSync = null;
		int option = 0;
		vSync = ((!(metaGameProgress != null)) ? null : ((VSync)metaGameProgress.AccessOptionsData.GetOption(OptionsData.OptionType.VSync)));
		if (vSync != null)
		{
			option = vSync.GetOption();
		}
		if (QualitySettings.GetQualityLevel() != m_level)
		{
			QualitySettings.SetQualityLevel(m_level);
		}
		if (vSync != null)
		{
			vSync.SetOption(option);
		}
	}
}
