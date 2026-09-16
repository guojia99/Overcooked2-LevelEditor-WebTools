using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerLevelPortalMapNode : ServerPortalMapNode
{
	protected LevelPortalMapNode m_baseLevelPortalMapNode;

	protected LevelPortalMapNodeMessage m_Message = new LevelPortalMapNodeMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_baseLevelPortalMapNode = (LevelPortalMapNode)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.LevelPortalMapNode;
	}

	protected override void OnAllowedSelection(MapAvatarControls _avatar)
	{
		switch (m_baseLevelPortalMapNode.GetState())
		{
		case WorldMapKitchenLevelIconUI.State.Affordable:
		{
			GameProgress.GameProgressData.LevelProgress levelProgress = m_baseLevelPortalMapNode.GetLevelProgress();
			levelProgress.Purchased = true;
			SendServerEvent(m_Message);
			break;
		}
		case WorldMapKitchenLevelIconUI.State.Purchased:
		{
			ServerWorldMapFlowController component = m_worldMapFlowController.GetComponent<ServerWorldMapFlowController>();
			component.OnSelectLevelPortal(_avatar, m_baseLevelPortalMapNode);
			break;
		}
		}
	}
}
