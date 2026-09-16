public interface IClientOrderDefinition
{
	AssembledDefinitionNode GetOrderComposition();

	void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback);

	void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback);
}
