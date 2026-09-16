using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientLevelPortalMapNode : ClientPortalMapNode
{
	protected LevelPortalMapNode m_baseLevelPortalMapNode;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_baseLevelPortalMapNode = (LevelPortalMapNode)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.LevelPortalMapNode;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		m_UIState = UIState.RequestedUpdate;
	}

	protected override void SetupUI(WorldMapLevelIconUI _ui)
	{
		WorldMapKitchenLevelIconUI worldMapKitchenLevelIconUI = _ui as WorldMapKitchenLevelIconUI;
		if (m_baseLevelPortalMapNode.m_sceneDirectoryEntry != null)
		{
			worldMapKitchenLevelIconUI.Setup(m_baseLevelPortalMapNode.m_sceneDirectoryEntry, m_baseLevelPortalMapNode.m_sceneProgress, m_baseLevelPortalMapNode.GetState());
		}
	}
}
