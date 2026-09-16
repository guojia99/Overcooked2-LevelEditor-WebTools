using UnityEngine;

public class ServerCookablePreparationContainer : ServerPreparationContainer
{
	private CookablePreparationContainer m_cookablePreparationContainer;

	private ServerCookingHandler m_cookingHandler;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cookablePreparationContainer = (CookablePreparationContainer)synchronisedObject;
		m_cookingHandler = base.gameObject.RequireComponent<ServerCookingHandler>();
		m_cookingHandler.CookingStateChangedCallback += delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		};
	}

	protected override bool CanAddIngredient(AssembledDefinitionNode _toAdd)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = GetAsOrderComposite() as CookedCompositeAssembledNode;
		if (cookedCompositeAssembledNode != null && cookedCompositeAssembledNode.m_progress != CookedCompositeOrderNode.CookingProgress.Raw)
		{
			return false;
		}
		return base.CanAddIngredient(_toAdd);
	}

	public override void AddOrderContents(AssembledDefinitionNode[] _contents)
	{
		base.AddOrderContents(_contents);
		if (_contents.Length > 0)
		{
			m_cookingHandler.SetCookingProgress(0f);
		}
	}

	public override bool CanTransferToContainer(IIngredientContents _container)
	{
		return m_cookingHandler.GetCookedOrderState() != CookedCompositeOrderNode.CookingProgress.Burnt && AssembledNodeTransfer.CanTransferFromContainer(this, _container) && base.CanTransferToContainer(_container);
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
