using System;

[Serializable]
public class CompositeOrderNode : OrderDefinitionNode
{
	public OrderDefinitionNode[] m_composition = new OrderDefinitionNode[0];

	public OrderDefinitionNode[] m_optional = new OrderDefinitionNode[0];

	public override AssembledDefinitionNode Convert()
	{
		CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
		compositeAssembledNode.m_composition = m_composition.ConvertAll((OrderDefinitionNode x) => x.Convert());
		compositeAssembledNode.m_optional = m_optional.ConvertAll((OrderDefinitionNode x) => x.Convert());
		return compositeAssembledNode;
	}
}
