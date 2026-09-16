using System.Collections;
using UnityEngine;

public abstract class IntroFlowroutineBase : MonoBehaviour
{
	public abstract void Setup(CallbackVoid _startRoundCallback);

	public abstract IEnumerator Run();
}
