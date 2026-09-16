using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientDynamicFlowController : ClientCampaignFlowController
{
	private DynamicFlowController m_dynamicFlowController;

	private Queue<IEnumerator> m_phaseQueue = new Queue<IEnumerator>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_dynamicFlowController = (DynamicFlowController)synchronisedObject;
	}

	protected override void Awake()
	{
		base.Awake();
		Mailbox.Client.RegisterForMessageType(MessageType.DynamicLevel, OnDynamicLevelMessage);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.DynamicLevel, OnDynamicLevelMessage);
	}

	protected void OnDynamicLevelMessage(IOnlineMultiplayerSessionUserId _sessionId, Serialisable _serialisable)
	{
		DynamicLevelMessage dynamicLevelMessage = (DynamicLevelMessage)_serialisable;
		IEnumerator item = BuildTransitionToPhaseRoutine(dynamicLevelMessage.m_phase);
		m_phaseQueue.Enqueue(item);
	}

	protected override ClientOrderControllerBase BuildOrderController(RecipeFlowGUI _recipeUI)
	{
		ClientDynamicOrderController clientDynamicOrderController = new ClientDynamicOrderController(_recipeUI);
		clientDynamicOrderController.SetRoundTimer(base.RoundTimer);
		return clientDynamicOrderController;
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		if (m_phaseQueue.Count > 0)
		{
			IEnumerator enumerator = m_phaseQueue.Peek();
			if (enumerator == null || !enumerator.MoveNext())
			{
				m_phaseQueue.Dequeue();
			}
		}
	}

	protected virtual IEnumerator BuildTransitionToPhaseRoutine(int _newPhase)
	{
		int index = _newPhase - 1;
		GameObject obj = m_dynamicFlowController.m_transitions[index];
		obj.SetActive(true);
		DynamicTransitionBase transition = obj.RequireComponent<DynamicTransitionBase>();
		transition.Setup(delegate
		{
		});
		IEnumerator routine = transition.Run();
		while (routine.MoveNext())
		{
			yield return null;
		}
	}
}
