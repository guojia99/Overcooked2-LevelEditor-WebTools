using Team17.Online.Multiplayer.Messaging;

public class ClientMultiLevelMiniPortalMapNode : ClientMiniLevelPortalMapNode
{
	public override EntityType GetEntityType()
	{
		return EntityType.MultiLevelMiniPortalMapNode;
	}

	protected override void SetupUI(WorldMapLevelIconUI _ui)
	{
		base.SetupUI(_ui);
		MultiLevelMiniPortalMapNode multiLevelMiniPortalMapNode = m_baseLevelPortalMapNode as MultiLevelMiniPortalMapNode;
		if (!(multiLevelMiniPortalMapNode != null))
		{
			return;
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (!(gameSession != null))
		{
			return;
		}
		GameProgress progress = gameSession.Progress;
		if (progress != null)
		{
			GameProgress.GameProgressData saveData = progress.SaveData;
			if (saveData != null && !saveData.IsLevelComplete(multiLevelMiniPortalMapNode.LevelIndex))
			{
				_ui.ActivateAttentionPopup();
			}
		}
	}
}
