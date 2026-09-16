public interface IIngredientContents
{
	bool CanAddIngredient(AssembledDefinitionNode _orderData);

	void AddIngredient(AssembledDefinitionNode _orderData);

	AssembledDefinitionNode RemoveIngredient(int i);

	AssembledDefinitionNode GetContentsElement(int i);

	AssembledDefinitionNode[] GetContents();

	int GetContentsCount();

	bool CanTakeContents(AssembledDefinitionNode[] _contents);

	void Empty();

	bool HasContents();
}
