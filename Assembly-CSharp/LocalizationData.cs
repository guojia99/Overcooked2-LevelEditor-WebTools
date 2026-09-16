using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "LocalizationData", menuName = "Team17/Create Localization Data")]
public class LocalizationData : ScriptableObject
{
	public TextAsset[] m_Localizations;
}
