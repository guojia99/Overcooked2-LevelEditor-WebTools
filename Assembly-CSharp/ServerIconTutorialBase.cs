using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerIconTutorialBase : ServerSynchroniserBase
{
	private IconTutorialBase m_iconTutorial;

	protected IFlowController m_iServerFlowController;

	private bool m_tutorialActive;

	private bool m_completed;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_iconTutorial = (IconTutorialBase)synchronisedObject;
		FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
		m_iServerFlowController = flowControllerBase.gameObject.RequestInterface<IServerFlowController>();
		m_iServerFlowController.RoundActivatedCallback += EnterRound;
		m_iServerFlowController.RoundDeactivatedCallback += ExitRound;
	}

	protected virtual void OnStartTutorial()
	{
	}

	protected virtual void OnStopTutorial()
	{
	}

	private void EnterRound()
	{
		if (!m_completed)
		{
			m_tutorialActive = true;
			OnStartTutorial();
		}
	}

	private void ExitRound()
	{
		if (m_tutorialActive)
		{
			m_tutorialActive = false;
			OnStopTutorial();
		}
	}

	protected void CompleteTutorial()
	{
		ExitRound();
		m_completed = true;
	}

	protected virtual void OnTutorialUpdate()
	{
	}
}
