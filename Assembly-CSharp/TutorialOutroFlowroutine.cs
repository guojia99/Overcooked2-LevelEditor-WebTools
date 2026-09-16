using UnityEngine;

public class TutorialOutroFlowroutine : CutsceneOutroFlowroutineBase, CampaignFlowController.IOutroFlowSceneProvider
{
	[SerializeField]
	private CutsceneController m_cutscene;

	[SerializeField]
	[SceneName]
	private string m_nextScene = string.Empty;

	protected void Awake()
	{
		if (m_cutscene != null)
		{
			m_cutscene.gameObject.SetActive(false);
		}
	}

	protected override CutsceneController GetCutsceneController()
	{
		return m_cutscene;
	}

	public string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
	{
		o_loadState = GameState.LoadKitchen;
		o_loadEndState = GameState.RunKitchen;
		o_useLoadingScreen = true;
		return m_nextScene;
	}
}
