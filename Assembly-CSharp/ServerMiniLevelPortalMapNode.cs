using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMiniLevelPortalMapNode : ServerPortalMapNode
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

	protected override void OnAllowedSelection(MapAvatarControls _avatar)
	{
		SceneDirectoryData sceneDirectory = m_worldMapFlowController.GetSceneDirectory();
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes.TryAtIndex(m_baseLevelPortalMapNode.LevelIndex);
		SceneDirectoryData.PerPlayerCountDirectoryEntry perPlayerCountDirectoryEntry = sceneDirectoryEntry.SceneVarients[0];
		ServerWorldMapFlowController component = m_worldMapFlowController.GetComponent<ServerWorldMapFlowController>();
		component.OnSelectMiniLevelPortal(_avatar, m_baseLevelPortalMapNode, 0);
	}
}
