using System;
using Team17.Online;
using UnityEngine;

[ExecutionDependency(typeof(WorldMapCamera))]
public class WorldMapFlowController : Manager
{
	[Serializable]
	public class RevealSequenceData
	{
		public float TimePerNode = 1f;

		public float IdealTransitTime = 1f;

		public float MinMoveSpeed = 10f;

		public float MaxMoveSpeed = 50f;

		public float AccelerationTime = 1f;
	}

	[SerializeField]
	private SceneDirectoryData m_sceneDirectory;

	[SerializeField]
	public AmbiControlsMappingData m_sidedAmbiMapping;

	[SerializeField]
	public AmbiControlsMappingData m_unsidedAmbiMapping;

	[SerializeField]
	public RevealSequenceData m_unfoldSequenceData = new RevealSequenceData();

	[SerializeField]
	[AssignResource("WorldMapInfoPopup_NewGamePlus", Editorbility.Editable)]
	public WorldMapInfoPopup m_newGamePlusDialogPrefab;

	[SerializeField]
	[AssignResource("WorldMapInfoPopup_PractiseMode", Editorbility.Editable)]
	public WorldMapInfoPopup m_practiceModeDialogPrefab;

	[SerializeField]
	[AssignResource("WorldMapInfoPopup_HordeMode", Editorbility.Editable)]
	public WorldMapInfoPopup m_hordeModeDialogPrefab;

	public SceneDirectoryData GetSceneDirectory()
	{
		return m_sceneDirectory;
	}

	public bool IsLevelUnlocked(PortalMapNode _node)
	{
		GameProgress progress = GameUtils.GetGameSession().Progress;
		if (_node.LevelIndex == -1)
		{
			return false;
		}
		if (_node.ForceUnlocked || DebugManager.Instance.GetOption("Unlock all levels"))
		{
			return true;
		}
		return progress.SaveData.IsLevelUnlocked(_node.LevelIndex);
	}

	public bool IsSwitchSwitched(SwitchMapNode _node)
	{
		GameProgress progress = GameUtils.GetGameSession().Progress;
		if (_node.SwitchID <= 0)
		{
			return false;
		}
		if (DebugManager.Instance.GetOption("Raise all ramps"))
		{
			return true;
		}
		GameProgress.GameProgressData.SwitchState switchState = progress.GetSwitchState(_node.SwitchID);
		if (switchState.Activated)
		{
			return true;
		}
		if (_node.IsSwitchedDueToCompletion())
		{
			return true;
		}
		return false;
	}

	private void Awake()
	{
		UserSystemUtils.BuildGameInputConfig();
	}
}
