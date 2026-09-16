using System.Collections;

public abstract class FlowroutineBase : IFlowroutineBuilder<FlowroutineData>
{
	public virtual IFlowroutine BuildFlowroutine(FlowroutineData flowroutineData)
	{
		Setup(flowroutineData);
		return new FlowroutineAdapter(Run(), Shutdown);
	}

	protected abstract void Setup(FlowroutineData flowroutineData);

	protected abstract IEnumerator Run();

	protected abstract void Shutdown();
}
