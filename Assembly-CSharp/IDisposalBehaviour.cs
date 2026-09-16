public interface IDisposalBehaviour
{
	void AddToDisposer(ICarrier _carrier, IDisposer _iDisposer);

	void AddToDisposer(IDisposer _iDisposer);

	void Destroying(IDisposer _iDisposer);

	bool WillBeDestroyed();
}
