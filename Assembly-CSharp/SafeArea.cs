using UnityEngine;

internal abstract class SafeArea : IQuantizedOption, OptionsData.IUnloadable, IOption
{
	private float m_area;

	public abstract string Label { get; }

	public OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.SafeArea;
		}
	}

	public abstract float SafeAreaAxis { get; set; }

	public int Quanta
	{
		get
		{
			return 10;
		}
	}

	public void Unload()
	{
		SafeAreaAxis = 1f;
	}

	public void SetOption(int _value)
	{
		m_area = _value;
		SafeAreaAxis = MathUtils.ClampedRemap(m_area, 0f, Quanta, 0.9f, 1f);
	}

	public int GetOption()
	{
		float f = MathUtils.ClampedRemap(SafeAreaAxis, 0.9f, 1f, 0f, Quanta);
		return Mathf.RoundToInt(f);
	}

	public void Commit()
	{
		SafeAreaAxis = MathUtils.ClampedRemap(m_area, 0f, Quanta, 0.9f, 1f);
	}
}
