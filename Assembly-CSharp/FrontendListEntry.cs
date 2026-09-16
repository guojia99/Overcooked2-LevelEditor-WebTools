using System;
using UnityEngine;
using UnityEngine.UI;

public class FrontendListEntry : ScrollingListEntry
{
	[Serializable]
	public class NameData : ScrollingListUIContainer.NameData
	{
		public string Name;

		public bool Enable;

		public bool Completed;

		public NameData(string _name, bool _enabled, bool _completed)
		{
			Name = _name;
			Enable = _enabled;
			Completed = _completed;
		}

		public bool Equals(NameData obj)
		{
			return Name == obj.Name && Enable == obj.Enable && Completed == obj.Completed;
		}
	}

	public override void SetNameData(ScrollingListUIContainer.NameData _nameData)
	{
		NameData nameData = _nameData as NameData;
		Text text = base.gameObject.RequestComponentRecursive<Text>();
		text.text = nameData.Name;
		text.color = GetTextColour(nameData);
	}

	private Color GetTextColour(NameData _data)
	{
		if (!_data.Enable)
		{
			return new Color(0.5f, 0.5f, 0.5f, 1f);
		}
		return new Color(0f, 0f, 0f, 1f);
	}
}
