using UnityEngine;

public static class AssembledNodeTransfer
{
	public static bool CanCombineWithContents(AssembledDefinitionNode _ingredientToAdd, IIngredientContents _container)
	{
		if (_container as MonoBehaviour != null)
		{
			if ((_container as MonoBehaviour).gameObject.RequestComponent<ServerMixableContainer>() != null)
			{
				ServerMixableContainer serverMixableContainer = (_container as MonoBehaviour).gameObject.RequestComponent<ServerMixableContainer>();
				if (_ingredientToAdd is IngredientAssembledNode)
				{
					return serverMixableContainer.CanTransferPremixedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				}
				if (_ingredientToAdd is ItemAssembledNode)
				{
					return false;
				}
                AssembledDefinitionNode assembledDefinitionNode = _ingredientToAdd.Simpilfy();
                // patch
                CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
				if (cookedCompositeAssembledNode != null)
				{
					if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
						return false;
					if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked)
						return serverMixableContainer.CanTransferPremixedContents(new AssembledDefinitionNode[1] { cookedCompositeAssembledNode }, 0f);
				}
                // patch
                CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
				if (compositeAssembledNode != null)
				{
					AssembledDefinitionNode[] composition = compositeAssembledNode.m_composition;
					return serverMixableContainer.CanTransferPremixedContents(composition, 0f);
				}
				return serverMixableContainer.CanTransferPremixedContents(new AssembledDefinitionNode[1] { assembledDefinitionNode }, 0f);
			}
			if ((_container as MonoBehaviour).gameObject.RequestComponent<ServerCookableContainer>() != null)
			{
				ServerCookableContainer serverCookableContainer = (_container as MonoBehaviour).gameObject.RequestComponent<ServerCookableContainer>();
				if (_ingredientToAdd is CookedCompositeAssembledNode)
				{
					// patch
					CookedCompositeAssembledNode cookedCompositeAssembledNode = _ingredientToAdd as CookedCompositeAssembledNode;
					ServerCookingHandler serverCookingHandler = serverCookableContainer.GetCookingHandler();
					if (serverCookingHandler != null && cookedCompositeAssembledNode.m_cookingStep.m_uID != serverCookingHandler.AccessCookingType.m_uID)
					{
						if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
							return false;
						if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked)
							return serverCookableContainer.CanTransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
                    }
					// patch
					float _progress;
					AssembledDefinitionNode[] recookFromNode = GetRecookFromNode(_ingredientToAdd as CookedCompositeAssembledNode, out _progress);
					return serverCookableContainer.CanTransferPrecookedContents(recookFromNode, _progress);
				}
				if (_ingredientToAdd is MixedCompositeAssembledNode)
				{
					return serverCookableContainer.CanTransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				}
				if (_ingredientToAdd is IngredientAssembledNode)
				{
					return serverCookableContainer.CanTransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				}
				if (_ingredientToAdd is ItemAssembledNode)
				{
					return false;
				}
			}
			if ((_container as MonoBehaviour).gameObject.RequestComponent<ServerItemContainer>() != null)
			{
				ServerItemContainer serverItemContainer = (_container as MonoBehaviour).gameObject.RequestComponent<ServerItemContainer>();
				if (_ingredientToAdd is ItemAssembledNode)
				{
					return serverItemContainer.CanTransferItemContents(new AssembledDefinitionNode[1] { _ingredientToAdd });
				}
				return false;
			}
		}
		for (int i = 0; i < _container.GetContentsCount(); i++)
		{
			if (_container.GetContentsElement(i).GetType() == typeof(CompositeAssembledNode))
			{
				CompositeAssembledNode compositeAssembledNode2 = _container.GetContentsElement(i) as CompositeAssembledNode;
				if (!compositeAssembledNode2.CanAddOrderNode(_ingredientToAdd))
				{
					return false;
				}
			}
		}
		if (_ingredientToAdd.GetType() == typeof(CompositeAssembledNode))
		{
			CompositeAssembledNode compositeAssembledNode3 = _ingredientToAdd as CompositeAssembledNode;
			for (int num = _container.GetContentsCount() - 1; num >= 0; num--)
			{
				if (!compositeAssembledNode3.CanAddOrderNode(_container.GetContentsElement(num)))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static void CombineWithContents(AssembledDefinitionNode _ingredientToAdd, IIngredientContents _container, bool _dontUpdate)
	{
		if (_container as MonoBehaviour != null && (_container as MonoBehaviour).gameObject.RequestComponent<ServerMixableContainer>() != null)
		{
			ServerMixableContainer serverMixableContainer = (_container as MonoBehaviour).gameObject.RequestComponent<ServerMixableContainer>();
			if (_ingredientToAdd is IngredientAssembledNode)
			{
				serverMixableContainer.TransferPremixedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				return;
			}
            AssembledDefinitionNode assembledDefinitionNode = _ingredientToAdd.Simpilfy();
            // patch
            CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
			if (cookedCompositeAssembledNode != null)
			{
				if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
					return;
				if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked)
				{
					serverMixableContainer.TransferPremixedContents(new AssembledDefinitionNode[1] { cookedCompositeAssembledNode }, 0f);
					return;
				}
			}
            // patch
            CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
			MixedCompositeAssembledNode mixedCompositeAssembledNode = ((!(assembledDefinitionNode is MixedCompositeAssembledNode)) ? (_ingredientToAdd as MixedCompositeAssembledNode) : (assembledDefinitionNode as MixedCompositeAssembledNode));
			float normalisedMixingProgress = 0f;
			if (mixedCompositeAssembledNode != null && mixedCompositeAssembledNode.m_recordedProgress.HasValue)
			{
				normalisedMixingProgress = mixedCompositeAssembledNode.m_recordedProgress.Value;
			}
			if (compositeAssembledNode != null)
			{
				AssembledDefinitionNode[] composition = compositeAssembledNode.m_composition;
				serverMixableContainer.TransferPremixedContents(composition, normalisedMixingProgress);
			}
			else
			{
				serverMixableContainer.TransferPremixedContents(new AssembledDefinitionNode[1] { assembledDefinitionNode }, normalisedMixingProgress);
			}
			return;
		}
		if (_container as MonoBehaviour != null && (_container as MonoBehaviour).gameObject.RequestComponent<ServerCookableContainer>() != null)
		{
			ServerCookableContainer serverCookableContainer = (_container as MonoBehaviour).gameObject.RequestComponent<ServerCookableContainer>();
			if (_ingredientToAdd is CookedCompositeAssembledNode)
			{
                // patch
                CookedCompositeAssembledNode cookedCompositeAssembledNode = _ingredientToAdd as CookedCompositeAssembledNode;
                ServerCookingHandler serverCookingHandler = serverCookableContainer.GetCookingHandler();
				if (serverCookingHandler != null && cookedCompositeAssembledNode.m_cookingStep.m_uID != serverCookingHandler.AccessCookingType.m_uID)
				{
                    if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
                        return;
                    if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked)
					{
                        serverCookableContainer.TransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
						return;
                    }
					if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Raw)
					{
                        float _progress1;
                        AssembledDefinitionNode[] recookFromNode1 = GetRecookFromNode(cookedCompositeAssembledNode, out _progress1);
                        serverCookableContainer.TransferPrecookedContents(recookFromNode1, 0f);
                        return;
                    }
                    return;
				}
				// patch
				float _progress;
				AssembledDefinitionNode[] recookFromNode = GetRecookFromNode(_ingredientToAdd as CookedCompositeAssembledNode, out _progress);
				serverCookableContainer.TransferPrecookedContents(recookFromNode, _progress);
				return;
			}
			if (_ingredientToAdd is MixedCompositeAssembledNode)
			{
				serverCookableContainer.TransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				return;
			}
			if (_ingredientToAdd is IngredientAssembledNode)
			{
				serverCookableContainer.TransferPrecookedContents(new AssembledDefinitionNode[1] { _ingredientToAdd }, 0f);
				return;
			}
		}
		for (int i = 0; i < _container.GetContentsCount(); i++)
		{
			if (_container.GetContentsElement(i) is CompositeAssembledNode)
			{
				CompositeAssembledNode compositeAssembledNode2 = _container.GetContentsElement(i) as CompositeAssembledNode;
				if (compositeAssembledNode2.CanAddOrderNode(_ingredientToAdd))
				{
					compositeAssembledNode2.AddOrderNode(_ingredientToAdd, _dontUpdate);
					return;
				}
			}
		}
		if (_ingredientToAdd is CompositeAssembledNode)
		{
			CompositeAssembledNode compositeAssembledNode3 = _ingredientToAdd as CompositeAssembledNode;
			for (int num = _container.GetContentsCount() - 1; num >= 0; num--)
			{
				if (compositeAssembledNode3.CanAddOrderNode(_container.GetContentsElement(num)))
				{
					compositeAssembledNode3.AddOrderNode(_container.GetContentsElement(num), _dontUpdate);
					_container.RemoveIngredient(num);
				}
			}
		}
		_container.AddIngredient(_ingredientToAdd);
	}

	private static AssembledDefinitionNode[] GetRecookFromNode(CookedCompositeAssembledNode _cookednode, out float _progress)
	{
		_progress = 0f;
		if (_cookednode.m_recordedProgress.HasValue)
		{
			_progress = _cookednode.m_recordedProgress.Value;
		}
		else
		{
			switch (_cookednode.m_progress)
			{
			case CookedCompositeOrderNode.CookingProgress.Raw:
				_progress = 0f;
				break;
			case CookedCompositeOrderNode.CookingProgress.Cooked:
				_progress = 1f;
				break;
			case CookedCompositeOrderNode.CookingProgress.Burnt:
				_progress = 2f;
				break;
			}
		}
		return _cookednode.m_composition;
	}

	public static bool CanTransferFromContainer<T>(T _originObject, IIngredientContents _container) where T : MonoBehaviour, IOrderDefinition, IContainerTransferBehaviour
	{
		IngredientContainer ingredientContainer = _originObject.gameObject.RequestComponent<IngredientContainer>();
		if (ingredientContainer != null && _originObject.GetOrderComposition().Simpilfy() != AssembledDefinitionNode.NullNode && CanCombineWithContents(_originObject.GetOrderComposition(), _container))
		{
			if (_container as MonoBehaviour != null && (_container as MonoBehaviour).gameObject.RequestComponent<ServerLadleContainer>() != null && _originObject.gameObject.RequestComponent<ServerCookingHandler>() != null)
			{
				return true;
			}
			if (_container as MonoBehaviour != null && (_container as MonoBehaviour).gameObject.RequestComponent<ServerCookingHandler>() == null)
			{
				ServerCookingHandler serverCookingHandler = _originObject.gameObject.RequestComponent<ServerCookingHandler>();
				if (serverCookingHandler != null)
				{
					float cookingProgress = serverCookingHandler.GetCookingProgress();
					return cookingProgress == 0f || cookingProgress >= serverCookingHandler.AccessCookingTime;
				}
			}
			if (_container as MonoBehaviour != null && (_container as MonoBehaviour).gameObject.RequestComponent<ServerMixingHandler>() == null)
			{
				ServerMixingHandler serverMixingHandler = _originObject.gameObject.RequestComponent<ServerMixingHandler>();
				if (serverMixingHandler != null)
				{
					float mixingProgress = serverMixingHandler.GetMixingProgress();
					return mixingProgress == 0f || mixingProgress >= serverMixingHandler.AccessMixingTime;
				}
			}
			return true;
		}
		return false;
	}

	public static void TransferFromContainer<T>(T _originObject, IIngredientContents _targetContainer, bool _dontRemove) where T : MonoBehaviour, IOrderDefinition, IContainerTransferBehaviour
	{
		ServerIngredientContainer serverIngredientContainer = _originObject.gameObject.RequireComponent<ServerIngredientContainer>();
		if (serverIngredientContainer != null && serverIngredientContainer.GetContentsCount() > 0)
		{
			AssembledDefinitionNode assembledDefinitionNode = _originObject.GetOrderComposition();
			if (_dontRemove)
			{
				assembledDefinitionNode = assembledDefinitionNode.Simpilfy();
			}
			CombineWithContents(assembledDefinitionNode, _targetContainer, _dontRemove);
			if (!_dontRemove)
			{
				ServerMessenger.TriggerAudioMessage(GameOneShotAudioTag.PutDown, _originObject.gameObject.layer);
				serverIngredientContainer.Empty();
			}
		}
	}
}
