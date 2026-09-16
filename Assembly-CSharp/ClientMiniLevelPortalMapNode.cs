using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMiniLevelPortalMapNode : ClientPortalMapNode
{
	protected MiniLevelPortalMapNode m_baseLevelPortalMapNode;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_baseLevelPortalMapNode = (MiniLevelPortalMapNode)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.MiniLevelPortalMapNode;
	}

	protected override void SetupUI(WorldMapLevelIconUI _ui)
	{
		SceneDirectoryData sceneDirectory = m_baseLevelPortalMapNode.m_worldMapFlowController.GetSceneDirectory();
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes.TryAtIndex(m_baseLevelPortalMapNode.LevelIndex);
		_ui.SetTitle(sceneDirectoryEntry.Label);
	}
}
