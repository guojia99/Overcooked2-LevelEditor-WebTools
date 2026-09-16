public interface IOrderDefinition
{
	AssembledDefinitionNode GetOrderComposition();

	void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback);

	void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback);
}
