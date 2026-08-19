using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/MixableContainer")]
[RequireComponent(typeof(IngredientContainer))]
[RequireComponent(typeof(PlacementContainer))]
[RequireComponent(typeof(PhysicalAttachment))]
[RequireComponent(typeof(MixingHandler))]
public class MixableContainer : MonoBehaviour
{
	[SerializeField]
	public OrderDefinitionNode[] m_ApprovedIngredients;

	public AssembledDefinitionNode GetOrderComposition(IIngredientContents _ingredientContents, float _recordedProgress, MixedCompositeOrderNode.MixingProgress _mixingProgress)
	{
		MixedCompositeAssembledNode mixedCompositeAssembledNode = new MixedCompositeAssembledNode();
		mixedCompositeAssembledNode.m_composition = _ingredientContents.GetContents();
		mixedCompositeAssembledNode.m_recordedProgress = _recordedProgress;
		mixedCompositeAssembledNode.m_progress = _mixingProgress;
		return mixedCompositeAssembledNode;
	}

	public bool AllowItemPlacement(GameObject _object, PlacementContext _context, OrderDefinitionNode[] _orderDefinitionNodes, bool _overMixed, float _cookingProgress)
	{
        // patch
        //if (_overMixed)
        //{
        //	return false;
        //}
        //IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
        //if (orderDefinition == null)
        //{
        //	return false;
        //}
        //if (_object.RequestComponent<MixableContainer>() != null && _cookingProgress == 0f)
        //{
        //	MixedCompositeOrderNode.MixingProgress mixingProgress = MixedCompositeOrderNode.MixingProgress.Unmixed;
        //	ServerMixableContainer serverMixableContainer = _object.RequestComponent<ServerMixableContainer>();
        //	if (serverMixableContainer != null)
        //	{
        //		ServerMixingHandler mixingHandler = serverMixableContainer.GetMixingHandler();
        //		if (mixingHandler != null)
        //		{
        //			mixingProgress = mixingHandler.GetMixedOrderState();
        //		}
        //	}
        //	else
        //	{
        //		ClientMixableContainer clientMixableContainer = _object.RequestComponent<ClientMixableContainer>();
        //		if (clientMixableContainer != null)
        //		{
        //			ClientMixingHandler mixingHandler2 = clientMixableContainer.GetMixingHandler();
        //			if (mixingHandler2 != null)
        //			{
        //				mixingProgress = mixingHandler2.GetMixedOrderState();
        //			}
        //		}
        //	}
        //	if (mixingProgress == MixedCompositeOrderNode.MixingProgress.OverMixed)
        //	{
        //		return false;
        //	}
        //	if (base.transform.parent.GetComponentInParent<CookingStation>() != null && mixingProgress != MixedCompositeOrderNode.MixingProgress.Mixed)
        //	{
        //		return false;
        //	}
        //	CookedCompositeOrderNode.CookingProgress cookingProgress = CookedCompositeOrderNode.CookingProgress.Raw;
        //	ServerCookableContainer serverCookableContainer = _object.RequestComponent<ServerCookableContainer>();
        //	if (serverCookableContainer != null)
        //	{
        //		ServerCookingHandler cookingHandler = serverCookableContainer.GetCookingHandler();
        //		if (cookingHandler != null)
        //		{
        //			cookingProgress = cookingHandler.GetCookedOrderState();
        //		}
        //	}
        //	else
        //	{
        //		ClientCookableContainer clientCookableContainer = _object.RequestComponent<ClientCookableContainer>();
        //		if (clientCookableContainer != null)
        //		{
        //			ClientCookingHandler cookingHandler2 = clientCookableContainer.GetCookingHandler();
        //			if (cookingHandler2 != null)
        //			{
        //				cookingProgress = cookingHandler2.GetCookedOrderState();
        //			}
        //		}
        //	}
        //	if (cookingProgress == CookedCompositeOrderNode.CookingProgress.Raw)
        //	{
        //		IIngredientContents ingredientContents = _object.RequestInterface<IIngredientContents>();
        //		if (ingredientContents != null)
        //		{
        //			IIngredientContents ingredientContents2 = base.gameObject.RequestInterface<IIngredientContents>();
        //			if (ingredientContents2 != null && ingredientContents2.CanTakeContents(ingredientContents.GetContents()))
        //			{
        //				return true;
        //			}
        //		}
        //	}
        //}
        //else if (base.transform.parent.GetComponentInParent<CookingStation>() != null)
        //{
        //	return false;
        //}
        //if (_orderDefinitionNodes != null && _cookingProgress == 0f)
        //{
        //	for (int i = 0; i < _orderDefinitionNodes.Length; i++)
        //	{
        //		if (AssembledDefinitionNode.Matching(orderDefinition.GetOrderComposition(), _orderDefinitionNodes[i]))
        //		{
        //			return true;
        //		}
        //	}
        //}
        //return false;

        if (_orderDefinitionNodes.IsEmpty() || _overMixed || _cookingProgress != 0f)
            return false;
        IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
        if (orderDefinition == null)
            return false;

        AssembledDefinitionNode assembledDefinitionNode = orderDefinition.GetOrderComposition();
        if (_object.RequestComponent<MixableContainer>() != null)
        {
            // IOrderDefinition is a ServerCookableContainer for mixbowl or ServerMixableContainer for blender 
            if (assembledDefinitionNode is CookedCompositeAssembledNode)
            {
                CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
                if (cookedCompositeAssembledNode.m_progress != CookedCompositeOrderNode.CookingProgress.Raw)
                    return false;
                assembledDefinitionNode = cookedCompositeAssembledNode.m_composition[0];
            }
            if (assembledDefinitionNode is MixedCompositeAssembledNode)
            {
                MixedCompositeAssembledNode mixedCompositeAssembledNode = assembledDefinitionNode as MixedCompositeAssembledNode;
                if (mixedCompositeAssembledNode.m_progress == MixedCompositeOrderNode.MixingProgress.OverMixed)
                    return false;
            }
        }

        if (assembledDefinitionNode is CookedCompositeAssembledNode)
        {
            CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
            if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
                return false;
            if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked)
            {
                for (int i = 0; i < _orderDefinitionNodes.Length; i++)
                {
                    if (AssembledDefinitionNode.Matching(cookedCompositeAssembledNode, _orderDefinitionNodes[i]))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        if (assembledDefinitionNode is CompositeAssembledNode)
        {
            CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
            foreach (AssembledDefinitionNode composition in compositeAssembledNode.m_composition)
            {
                bool flag = false;
                for (int i = 0; i < _orderDefinitionNodes.Length; i++)
                {
                    if (AssembledDefinitionNode.Matching(composition, _orderDefinitionNodes[i]))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                    return false;
            }
            return true;
        }

        for (int i = 0; i < _orderDefinitionNodes.Length; i++)
        {
            if (AssembledDefinitionNode.Matching(assembledDefinitionNode, _orderDefinitionNodes[i]))
            {
                return true;
            }
        }
        return false;

        // patch
    }
}
