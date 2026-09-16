using GameModes;
using UnityEngine;

[ExecutionDependency(typeof(BootstrapManager))]
public class HordeLevelPortalMapNode : LevelPortalMapNode
{
	[AssignResource("horde_world_map_level_preview", Editorbility.Editable)]
	[SerializeField]
	private WorldMapLevelIconUI m_uiPrefab;

	public override WorldMapLevelIconUI GetUIPrefab(Kind _kind)
	{
		return m_uiPrefab;
	}
}
