using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatSystem : MonoBehaviour
{
	private enum STAT_TYPE
	{
		ST_COUNTER = 0,
		ST_ID_HOLDER = 1
	}

	private class Stat
	{
		public string m_Name;

		public int m_ID;

		public float m_Value;

		public StatRule[] m_RefStats;

		public STAT_TYPE m_Type;

		public Hashtable m_IDHolder;

		public StatValidationList m_validationList;

		public Stat()
		{
			m_Name = "STAT:NONAME";
			m_ID = 0;
			m_Value = 0f;
			m_RefStats = null;
			m_Type = STAT_TYPE.ST_COUNTER;
			m_IDHolder = null;
		}
	}

	public enum StatCompare
	{
		EQUAL = 0,
		GREATER_THAN_EQUAL = 1,
		HAS_ID = 2
	}

	private class StatRule
	{
		public Stat m_Stat;

		public float m_RefValue;

		public StatCompare m_Compare;

		public Trophy m_RefTrophy;

		public StatRule()
		{
			m_Stat = null;
			m_RefValue = 0f;
		}

		public bool Check(ref bool bRuleJustMadeTrueByNewValue, float newValue)
		{
			bool result = false;
			if (m_Stat.m_Type == STAT_TYPE.ST_COUNTER)
			{
				switch (m_Compare)
				{
				case StatCompare.EQUAL:
					if (m_Stat.m_Value == m_RefValue)
					{
						result = true;
					}
					break;
				case StatCompare.GREATER_THAN_EQUAL:
					if (m_Stat.m_Value >= m_RefValue)
					{
						result = true;
					}
					break;
				}
			}
			else
			{
				switch (m_Compare)
				{
				case StatCompare.EQUAL:
					if ((float)m_Stat.m_IDHolder.Keys.Count == m_RefValue)
					{
						result = true;
					}
					break;
				case StatCompare.GREATER_THAN_EQUAL:
					if ((float)m_Stat.m_IDHolder.Keys.Count >= m_RefValue)
					{
						result = true;
					}
					break;
				case StatCompare.HAS_ID:
					if (m_Stat.m_IDHolder.ContainsKey((int)m_RefValue))
					{
						result = true;
						if ((int)m_RefValue == (int)newValue)
						{
							bRuleJustMadeTrueByNewValue = true;
						}
						else
						{
							bRuleJustMadeTrueByNewValue = false;
						}
					}
					break;
				}
			}
			return result;
		}

		public float GetProgress()
		{
			if (m_Stat.m_Type == STAT_TYPE.ST_COUNTER)
			{
				StatCompare compare = m_Compare;
				if (compare == StatCompare.EQUAL || compare == StatCompare.GREATER_THAN_EQUAL)
				{
					return Mathf.Clamp01(m_Stat.m_Value / m_RefValue);
				}
			}
			else if (m_Stat.m_Type == STAT_TYPE.ST_ID_HOLDER)
			{
				switch (m_Compare)
				{
				case StatCompare.EQUAL:
				case StatCompare.GREATER_THAN_EQUAL:
					return Mathf.Clamp01((float)m_Stat.m_IDHolder.Count / m_RefValue);
				case StatCompare.HAS_ID:
					return (!m_Stat.m_IDHolder.ContainsKey((int)m_RefValue)) ? 0f : 1f;
				}
			}
			return 0f;
		}
	}

	private enum Combiner
	{
		C_NA = 0,
		C_AND = 1,
		C_OR = 2
	}

	private class Trophy
	{
		public string m_Name;

		public string m_APIName;

		public int m_TrophyID;

		public float m_TrophyProgress;

		public bool m_Unlocked;

		public StatRule[] m_Rules;

		public int m_RuleCount;

		public Combiner m_Combiner;

		public Trophy()
		{
			m_Name = "XX";
			m_APIName = "XX";
			m_TrophyID = -1;
			m_TrophyProgress = 0f;
			m_Unlocked = false;
		}

		public bool Check()
		{
			bool flag = false;
			switch (m_Combiner)
			{
			case Combiner.C_NA:
				flag = false;
				break;
			case Combiner.C_AND:
				flag = true;
				break;
			case Combiner.C_OR:
				flag = false;
				break;
			}
			for (uint num = 0u; num < m_Rules.Length; num++)
			{
				bool bRuleJustMadeTrueByNewValue = false;
				bool flag2 = m_Rules[num].Check(ref bRuleJustMadeTrueByNewValue, 0f);
				switch (m_Combiner)
				{
				case Combiner.C_NA:
					flag = flag2;
					break;
				case Combiner.C_AND:
					flag = flag && flag2;
					break;
				case Combiner.C_OR:
					flag = flag || flag2;
					break;
				}
			}
			return flag;
		}

		public float GetProgress()
		{
			int num = m_Rules.Length;
			float num2 = 0f;
			for (int i = 0; i < num; i++)
			{
				num2 += m_Rules[i].GetProgress();
			}
			return Mathf.Clamp01(num2 / (float)num);
		}
	}

	private SaveManager m_saveManager;

	private VoidGeneric<int, float, float> m_statValueChangedCallback = delegate
	{
	};

	private VoidGeneric<int> m_trophyUnlockCallback = delegate
	{
	};

	private VoidGeneric<int, float> m_trophyProgressCallback = delegate
	{
	};

	private FastList<Stat> m_Stats;

	private StatRule[] m_StatsRules;

	private FastList<Trophy> m_Trophies;

	private void Awake()
	{
		m_saveManager = GameUtils.RequestManager<SaveManager>();
	}

	public void RegisterStatValueChanged(VoidGeneric<int, float, float> _callback)
	{
		m_statValueChangedCallback = (VoidGeneric<int, float, float>)Delegate.Combine(m_statValueChangedCallback, _callback);
	}

	public void UnregisterStatValueChanged(VoidGeneric<int, float, float> _callback)
	{
		m_statValueChangedCallback = (VoidGeneric<int, float, float>)Delegate.Remove(m_statValueChangedCallback, _callback);
	}

	public void RegisterTrophyUnlock(VoidGeneric<int> _callback)
	{
		m_trophyUnlockCallback = (VoidGeneric<int>)Delegate.Combine(m_trophyUnlockCallback, _callback);
	}

	public void UnregisterTrophyUnlock(VoidGeneric<int> _callback)
	{
		m_trophyUnlockCallback = (VoidGeneric<int>)Delegate.Remove(m_trophyUnlockCallback, _callback);
	}

	public void RegisterTrophyProgress(VoidGeneric<int, float> _callback)
	{
		m_trophyProgressCallback = (VoidGeneric<int, float>)Delegate.Combine(m_trophyProgressCallback, _callback);
	}

	public void UnregisterTrophyProgress(VoidGeneric<int, float> _callback)
	{
		m_trophyProgressCallback = (VoidGeneric<int, float>)Delegate.Remove(m_trophyProgressCallback, _callback);
	}

	public void Clear()
	{
		m_Stats = null;
		m_Trophies = null;
	}

	public void Setup(StatsTracking statsTracking)
	{
		if (m_Stats == null)
		{
			m_Stats = new FastList<Stat>(statsTracking.m_Stats.Length);
		}
		if (m_Trophies == null)
		{
			m_Trophies = new FastList<Trophy>(statsTracking.m_Tropies.Length);
		}
		for (int i = 0; i < statsTracking.m_Stats.Length; i++)
		{
			CreateStat(statsTracking.m_Stats[i].m_ID.ToString(), (int)statsTracking.m_Stats[i].m_ID, (int)statsTracking.m_Stats[i].m_StatType, statsTracking.m_Stats[i].m_validationList);
		}
		for (int j = 0; j < statsTracking.m_Tropies.Length; j++)
		{
			CreateTrophy(statsTracking.m_Tropies[j].m_Name, statsTracking.m_Tropies[j].m_APIName, statsTracking.m_Tropies[j].m_TrophyID, statsTracking.m_Tropies[j].m_Rules.Length, (int)statsTracking.m_Tropies[j].m_CombineMode);
			for (int k = 0; k < statsTracking.m_Tropies[j].m_Rules.Length; k++)
			{
				SetUpTrophy(statsTracking.m_Tropies[j].m_TrophyID, (int)statsTracking.m_Tropies[j].m_Rules[k].m_StatID, statsTracking.m_Tropies[j].m_Rules[k].m_RefValue, (int)statsTracking.m_Tropies[j].m_Rules[k].m_Compare);
			}
		}
	}

	public void CreateStat(string statName, int ID, int statType, StatValidationList validationList = null)
	{
		Stat stat = new Stat();
		stat.m_Name = statName;
		stat.m_ID = ID;
		stat.m_Type = (STAT_TYPE)statType;
		stat.m_validationList = validationList;
		Stat stat2 = stat;
		if (stat2.m_Type == STAT_TYPE.ST_ID_HOLDER)
		{
			stat2.m_IDHolder = new Hashtable();
		}
		m_Stats.Add(stat2);
	}

	public void CreateTrophy(string trophyName, string apiName, int trophyID, int numberRules, int combiner)
	{
		Trophy trophy = new Trophy();
		trophy.m_Name = trophyName;
		trophy.m_APIName = apiName;
		trophy.m_TrophyID = trophyID;
		trophy.m_Rules = new StatRule[numberRules];
		trophy.m_RuleCount = 0;
		trophy.m_Combiner = (Combiner)combiner;
		Trophy item = trophy;
		m_Trophies.Add(item);
	}

	public void SetUpTrophy(int trophyID, int statID, float refValue, int compareFunc)
	{
		Trophy trophy;
		Stat stat;
		if (FindTrophy(trophyID, out trophy) && FindStat(statID, out stat))
		{
			trophy.m_Rules[trophy.m_RuleCount] = new StatRule();
			trophy.m_Rules[trophy.m_RuleCount].m_Stat = stat;
			trophy.m_Rules[trophy.m_RuleCount].m_RefValue = refValue;
			trophy.m_Rules[trophy.m_RuleCount].m_Compare = (StatCompare)compareFunc;
			trophy.m_RuleCount++;
		}
	}

	public void SetupDone()
	{
		int num = 0;
		for (int i = 0; i < m_Trophies.Count; i++)
		{
			num += m_Trophies._items[i].m_Rules.Length;
		}
		m_StatsRules = new StatRule[num];
		for (int j = 0; j < m_Stats.Count; j++)
		{
			int num2 = 0;
			for (int i = 0; i < m_Trophies.Count; i++)
			{
				for (int k = 0; k < m_Trophies._items[i].m_Rules.Length; k++)
				{
					if (m_Trophies._items[i].m_Rules[k].m_Stat == m_Stats._items[j])
					{
						num2++;
					}
				}
			}
			m_Stats._items[j].m_RefStats = new StatRule[num2];
		}
		num = 0;
		for (int i = 0; i < m_Trophies.Count; i++)
		{
			for (int k = 0; k < m_Trophies._items[i].m_Rules.Length; k++)
			{
				m_StatsRules[num] = m_Trophies._items[i].m_Rules[k];
				m_StatsRules[num].m_RefTrophy = m_Trophies._items[i];
				num++;
			}
		}
		for (int j = 0; j < m_Stats.Count; j++)
		{
			int num2 = 0;
			for (int i = 0; i < m_StatsRules.Length; i++)
			{
				if (m_StatsRules[i].m_Stat == m_Stats._items[j])
				{
					m_Stats._items[j].m_RefStats[num2] = m_StatsRules[i];
					num2++;
				}
			}
		}
	}

	public void LoadStats()
	{
		MetaGameProgress metaGameProgress = m_saveManager.GetMetaGameProgress();
		for (int i = 0; i < m_Stats.Count; i++)
		{
			Stat stat = m_Stats._items[i];
			if (stat.m_Type == STAT_TYPE.ST_COUNTER)
			{
				float value = 0f;
				if (metaGameProgress.SaveData.Get("S_" + stat.m_Name + "_V_" + stat.m_ID, out value, 0f))
				{
					stat.m_Value = value;
				}
			}
			else
			{
				int value2;
				if (!metaGameProgress.SaveData.Get("S_" + stat.m_Name + "_HS_" + stat.m_ID, out value2, 0))
				{
					continue;
				}
				stat.m_IDHolder = new Hashtable();
				for (int j = 0; j < value2; j++)
				{
					int value3;
					int value4;
					if (metaGameProgress.SaveData.Get("S_" + stat.m_Name + "_KEY_" + stat.m_ID + "_" + j.ToString(), out value3, 0) && metaGameProgress.SaveData.Get("S_" + stat.m_Name + "_V_" + stat.m_ID + "_" + j.ToString(), out value4, 0))
					{
						stat.m_IDHolder[value3] = value4;
					}
				}
			}
		}
	}

	public void ResetStat(int ID, ControlPadInput.PadNum pad)
	{
		if (pad != ControlPadInput.PadNum.One)
		{
			Stat stat = null;
			if (FindStat(ID, out stat))
			{
				m_statValueChangedCallback(ID, stat.m_Value, 0f);
				stat.m_Value = 0f;
				MetaGameProgress metaGameProgress = m_saveManager.GetMetaGameProgress();
				metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_V_" + stat.m_ID, stat.m_Value);
			}
		}
	}

	public void SetStat(int ID, float value, ControlPadInput.PadNum pad)
	{
		if (pad == ControlPadInput.PadNum.One)
		{
			Stat stat = null;
			if (FindStat(ID, out stat))
			{
				m_statValueChangedCallback(ID, stat.m_Value, value);
				stat.m_Value = value;
				CheckStatRulesLinkedToStat(stat, stat.m_Value);
				MetaGameProgress metaGameProgress = m_saveManager.GetMetaGameProgress();
				metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_V_" + stat.m_ID, stat.m_Value);
			}
		}
	}

	public void IncStat(int ID, float inc, ControlPadInput.PadNum pad)
	{
		if (pad == ControlPadInput.PadNum.One)
		{
			Stat stat = null;
			if (FindStat(ID, out stat))
			{
				m_statValueChangedCallback(ID, stat.m_Value, stat.m_Value + inc);
				stat.m_Value += inc;
				CheckStatRulesLinkedToStat(stat, stat.m_Value);
				MetaGameProgress metaGameProgress = m_saveManager.GetMetaGameProgress();
				metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_V_" + stat.m_ID, stat.m_Value);
			}
		}
	}

	public int AddIDStat(int ID, int itemID, ControlPadInput.PadNum pad)
	{
		if (pad == ControlPadInput.PadNum.One)
		{
			Stat stat = null;
			if (FindStat(ID, out stat) && stat.m_Type == STAT_TYPE.ST_ID_HOLDER)
			{
				if (stat.m_validationList != null && Array.IndexOf(stat.m_validationList.m_ids, itemID) == -1)
				{
					return -1;
				}
				if (!stat.m_IDHolder.ContainsKey(itemID))
				{
					stat.m_IDHolder[itemID] = 1;
				}
				else
				{
					int num = (int)stat.m_IDHolder[itemID];
					num++;
					stat.m_IDHolder[itemID] = num;
				}
				CheckStatRulesLinkedToStat(stat, itemID);
				MetaGameProgress metaGameProgress = m_saveManager.GetMetaGameProgress();
				metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_HS_" + stat.m_ID, stat.m_IDHolder.Keys.Count);
				int num2 = 0;
				foreach (DictionaryEntry item in stat.m_IDHolder)
				{
					metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_KEY_" + stat.m_ID + "_" + num2.ToString(), (int)item.Key);
					metaGameProgress.SaveData.Set("S_" + stat.m_Name + "_V_" + stat.m_ID + "_" + num2.ToString(), (int)item.Value);
					num2++;
				}
				return stat.m_IDHolder.Keys.Count;
			}
		}
		return -1;
	}

	private bool FindStat(int ID, out Stat stat)
	{
		for (int i = 0; i < m_Stats.Count; i++)
		{
			if (m_Stats._items[i].m_ID == ID)
			{
				stat = m_Stats._items[i];
				return true;
			}
		}
		stat = null;
		return false;
	}

	private bool FindTrophy(int ID, out Trophy trophy)
	{
		for (int i = 0; i < m_Trophies.Count; i++)
		{
			if (m_Trophies._items[i].m_TrophyID == ID)
			{
				trophy = m_Trophies._items[i];
				return true;
			}
		}
		trophy = null;
		return false;
	}

	public string GetStatName(int ID)
	{
		Stat stat = null;
		if (FindStat(ID, out stat))
		{
			return stat.m_Name;
		}
		return null;
	}

	public string GetTrophyName(int ID)
	{
		Trophy trophy = null;
		if (FindTrophy(ID, out trophy))
		{
			return trophy.m_Name;
		}
		return null;
	}

	public string GetTrophyApiName(int ID)
	{
		Trophy trophy = null;
		if (FindTrophy(ID, out trophy))
		{
			return trophy.m_APIName;
		}
		return null;
	}

	public float GetTrophyProgress(int ID)
	{
		Trophy trophy = null;
		if (FindTrophy(ID, out trophy))
		{
			return trophy.GetProgress();
		}
		return 0f;
	}

	private void CheckStatRulesLinkedToStat(Stat stat, float newValue)
	{
		bool flag = false;
		bool flag2 = false;
		for (uint num = 0u; num < stat.m_RefStats.Length; num++)
		{
			Trophy refTrophy = stat.m_RefStats[num].m_RefTrophy;
			if (refTrophy != null && !refTrophy.m_Unlocked)
			{
				m_trophyProgressCallback(refTrophy.m_TrophyID, refTrophy.GetProgress());
			}
			flag2 = true;
			flag |= stat.m_RefStats[num].Check(ref flag2, newValue);
			if (flag && flag2 && refTrophy != null && !refTrophy.m_Unlocked && stat.m_RefStats[num].m_RefTrophy.Check())
			{
				m_trophyUnlockCallback(refTrophy.m_TrophyID);
				refTrophy.m_Unlocked = true;
			}
		}
	}
}
