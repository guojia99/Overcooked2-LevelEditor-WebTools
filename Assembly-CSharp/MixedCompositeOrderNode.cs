using System;

[Serializable]
public class MixedCompositeOrderNode : CompositeOrderNode
{
	public enum MixingProgress
	{
		Unmixed = 0,
		Mixed = 1,
		OverMixed = 2
	}

	public MixingProgress m_progress = MixingProgress.Mixed;

	public override AssembledDefinitionNode Convert()
	{
		MixedCompositeAssembledNode mixedCompositeAssembledNode = new MixedCompositeAssembledNode();
		mixedCompositeAssembledNode.m_composition = m_composition.ConvertAll((OrderDefinitionNode x) => x.Convert());
		mixedCompositeAssembledNode.m_optional = m_optional.ConvertAll((OrderDefinitionNode x) => x.Convert());
		mixedCompositeAssembledNode.m_progress = m_progress;
		return mixedCompositeAssembledNode;
	}
}
