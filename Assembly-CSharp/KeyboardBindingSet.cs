using System;
using System.Collections.Generic;
using InControl;

public class KeyboardBindingSet
{
	public Dictionary<ControlPadInput.Button, List<Key>> m_ButtonBindings = new Dictionary<ControlPadInput.Button, List<Key>>();

	public Dictionary<ControlPadInput.Value, List<Key>> m_PositiveValueBindings = new Dictionary<ControlPadInput.Value, List<Key>>();

	public Dictionary<ControlPadInput.Value, List<Key>> m_NegativeValueBindings = new Dictionary<ControlPadInput.Value, List<Key>>();

	public void CopyFrom(KeyboardBindingSet original)
	{
		CopyMap(original.m_ButtonBindings, m_ButtonBindings);
		CopyMap(original.m_PositiveValueBindings, m_PositiveValueBindings);
		CopyMap(original.m_NegativeValueBindings, m_NegativeValueBindings);
	}

	public void Save(GlobalSave saveData, string prefix)
	{
		SaveMap(prefix + "_BB", m_ButtonBindings, saveData);
		SaveMap(prefix + "_PV", m_PositiveValueBindings, saveData);
		SaveMap(prefix + "_NV", m_NegativeValueBindings, saveData);
	}

	public bool Load(GlobalSave saveData, string prefix)
	{
		bool flag = true;
		flag &= LoadMap(prefix + "_BB", m_ButtonBindings, saveData);
		flag &= LoadMap(prefix + "_PV", m_PositiveValueBindings, saveData);
		return flag & LoadMap(prefix + "_NV", m_NegativeValueBindings, saveData);
	}

	public void CopyMap<T>(Dictionary<T, List<Key>> from, Dictionary<T, List<Key>> to)
	{
		to.Clear();
		foreach (KeyValuePair<T, List<Key>> item in from)
		{
			List<Key> list = new List<Key>();
			foreach (Key item2 in item.Value)
			{
				list.Add(item2);
			}
			to.Add(item.Key, list);
		}
	}

	public void SaveMap<T>(string name, Dictionary<T, List<Key>> map, GlobalSave saveData)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<T, List<Key>> item in map)
		{
			string text = string.Empty;
			foreach (Key item2 in item.Value)
			{
				if (!string.IsNullOrEmpty(text))
				{
					text += ",";
				}
				text += item2;
			}
			dictionary.Add(item.Key.ToString(), text);
		}
		saveData.Set(name, dictionary);
	}

	public bool LoadMap<T>(string name, Dictionary<T, List<Key>> map, GlobalSave saveData)
	{
		map.Clear();
		Dictionary<string, string> value;
		if (saveData.Get(name, out value, (Dictionary<string, string>)null))
		{
			try
			{
				foreach (KeyValuePair<string, string> item in value)
				{
					List<Key> value2 = new List<Key>();
					if (!string.IsNullOrEmpty(item.Value))
					{
						List<string> list = new List<string>(item.Value.Split(','));
						value2 = list.ConvertAll((string x) => (Key)Enum.Parse(typeof(Key), x));
					}
					map.Add((T)Enum.Parse(typeof(T), item.Key), value2);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
		return false;
	}
}
