using System;

[Serializable]
public class CookedCompositeOrderNode : CompositeOrderNode
{
	public enum CookingProgress
	{
		Raw = 0,
		Cooked = 1,
		Burnt = 2
	}

	public CookingStepData m_cookingStep;

	public CookingProgress m_progress = CookingProgress.Cooked;

	public override AssembledDefinitionNode Convert()
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
		cookedCompositeAssembledNode.m_composition = m_composition.ConvertAll((OrderDefinitionNode x) => x.Convert());
		cookedCompositeAssembledNode.m_optional = m_optional.ConvertAll((OrderDefinitionNode x) => x.Convert());
		cookedCompositeAssembledNode.m_cookingStep = m_cookingStep;
		cookedCompositeAssembledNode.m_progress = m_progress;
		return cookedCompositeAssembledNode;
	}
}
