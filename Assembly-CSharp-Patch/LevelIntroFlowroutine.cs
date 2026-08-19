using System.Collections;
using UnityEngine;

public class LevelIntroFlowroutine : IntroFlowroutineBase
{
	private const float c_tutorialAutoDismissTime = 15f;

	private const float c_dismissTutorialHoldDuration = 1f;

	[SerializeField]
	private LevelIntroFlowroutineData m_data;

	private GameObject m_readyUIInstance;

	private GameObject m_goUIInstance;

	private CallbackVoid m_startRoundCallback = delegate
	{
	};

	protected virtual void Awake()
	{
	}

	public override void Setup(CallbackVoid _startRoundCallback)
	{
		m_startRoundCallback = _startRoundCallback;
		if (m_data.ReadyUIPrefab != null)
		{
			m_readyUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.ReadyUIPrefab);
			m_readyUIInstance.SetActive(false);
		}
		if (m_data.GoUIPrefab != null)
		{
			m_goUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.GoUIPrefab);
			m_goUIInstance.SetActive(false);
		}
	}

	public override IEnumerator Run()
	{
		TimeManager timeManager = GameUtils.RequireManager<TimeManager>();
		timeManager.SetPaused(TimeManager.PauseLayer.Main, true, this);
		if (m_data.TutorialPopup.CanSpawn() && ClientGameSetup.Mode == GameMode.Campaign)
		{
			ClientTutorialPopupController controller = base.gameObject.RequireComponent<ClientTutorialPopupController>();
			IEnumerator tutorialRoutine = controller.ShowTutorial(m_data.TutorialPopup, TutorialDismissRoutine);
			while (tutorialRoutine.MoveNext())
			{
				yield return null;
			}
			controller.Shutdown();
		}
		int layerID = LayerMask.NameToLayer("UI");
		// patch
		//IEnumerator timerRoutine = CoroutineUtils.TimerRoutine(m_data.ReadyDelay, layerID);
		IEnumerator timerRoutine = CoroutineUtils.TimerRoutine(0.1f, layerID);
        // patch
        while (timerRoutine.MoveNext())
		{
			yield return null;
		}
		if (m_readyUIInstance != null)
		{
			// patch
			m_readyUIInstance.SetActive(true);
			//GameUtils.TriggerAudio(GameOneShotAudioTag.ReadyIntro, m_goUIInstance.layer);
			//timerRoutine = CoroutineUtils.TimerRoutine(m_data.ReadyUILifetime, layerID);
			timerRoutine = CoroutineUtils.TimerRoutine(0.1f, layerID);
			// patch
            while (timerRoutine.MoveNext())
			{
				yield return null;
			}
			Object.Destroy(m_readyUIInstance);
		}
		if (!ConnectionStatus.IsInSession())
		{
			PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
			while (playerManager.IsWarningActive(PlayerWarning.Disengaged))
			{
				yield return null;
			}
		}
		timeManager.SetPaused(TimeManager.PauseLayer.Main, false, this);
		m_startRoundCallback();
		if ((bool)m_goUIInstance)
		{
            // patch
            m_goUIInstance.SetActive(true);
			//GameUtils.TriggerAudio(GameOneShotAudioTag.LevelGo, m_goUIInstance.layer);
			//timerRoutine = CoroutineUtils.TimerRoutine(m_data.GOUILifetime, layerID);
			timerRoutine = CoroutineUtils.TimerRoutine(0.1f, layerID);
            // patch
            while (timerRoutine.MoveNext())
			{
				yield return null;
			}
			Object.Destroy(m_goUIInstance);
		}
	}

	private IEnumerator TutorialDismissRoutine(GameObject _ui)
	{
		ILogicalButton rawSkipButton = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart, PadSide.Both);
		TimedLogicalButton skipButton = new TimedLogicalButton(rawSkipButton, TimedLogicalButton.Condition.HeldLonger, 1f);
		if (_ui != null)
		{
			TimedInputUIController timedInputUIController = _ui.RequestComponentRecursive<TimedInputUIController>();
			timedInputUIController.SetDisplayInput(skipButton, 1f);
		}
		IEnumerator autoDismissTimer = CoroutineUtils.TimerRoutine(15f, LayerMask.NameToLayer("UI"));
		while (autoDismissTimer.MoveNext() && !skipButton.JustPressed())
		{
			yield return null;
		}
	}
}
