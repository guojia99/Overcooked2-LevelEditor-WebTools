using System.Collections;
using UnityEngine;

public abstract class CutsceneOutroFlowroutineBase : CampaignFlowController.OutroFlowroutine
{
	[SerializeField]
	private LevelOutroFlowroutineData m_data;

	private ClientCutsceneController m_cutsceneController;

	private IEnumerator m_cutsceneRoutine;

	private GameObject m_timesUpUIInstance;

	protected override void Setup(CampaignFlowController.OutroData _setupData)
	{
		if (m_data.TimesUpUIPrefab != null)
		{
			m_timesUpUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.TimesUpUIPrefab);
			m_timesUpUIInstance.SetActive(false);
		}
		CutsceneController cutsceneController = GetCutsceneController();
		m_cutsceneController = cutsceneController.gameObject.RequireComponent<ClientCutsceneController>();
		CutsceneController.SetupData setupData = new CutsceneController.SetupData();
		setupData.skippable = true;
		setupData.postplaybackUIEnabled = false;
		m_cutsceneRoutine = m_cutsceneController.StartCutscene(setupData);
	}

	protected override IEnumerator Run()
	{
		int layerID = LayerMask.NameToLayer("UI");
		IEnumerator timerRoutine = null;
		if (m_timesUpUIInstance != null)
		{
			m_timesUpUIInstance.SetActive(true);
			GameUtils.TriggerAudio(GameOneShotAudioTag.TimesUp, m_timesUpUIInstance.layer);
			timerRoutine = CoroutineUtils.TimerRoutine(m_data.TimesUpUILifetime, layerID);
			while (timerRoutine.MoveNext())
			{
				yield return null;
			}
			Object.Destroy(m_timesUpUIInstance);
		}
		if (m_cutsceneController != null)
		{
			m_cutsceneController.gameObject.SetActive(true);
			while (m_cutsceneRoutine != null && m_cutsceneRoutine.MoveNext())
			{
				yield return m_cutsceneRoutine.Current;
			}
			m_cutsceneController.Shutdown();
			m_cutsceneController.gameObject.SetActive(false);
		}
	}

	protected override void Shutdown()
	{
	}

	protected abstract CutsceneController GetCutsceneController();
}
