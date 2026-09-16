using System;

public static class AssembledDefinitionNodeFactory
{
	private delegate AssembledDefinitionNode CreateMethod();

	public const int kBitsPerType = 4;

	private static Type[] m_typeLookup = new Type[6]
	{
		typeof(NullAssembledNode),
		typeof(IngredientAssembledNode),
		typeof(CompositeAssembledNode),
		typeof(CookedCompositeAssembledNode),
		typeof(MixedCompositeAssembledNode),
		typeof(ItemAssembledNode)
	};

	private static CreateMethod[] m_createLookup = new CreateMethod[6] { CreateNode<NullAssembledNode>, CreateNode<IngredientAssembledNode>, CreateNode<CompositeAssembledNode>, CreateNode<CookedCompositeAssembledNode>, CreateNode<MixedCompositeAssembledNode>, CreateNode<ItemAssembledNode> };

	private static T CreateNode<T>() where T : AssembledDefinitionNode, new()
	{
		return new T();
	}

	public static int GetNodeType(AssembledDefinitionNode node)
	{
		return m_typeLookup.FindIndex_Predicate((Type x) => x == node.GetType());
	}

	public static AssembledDefinitionNode CreateNode(int type)
	{
		return m_createLookup[type]();
	}
}
