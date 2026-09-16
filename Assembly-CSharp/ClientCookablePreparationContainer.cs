using UnityEngine;

public class ClientCookablePreparationContainer : ClientPreparationContainer
{
	private ClientCookingHandler m_cookingHandler;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookingHandler = base.gameObject.RequireComponent<ClientCookingHandler>();
		m_cookingHandler.CookingStateChangedCallback += delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		};
		base.StartSynchronising(synchronisedObject);
	}

	protected override CompositeAssembledNode GetAsOrderComposite()
	{
		CompositeAssembledNode asOrderComposite = base.GetAsOrderComposite();
		CookedCompositeAssembledNode cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
		cookedCompositeAssembledNode.m_freeObject = asOrderComposite.m_freeObject;
		cookedCompositeAssembledNode.m_composition = asOrderComposite.m_composition;
		cookedCompositeAssembledNode.m_optional = asOrderComposite.m_optional;
		cookedCompositeAssembledNode.m_permittedEntries = asOrderComposite.m_permittedEntries;
		cookedCompositeAssembledNode.m_cookingStep = m_cookingHandler.AccessCookingType;
		cookedCompositeAssembledNode.m_recordedProgress = m_cookingHandler.GetCookingProgress() / m_cookingHandler.AccessCookingTime;
		cookedCompositeAssembledNode.m_progress = m_cookingHandler.GetCookedOrderState();
		return cookedCompositeAssembledNode;
	}
}
