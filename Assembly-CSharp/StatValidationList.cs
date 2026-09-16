using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "StatValidationList", menuName = "Team17/Create StatValidationList")]
public class StatValidationList : ScriptableObject
{
	[SerializeField]
	public int[] m_ids;
}
