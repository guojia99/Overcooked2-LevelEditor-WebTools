using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "StatsTracking", menuName = "Team17/Create StatsTracking")]
public class StatsTracking : ScriptableObject
{
	[Serializable]
	public enum STAT_TYPE
	{
		Counter = 0,
		ID_Holder = 1
	}

	[Serializable]
	public class Stat
	{
		public STAT_IDS m_ID;

		public STAT_TYPE m_StatType;

		public StatValidationList m_validationList;
	}

	public enum StatCompare
	{
		EQUAL = 0,
		GREATER_THAN_EQUAL = 1,
		HAS_ID = 2
	}

	[Serializable]
	public class StatRule
	{
		public STAT_IDS m_StatID;

		public float m_RefValue;

		public StatCompare m_Compare;
	}

	[Serializable]
	public enum Combiner
	{
		NA = 0,
		AND = 1,
		OR = 2
	}

	[Serializable]
	public class Trophy
	{
		public string m_Name;

		public string m_APIName;

		public int m_TrophyID;

		public StatRule[] m_Rules;

		public Combiner m_CombineMode;
	}

	private static StatsTracking m_Instance;

	public Stat[] m_Stats;

	public Trophy[] m_Tropies;

	private StatsTracking()
	{
		m_Instance = this;
	}
}
