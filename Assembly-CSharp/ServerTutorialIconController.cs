using UnityEngine;

public class ServerTutorialIconController : ServerIconTutorialBase
{
	private TutorialIconController m_tutorialController;

	private ServerCampaignFlowController m_serverFlowController;

	private TutorialIconController.TutorialStage m_stage;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_tutorialController = (TutorialIconController)synchronisedObject;
		m_serverFlowController = m_iServerFlowController as ServerCampaignFlowController;
	}

	protected override void OnStartTutorial()
	{
		m_serverFlowController.SetOrdersAutoProgress(false);
		m_serverFlowController.RegisterOnSuccessfulDeliveryCallback(OnSuccessfulOrder);
		m_serverFlowController.AddNextOrder();
	}

	protected override void OnTutorialUpdate()
	{
	}

	protected override void OnStopTutorial()
	{
	}

	protected void OnSuccessfulOrder(RecipeList.Entry _node)
	{
		if (m_stage == TutorialIconController.TutorialStage.Lettuce)
		{
			m_stage = TutorialIconController.TutorialStage.LettuceTomato;
			m_serverFlowController.AddNextOrder();
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomato)
		{
			m_stage = TutorialIconController.TutorialStage.LettuceTomatoCucumber;
			m_serverFlowController.AddNextOrder();
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			m_serverFlowController.SetOrdersAutoProgress(true);
			m_serverFlowController.UnregisterOnSuccessfulDeliveryCallback(OnSuccessfulOrder);
		}
	}
}
