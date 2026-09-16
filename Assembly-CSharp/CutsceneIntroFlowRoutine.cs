using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class CutsceneIntroFlowRoutine : LevelIntroFlowroutine
{
	[SerializeField]
	private CutsceneController m_cutscene;

	private ClientCutsceneController m_cutsceneController;

	private IEnumerator m_cutsceneRoutine;

	protected override void Awake()
	{
		if (m_cutscene != null)
		{
			m_cutscene.gameObject.SetActive(false);
		}
	}

	public override void Setup(CallbackVoid _startRoundCallback)
	{
		base.Setup(_startRoundCallback);
		m_cutsceneController = m_cutscene.gameObject.RequireComponent<ClientCutsceneController>();
		CutsceneController.SetupData setupData = new CutsceneController.SetupData();
		setupData.skippable = true;
		setupData.postplaybackUIEnabled = true;
		m_cutsceneRoutine = m_cutsceneController.StartCutscene(setupData);
	}

	public override IEnumerator Run()
	{
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
		IEnumerator routine = _003CRun_003E__BaseCallProxy0();
		while (routine.MoveNext())
		{
			yield return routine.Current;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
