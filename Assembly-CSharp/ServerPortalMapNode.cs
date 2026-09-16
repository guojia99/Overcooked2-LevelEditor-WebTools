using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerPortalMapNode : ServerSynchroniserBase, IServerMapSelectable
{
	protected PortalMapNode m_basePortalMapNode;

	protected WorldMapFlowController m_worldMapFlowController;

	protected SceneDirectoryData.SceneDirectoryEntry m_sceneDirectoryEntry;

	private bool m_inSelectable;

	public SceneDirectoryData.SceneDirectoryEntry SceneDirectoryEntry
	{
		get
		{
			return m_sceneDirectoryEntry;
		}
	}

	protected bool InSelectable
	{
		get
		{
			return m_inSelectable;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_basePortalMapNode = (PortalMapNode)synchronisedObject;
		m_worldMapFlowController = GameUtils.RequireManager<WorldMapFlowController>();
		m_sceneDirectoryEntry = m_worldMapFlowController.GetSceneDirectory().Scenes.TryAtIndex(m_basePortalMapNode.LevelIndex);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PortalMapNode;
	}

	public void AvatarEnteringSelectable(MapAvatarControls _avatar)
	{
		m_inSelectable = true;
	}

	public void AvatarLeavingSelectable(MapAvatarControls _avatar)
	{
		m_inSelectable = false;
	}

	public void OnSelected(MapAvatarControls _avatar)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (MaskUtils.HasFlag(m_sceneDirectoryEntry.m_supportedGameModes, gameSession.GameModeKind) && m_basePortalMapNode.Unfolded)
		{
			OnAllowedSelection(_avatar);
		}
	}

	protected abstract void OnAllowedSelection(MapAvatarControls _avatar);
}
