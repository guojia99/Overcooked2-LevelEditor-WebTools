public class ServerTrayIngredientContainer : ServerIngredientContainer
{
	public override void AddIngredient(AssembledDefinitionNode _orderData)
	{
		CompositeAssembledNode compositeAssembledNode = _orderData as CompositeAssembledNode;
		if (compositeAssembledNode != null && compositeAssembledNode.m_permittedEntries.Count > 0)
		{
			for (int i = 0; i < compositeAssembledNode.m_composition.Length; i++)
			{
				base.AddIngredient(compositeAssembledNode.m_composition[i]);
			}
		}
		else
		{
			base.AddIngredient(_orderData);
		}
	}
}
