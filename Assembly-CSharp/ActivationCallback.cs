using UnityEngine;

public class ActivationCallback : MonoBehaviour
{
	public event VoidGeneric<bool> StateChangeCallbacks = delegate
	{
	};

	public event CallbackVoid ActivateCallbacks = delegate
	{
	};

	public event CallbackVoid DeactivateCallbacks = delegate
	{
	};

	private void OnEnable()
	{
		this.StateChangeCallbacks(true);
		this.ActivateCallbacks();
	}

	private void OnDisable()
	{
		this.StateChangeCallbacks(false);
		this.DeactivateCallbacks();
	}
}
