using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class TutorialIntroFlowroutine : CutsceneIntroFlowRoutine
{
	[SerializeField]
	private TutorialIconController m_iconController;

	protected override void Awake()
	{
		if (m_iconController != null)
		{
			m_iconController.enabled = false;
		}
	}

	public override IEnumerator Run()
	{
		IEnumerator routine = _003CRun_003E__BaseCallProxy0();
		while (routine.MoveNext())
		{
			yield return routine.Current;
		}
		if (m_iconController != null)
		{
			m_iconController.enabled = true;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
