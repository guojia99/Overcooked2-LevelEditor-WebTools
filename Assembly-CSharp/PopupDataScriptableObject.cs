using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PopupDataScriptableObject : ScriptableObject
{
	public List<PopupData> m_Popups = new List<PopupData>();
}
