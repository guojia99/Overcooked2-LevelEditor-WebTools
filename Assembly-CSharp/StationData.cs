using System;
using UnityEngine;

[Serializable]
public class StationData : ScriptableObject
{
	public GameObject m_prefab;

	public string m_name;

	public int m_cost = 25;
}
