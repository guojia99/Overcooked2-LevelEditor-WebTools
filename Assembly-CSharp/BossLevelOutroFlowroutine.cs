using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossLevelOutroFlowroutine : CutsceneOutroFlowroutineBase, CampaignFlowController.IOutroFlowSceneProvider
{
	[SerializeField]
	private CutsceneController m_successCutscene;

	[SerializeField]
	private CutsceneController m_failureCutscene;

	[SerializeField]
	[SceneName]
	private string m_creditsScene = string.Empty;

	private bool m_succeeded;

	public void SetOutroDirectors(CutsceneController _successDirector, CutsceneController _failureDirector)
	{
		m_successCutscene = _successDirector ?? m_successCutscene;
		m_failureCutscene = _failureDirector ?? m_failureCutscene;
	}

	protected override void Setup(CampaignFlowController.OutroData _setupData)
	{
		m_succeeded = _setupData.StarsAwarded > 0;
		if (m_successCutscene != null)
		{
			m_successCutscene.gameObject.SetActive(false);
		}
		if (m_failureCutscene != null)
		{
			m_failureCutscene.gameObject.SetActive(false);
		}
		base.Setup(_setupData);
	}

	protected override IEnumerator Run()
	{
		IEnumerator cutsceneRoutine = _003CRun_003E__BaseCallProxy0();
		while (cutsceneRoutine.MoveNext())
		{
			yield return null;
		}
	}

	protected override CutsceneController GetCutsceneController()
	{
		return (!m_succeeded) ? m_failureCutscene : m_successCutscene;
	}

	public string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
	{
		if (m_succeeded)
		{
			o_loadState = GameState.LoadKitchen;
			o_loadEndState = GameState.NotSet;
			o_useLoadingScreen = false;
			return m_creditsScene;
		}
		o_loadState = GameState.LoadKitchen;
		o_loadEndState = GameState.RunKitchen;
		o_useLoadingScreen = true;
		return SceneManager.GetActiveScene().name;
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
