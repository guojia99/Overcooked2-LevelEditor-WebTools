using System;
using UnityEngine;

public class OptionsMenuUIController : ScrollingListUIController
{
	[Serializable]
	private class DiscreteEntry : GenericEntry<OptionsScreenListEntry.OptionsDiscreteNameData>
	{
		public DiscreteEntry(OptionsScreenListEntry.OptionsDiscreteNameData _data, int _order)
			: base(_data, _order)
		{
		}
	}

	[Serializable]
	private class SliderEntry : GenericEntry<OptionsScreenListEntry.OptionsSliderNameData>
	{
		public SliderEntry(OptionsScreenListEntry.OptionsSliderNameData _data, int _order)
			: base(_data, _order)
		{
		}
	}

	private class GenericEntry<T> : Entry where T : OptionsScreenListEntry.OptionsNameData
	{
		public T Data;

		public override OptionsScreenListEntry.OptionsNameData BaseData
		{
			get
			{
				return Data;
			}
		}

		public GenericEntry(T _data, int _order)
		{
			Data = _data;
			Order = _order;
		}
	}

	private abstract class Entry
	{
		public int Order = -1;

		public abstract OptionsScreenListEntry.OptionsNameData BaseData { get; }
	}

	[SerializeField]
	private LocalisedText m_titleObject;

	[SerializeField]
	private DiscreteEntry[] m_discreteEntries = new DiscreteEntry[0];

	[SerializeField]
	private SliderEntry[] m_sliderEntries = new SliderEntry[0];

	public void SetNames(string _title, OptionsScreenListEntry.OptionsNameData[] _nameData)
	{
		m_titleObject.text = _title;
		m_discreteEntries = new DiscreteEntry[0];
		m_sliderEntries = new SliderEntry[0];
		for (int i = 0; i < _nameData.Length; i++)
		{
			if (_nameData[i] is OptionsScreenListEntry.OptionsDiscreteNameData)
			{
				ArrayUtils.PushBack(ref m_discreteEntries, new DiscreteEntry(_nameData[i] as OptionsScreenListEntry.OptionsDiscreteNameData, i));
			}
			if (_nameData[i] is OptionsScreenListEntry.OptionsSliderNameData)
			{
				ArrayUtils.PushBack(ref m_sliderEntries, new SliderEntry(_nameData[i] as OptionsScreenListEntry.OptionsSliderNameData, i));
			}
		}
		OnSetNames();
	}

	protected override NameData[] GetNameData()
	{
		Entry[] array = new Entry[m_sliderEntries.Length + m_discreteEntries.Length];
		m_sliderEntries.CopyTo(array, 0);
		m_discreteEntries.CopyTo(array, m_sliderEntries.Length);
		Array.Sort(array, (Entry x1, Entry x2) => x1.Order.CompareTo(x2.Order));
		return array.ConvertAll((Entry x) => x.BaseData);
	}

	public void MoveLeft()
	{
		MoveSelection(-1);
	}

	public void MoveRight()
	{
		MoveSelection(1);
	}

	public int GetOptionValue()
	{
		int selection = GetSelection();
		OptionsScreenListEntry optionsScreenListEntry = GetUnselectedEntries()[selection] as OptionsScreenListEntry;
		return optionsScreenListEntry.SelectedValueIndex;
	}

	private void MoveSelection(int _moveValue)
	{
		int selection = GetSelection();
		OptionsScreenListEntry optionsScreenListEntry = GetUnselectedEntries()[selection] as OptionsScreenListEntry;
		optionsScreenListEntry.SelectedValueIndex += _moveValue;
		OptionsScreenListEntry optionsScreenListEntry2 = GetSelectedEntry() as OptionsScreenListEntry;
		optionsScreenListEntry2.SelectedValueIndex = optionsScreenListEntry.SelectedValueIndex;
	}
}
