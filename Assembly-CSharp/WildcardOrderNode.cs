using System;

[Serializable]
public class WildcardOrderNode : OrderDefinitionNode
{
	public CookingStepData m_cookingStep;

	public override AssembledDefinitionNode Convert()
	{
		return AssembledDefinitionNode.NullNode;
	}
}
