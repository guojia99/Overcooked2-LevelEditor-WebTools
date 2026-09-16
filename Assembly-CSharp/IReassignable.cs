public interface IReassignable<T> where T : ILogicalElement
{
	void Reassign(T _childNode);
}
