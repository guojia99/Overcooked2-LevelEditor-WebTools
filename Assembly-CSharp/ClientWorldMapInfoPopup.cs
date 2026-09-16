using System.Collections;
using GameModes.Horde;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWorldMapInfoPopup : ClientSynchroniserBase
{
	public struct InfoPopupShowRequest
	{
		public Transform m_requester;

		public ClientWorldMapInfoPopup m_popup;

		public InfoPopupShowRequest(Transform _requester, ClientWorldMapInfoPopup _popup)
		{
			m_requester = _requester;
			m_popup = _popup;
		}
	}

	private WorldMapInfoPopup m_popupInfo;

	private bool m_hasShown;

	private bool m_anyRevealedSwitches;

	public override EntityType GetEntityType()
	{
		return EntityType.WorldPopup;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_popupInfo = synchronisedObject as WorldMapInfoPopup;
		InitialiseForType();
		bool flag = !ConnectionStatus.IsInSession() || ConnectionStatus.IsHost();
		m_popupInfo.m_buttonImage.enabled = flag;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		m_hasShown = true;
	}

	protected void InitialiseForType()
	{
		if (m_popupInfo.m_type == WorldMapInfoPopup.Type.Switch)
		{
			GameProgress.GameProgressData saveData = GameUtils.GetGameSession().Progress.SaveData;
			m_anyRevealedSwitches = saveData.Switches.Length > 0;
		}
	}

	public bool CanShow()
	{
		GameProgress.GameProgressData saveData = GameUtils.GetGameSession().Progress.SaveData;
		switch (m_popupInfo.m_type)
		{
		case WorldMapInfoPopup.Type.Switch:
			if (m_anyRevealedSwitches)
			{
				return false;
			}
			break;
		case WorldMapInfoPopup.Type.HiddenLevel:
		{
			SceneDirectoryData sceneDirectory2 = GameUtils.GetGameSession().Progress.GetSceneDirectory();
			for (int j = 0; j < saveData.Levels.Length; j++)
			{
				GameProgress.GameProgressData.LevelProgress levelProgress2 = saveData.Levels[j];
				if (levelProgress2.Revealed && sceneDirectory2.Scenes[levelProgress2.LevelId].IsHidden)
				{
					return false;
				}
			}
			break;
		}
		case WorldMapInfoPopup.Type.NewGamePlus:
		{
			GameSession gameSession3 = GameUtils.GetGameSession();
			GameProgress progress = gameSession3.Progress;
			if (progress.HasShownNGPlusDialog(progress.SaveData) || (!progress.SaveData.IsNGPEnabledForAnyLevel() && !progress.CanUnlockNewGamePlus(progress.SaveData)))
			{
				return false;
			}
			break;
		}
		case WorldMapInfoPopup.Type.PracticeMode:
		{
			GameSession gameSession2 = GameUtils.GetGameSession();
			if (gameSession2.HasShownMetaDialog(MetaGameProgress.MetaDialogType.PracticeMode))
			{
				return false;
			}
			break;
		}
		case WorldMapInfoPopup.Type.HordeMode:
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (gameSession.HasShownMetaDialog(MetaGameProgress.MetaDialogType.HordeMode))
			{
				return false;
			}
			bool flag = false;
			SceneDirectoryData sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
			for (int i = 0; i < saveData.Levels.Length; i++)
			{
				GameProgress.GameProgressData.LevelProgress levelProgress = saveData.Levels[i];
				if (levelProgress.Revealed)
				{
					SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes.TryAtIndex(levelProgress.LevelId);
					if (sceneDirectoryEntry != null && sceneDirectoryEntry.SceneVarients[0].LevelConfig.GetType() == typeof(HordeLevelConfig))
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				return false;
			}
			break;
		}
		}
		return !m_hasShown;
	}

	public IEnumerator PopupRoutine()
	{
		base.gameObject.SetActive(true);
		while (!m_hasShown)
		{
			yield return null;
		}
		switch (m_popupInfo.m_type)
		{
		case WorldMapInfoPopup.Type.NewGamePlus:
		{
			GameSession gameSession2 = GameUtils.GetGameSession();
			gameSession2.Progress.SetNGPlusDialogShown(gameSession2.Progress.SaveData);
			break;
		}
		case WorldMapInfoPopup.Type.PracticeMode:
		{
			GameSession gameSession3 = GameUtils.GetGameSession();
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				MetaGameProgress metaGameProgress2 = GameUtils.GetMetaGameProgress();
				metaGameProgress2.SetMetaDialogShown(MetaGameProgress.MetaDialogType.PracticeMode);
			}
			gameSession3.SetMetaDialogShown(MetaGameProgress.MetaDialogType.PracticeMode);
			break;
		}
		case WorldMapInfoPopup.Type.HordeMode:
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
				metaGameProgress.SetMetaDialogShown(MetaGameProgress.MetaDialogType.HordeMode);
			}
			gameSession.SetMetaDialogShown(MetaGameProgress.MetaDialogType.HordeMode);
			break;
		}
		}
		base.gameObject.SetActive(false);
	}
}
