using System;
using UnityEngine;

[RequireComponent(typeof(ConveyorStation))]
public class TriggerAnimationOnConveyor : MonoBehaviour, ITriggerReceiver
{
	public enum State
	{
		Idle = 0,
		Pending = 1,
		Animating = 2
	}

	[SerializeField]
	public string m_startTrigger;

	[SerializeField]
	[ReadOnly]
	public string m_animationStartTrigger = "Animate";

	[SerializeField]
	[ReadOnly]
	public string m_animationFinishedTrigger = "AnimationFinished";

	[SerializeField]
	public bool m_stopWhileAnimating = true;

	public int m_animationStartTriggerHash;

	private GenericVoid<string> m_onTriggerCallback = delegate
	{
	};

	public void Awake()
	{
		m_animationStartTriggerHash = Animator.StringToHash(m_animationStartTrigger);
		TriggerConveyorAdjacentUpdate triggerConveyorAdjacentUpdate = base.gameObject.RequestComponent<TriggerConveyorAdjacentUpdate>();
		if (triggerConveyorAdjacentUpdate == null)
		{
			triggerConveyorAdjacentUpdate = base.gameObject.AddComponent<TriggerConveyorAdjacentUpdate>();
			triggerConveyorAdjacentUpdate.hideFlags = HideFlags.NotEditable;
			triggerConveyorAdjacentUpdate.m_updateTrigger = m_animationFinishedTrigger;
		}
	}

	public void RegisterOnTriggerCallback(GenericVoid<string> _callback)
	{
		m_onTriggerCallback = (GenericVoid<string>)Delegate.Combine(m_onTriggerCallback, _callback);
	}

	public void UnregisterOnTriggerCallback(GenericVoid<string> _callback)
	{
		m_onTriggerCallback = (GenericVoid<string>)Delegate.Remove(m_onTriggerCallback, _callback);
	}

	public void OnTrigger(string _trigger)
	{
		m_onTriggerCallback(_trigger);
	}
}
