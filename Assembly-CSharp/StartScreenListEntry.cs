using System;
using UnityEngine;
using UnityEngine.UI;

public class StartScreenListEntry : FrontendListEntry
{
	[Serializable]
	public new class NameData : FrontendListEntry.NameData
	{
		public Color EnabledColor;

		public bool IsAvailable;

		public string AvailableMessage;

		public NameData(string _name, bool _enabled, bool _completed)
			: base(_name, _enabled, _completed)
		{
			EnabledColor = Color.black;
			IsAvailable = false;
			AvailableMessage = string.Empty;
		}
	}

	public override void SetNameData(ScrollingListUIContainer.NameData _nameData)
	{
		NameData nameData = _nameData as NameData;
		GameObject obj = base.transform.Find("SelectedText").gameObject;
		Text text = obj.RequestComponentRecursive<Text>();
		Outline[] array = text.gameObject.RequestComponents<Outline>();
		text.text = nameData.Name;
		text.color = GetTextColour(nameData);
		for (int i = 0; i < array.Length; i++)
		{
			Color effectColor = array[i].effectColor;
			effectColor.r = text.color.r;
			effectColor.g = text.color.g;
			effectColor.b = text.color.b;
			array[i].effectColor = effectColor;
		}
		GameObject obj2 = base.transform.Find("AvailableText").gameObject;
		Text text2 = obj2.RequestComponentRecursive<Text>();
		text2.enabled = nameData.IsAvailable;
		text2.text = nameData.AvailableMessage;
	}

	protected Color GetTextColour(NameData _data)
	{
		if (!_data.Enable)
		{
			return new Color(0.5f, 0.5f, 0.5f, 1f);
		}
		return _data.EnabledColor;
	}
}
