using System.Collections.Generic;

public class Prefs
{
	private static Dictionary<string, int> m_prefs = new Dictionary<string, int>();

	public static void Setup(string label, int v = 0)
	{
		SaveManager saveManager = GameUtils.RequestManager<SaveManager>();
		if (!(saveManager != null))
		{
			return;
		}
		MetaGameProgress metaGameProgress = saveManager.GetMetaGameProgress();
		if (metaGameProgress != null)
		{
			int value = 0;
			if (metaGameProgress.SaveData.Get("P_" + label, out value, v))
			{
				m_prefs[label] = value;
			}
		}
	}

	public static bool HasKey(string label)
	{
		return m_prefs.ContainsKey(label);
	}

	public static int GetInt(string label, int v)
	{
		return m_prefs.SafeGet(label, v);
	}

	public static void SetInt(string label, int v)
	{
		m_prefs.SafeAdd(label, v);
	}

	public static void Save()
	{
		SaveManager saveManager = GameUtils.RequestManager<SaveManager>();
		if (!(saveManager != null))
		{
			return;
		}
		MetaGameProgress metaGameProgress = saveManager.GetMetaGameProgress();
		if (!(metaGameProgress != null))
		{
			return;
		}
		foreach (KeyValuePair<string, int> pref in m_prefs)
		{
			metaGameProgress.SaveData.Set("P_" + pref.Key, pref.Value);
		}
	}
}
