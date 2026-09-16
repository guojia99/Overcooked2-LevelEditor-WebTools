using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class FrontendCredits : FrontendMenuBehaviour
{
	[Header("Credits")]
	[SerializeField]
	[AssignChild("Credits", Editorbility.NonEditable)]
	private GameObject m_rootObject;

	[SerializeField]
	private string m_startTrigger = "Start";

	[SerializeField]
	private string m_resetTrigger = "Reset";

	[SerializeField]
	private string m_finishedTrigger = "Finished";

	private static int m_startId = -1;

	private static int m_resetId = -1;

	private static int m_finishedId = -1;

	private Animator m_animator;

	private IEnumerator m_creditsRoutine;

	private bool m_pendingClose;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_animator = m_rootObject.RequestComponentRecursive<Animator>();
		if (m_startId == -1)
		{
			m_startId = Animator.StringToHash(m_startTrigger);
		}
		if (m_resetId == -1)
		{
			m_resetId = Animator.StringToHash(m_resetTrigger);
		}
		if (m_finishedId == -1)
		{
			m_finishedId = Animator.StringToHash(m_finishedTrigger);
		}
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_rootObject != null)
		{
			m_rootObject.SetActive(false);
		}
		m_creditsRoutine = RunCredits();
		m_pendingClose = false;
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		Shutdown();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
		return true;
	}

	protected override void Update()
	{
		base.Update();
		if (m_creditsRoutine != null && !m_creditsRoutine.MoveNext())
		{
			m_creditsRoutine = null;
		}
	}

	public override void Close()
	{
		m_pendingClose = true;
	}

	private IEnumerator RunCredits()
	{
		ScreenTransitionManager transitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
		bool transitionFinished = false;
		transitionManager.StartTransitionUp(delegate
		{
			transitionManager.StartTransitionDown(delegate
			{
				transitionFinished = true;
			});
			if (m_rootObject != null)
			{
				m_rootObject.SetActive(true);
			}
		});
		while (!transitionFinished)
		{
			yield return null;
		}
		if (!m_pendingClose)
		{
			if (m_animator != null)
			{
				m_animator.SetTrigger(m_startId);
				m_animator.Update(0f);
			}
			while (!m_pendingClose && (!(m_animator != null) || !m_animator.GetBool(m_finishedId)))
			{
				yield return null;
			}
		}
		m_pendingClose = false;
		transitionManager.StartTransitionUp(delegate
		{
			Shutdown();
			_003CClose_003E__BaseCallProxy0();
			transitionManager.StartTransitionDown();
		});
	}

	private void Shutdown()
	{
		if (m_animator != null && m_animator.IsActive())
		{
			m_animator.SetTrigger(m_resetId);
			m_animator.Update(0f);
		}
		if (m_rootObject != null)
		{
			m_rootObject.SetActive(false);
		}
		m_creditsRoutine = null;
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private void _003CClose_003E__BaseCallProxy0()
	{
		base.Close();
	}
}
