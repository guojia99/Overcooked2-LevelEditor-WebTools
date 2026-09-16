using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class T17OverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IEventSystemHandler
{
	public UnityEvent m_OnEnter;

	public UnityEvent m_OnLeave;

	private bool m_bOver;

	private void Awake()
	{
	}

	private void OnDisable()
	{
		if (m_bOver)
		{
			Leave();
		}
	}

	public virtual void OnPointerEnter(PointerEventData eventData)
	{
		Enter();
	}

	public virtual void OnPointerExit(PointerEventData eventData)
	{
		Leave();
	}

	private void Enter()
	{
		m_bOver = true;
		if (m_OnEnter != null)
		{
			m_OnEnter.Invoke();
		}
	}

	private void Leave()
	{
		m_bOver = false;
		if (m_OnLeave != null)
		{
			m_OnLeave.Invoke();
		}
	}
}
