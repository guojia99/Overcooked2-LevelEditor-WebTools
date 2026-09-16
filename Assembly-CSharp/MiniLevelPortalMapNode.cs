using GameModes;
using UnityEngine;

public class MiniLevelPortalMapNode : PortalMapNode
{
	[SerializeField]
	[AssignResource("WorldMapMiniLevelIconUI", Editorbility.Editable)]
	public WorldMapLevelIconUI m_uiPrefab;

	public override WorldMapLevelIconUI GetUIPrefab(Kind _kind)
	{
		return m_uiPrefab;
	}
}
