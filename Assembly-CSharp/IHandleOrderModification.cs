public interface IHandleOrderModification
{
	bool CanAddOrderContents(AssembledDefinitionNode[] _contents);

	void AddOrderContents(AssembledDefinitionNode[] _contents);
}
