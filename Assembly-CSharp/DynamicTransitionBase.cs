using System.Collections;
using UnityEngine;

public abstract class DynamicTransitionBase : MonoBehaviour
{
	public abstract void Setup(CallbackVoid _endTransitionCallback);

	public abstract IEnumerator Run();
}
