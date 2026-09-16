using UnityEngine;

public class SendTriggerToObject : StateMachineBehaviour
{
	[SerializeField]
	private string m_objectName;

	[SerializeField]
	private string m_triggerToSend;

	[SerializeField]
	private float m_triggerTime;

	[SerializeField]
	private bool m_orTriggerOnExit;

	private float m_timer;

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_timer = 0f;
		if (!m_orTriggerOnExit && m_triggerTime == 0f)
		{
			SendTrigger(GetTarget(_animator), m_triggerToSend);
		}
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (!m_orTriggerOnExit && m_timer < m_triggerTime)
		{
			m_timer += TimeManager.GetDeltaTime(_animator.gameObject) * _animator.speed;
			if (m_timer >= m_triggerTime)
			{
				SendTrigger(GetTarget(_animator), m_triggerToSend);
			}
		}
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (m_orTriggerOnExit)
		{
			SendTrigger(GetTarget(_animator), m_triggerToSend);
		}
	}

	private GameObject GetTarget(Animator _animator)
	{
		GameObject result = _animator.gameObject;
		if (m_objectName != string.Empty)
		{
			result = GameObject.Find(m_objectName);
		}
		return result;
	}

	private void SendTrigger(GameObject _object, string _trigger)
	{
		if (_object != null)
		{
			_object.SendMessage("OnTrigger", _trigger, SendMessageOptions.DontRequireReceiver);
		}
	}
}
