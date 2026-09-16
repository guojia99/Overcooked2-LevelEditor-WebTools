using System.Collections;
using UnityEngine;

public abstract class FlowroutineComponent<SetupData> : MonoBehaviour, IFlowroutineBuilder<SetupData>
{
	public IFlowroutine BuildFlowroutine(SetupData _setupData)
	{
		Setup(_setupData);
		return new FlowroutineAdapter(Run(), Shutdown);
	}

	protected abstract void Setup(SetupData _setupData);

	protected abstract IEnumerator Run();

	protected abstract void Shutdown();
}
