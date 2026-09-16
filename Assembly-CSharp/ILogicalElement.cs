public interface ILogicalElement
{
	void GetLogicTreeData(out AcyclicGraph<ILogicalElement, LogicalLinkInfo> _graph, out AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head);
}
