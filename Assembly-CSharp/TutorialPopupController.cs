using System;
using UnityEngine;

public class TutorialPopupController : MonoBehaviour
{
	private CallbackVoid m_dismissedCallback = delegate
	{
	};

	public void RegisterDismissCallback(CallbackVoid _callback)
	{
		m_dismissedCallback = (CallbackVoid)Delegate.Combine(m_dismissedCallback, _callback);
	}

	public void DeregisterDismissCallback(CallbackVoid _callback)
	{
		m_dismissedCallback = (CallbackVoid)Delegate.Remove(m_dismissedCallback, _callback);
	}

	public void OnTutorialDismissed()
	{
		m_dismissedCallback();
	}
}
