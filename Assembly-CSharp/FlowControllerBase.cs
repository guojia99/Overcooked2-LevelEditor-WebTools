using UnityEngine;

[RequireComponent(typeof(IntroFlowroutineBase))]
public abstract class FlowControllerBase : Manager
{
	[SerializeField]
	public GameConfig m_gameConfig;
}
