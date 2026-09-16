using System;
using UnityEngine;
using UnityEngine.UI;

public class OptionsScreenListEntry : ScrollingListEntry
{
	[Serializable]
	public class OptionsDiscreteNameData : OptionsNameData
	{
		public string Label;

		public string[] Options;

		public int SelectedIndex;

		public override int Value
		{
			get
			{
				return SelectedIndex;
			}
			set
			{
				int num = Options.Length;
				SelectedIndex = (value + num) % num;
			}
		}

		public OptionsDiscreteNameData(string _label, string[] _options, int _option)
		{
			Label = _label;
			Options = _options;
			SelectedIndex = _option;
		}

		public override void Setup(OptionsScreenListEntry _entry)
		{
			_entry.m_optionName.text = Label;
			_entry.m_optionValue.text = Options[SelectedIndex];
			_entry.m_optionNumber.enabled = false;
		}
	}

	[Serializable]
	public class OptionsSliderNameData : OptionsNameData
	{
		public string Label;

		public float Prop;

		public int Quanta = 10;

		public override int Value
		{
			get
			{
				return Mathf.RoundToInt(Prop * (float)Quanta);
			}
			set
			{
				Prop = Mathf.Clamp01((float)value / (float)Quanta);
			}
		}

		public OptionsSliderNameData(string _label, int _value, int _quanta)
		{
			Label = _label;
			Value = _value;
			Quanta = _quanta;
		}

		public override void Setup(OptionsScreenListEntry _entry)
		{
			_entry.m_optionName.text = Label;
			_entry.m_optionValue.enabled = false;
			_entry.m_optionNumber.text = Mathf.RoundToInt(Prop * 100f).ToString();
		}
	}

	[Serializable]
	public abstract class OptionsNameData : ScrollingListUIContainer.NameData
	{
		public abstract int Value { get; set; }

		public abstract void Setup(OptionsScreenListEntry _entry);
	}

	[SerializeField]
	[AssignChild("LabelText", Editorbility.NonEditable)]
	private Text m_optionName;

	[SerializeField]
	[AssignChild("ValueText", Editorbility.NonEditable)]
	private Text m_optionValue;

	[SerializeField]
	[AssignChild("ValueNumber", Editorbility.NonEditable)]
	private Text m_optionNumber;

	private OptionsNameData m_optionsNameData;

	public int SelectedValueIndex
	{
		get
		{
			return m_optionsNameData.Value;
		}
		set
		{
			m_optionsNameData.Value = value;
			SetNameData(m_optionsNameData);
		}
	}

	public override void SetNameData(ScrollingListUIContainer.NameData _nameData)
	{
		m_optionsNameData = _nameData as OptionsNameData;
		m_optionsNameData.Setup(this);
	}
}
