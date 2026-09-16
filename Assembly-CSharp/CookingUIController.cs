using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CookingUIController : HoverIconUIController
{
	public enum State
	{
		Idle = 0,
		Progressing = 1,
		Completed = 2,
		OverDoing = 3,
		Ruined = 4
	}

	[SerializeField]
	private State m_currentState;

	[SerializeField]
	private ProgressBarUI m_progressBar;

	[SerializeField]
	private ImagePulser m_warningIcon;

	[SerializeField]
	private Image m_tick;

	[SerializeField]
	private float[] m_pulseMultipliers = new float[1] { 1f };

	[SerializeField]
	private float m_tickAnimationLength;

	private IEnumerator m_TickAnimation;

	public State CurrentState
	{
		get
		{
			return m_currentState;
		}
	}

	public void SetState(State _state)
	{
		if (_state == m_currentState)
		{
			return;
		}
		m_TickAnimation = null;
		m_tick.gameObject.SetActive(false);
		switch (_state)
		{
		case State.Idle:
			DoIdleState();
			break;
		case State.Progressing:
			m_progressBar.gameObject.SetActive(true);
			m_warningIcon.gameObject.SetActive(false);
			break;
		case State.Completed:
			m_progressBar.gameObject.SetActive(false);
			m_warningIcon.gameObject.SetActive(false);
			if (m_currentState == State.Progressing)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.ImCooked, base.gameObject.layer);
			}
			m_TickAnimation = RunTickAnimation();
			break;
		case State.OverDoing:
			m_progressBar.gameObject.SetActive(false);
			m_warningIcon.gameObject.SetActive(true);
			break;
		case State.Ruined:
			m_progressBar.gameObject.SetActive(false);
			m_warningIcon.gameObject.SetActive(false);
			break;
		}
		m_currentState = _state;
		UI_Move uI_Move = base.gameObject.RequireComponent<UI_Move>();
		if (uI_Move != null)
		{
			uI_Move.UpdateGraphics();
		}
	}

	private void OnDisable()
	{
		if (m_TickAnimation != null)
		{
			m_TickAnimation = null;
			m_tick.gameObject.SetActive(false);
		}
	}

	public void Update()
	{
		if (m_TickAnimation != null && !m_TickAnimation.MoveNext())
		{
			m_TickAnimation = null;
		}
	}

	private IEnumerator RunTickAnimation()
	{
		m_tick.gameObject.SetActive(true);
		Color c = m_tick.color;
		for (float timer = 0f; timer < m_tickAnimationLength; timer += TimeManager.GetDeltaTime(base.gameObject))
		{
			c.a = 0.5f * (1f - Mathf.Cos((float)Math.PI * 2f * Mathf.Clamp01(timer / m_tickAnimationLength)));
			m_tick.color = c;
			yield return null;
		}
		m_tick.gameObject.SetActive(false);
	}

	public void SetProgress(float _value)
	{
		m_progressBar.SetValue(_value);
	}

	public void SetOverDoingAmount(float _value)
	{
		int num = (int)Mathf.Round(MathUtils.ClampedRemap(_value, 0f, 1f, -0.49f, (float)m_pulseMultipliers.Length - 0.51f));
		m_warningIcon.SetPulseSpeedMultiplier(m_pulseMultipliers[num]);
	}

	protected override void Awake()
	{
		base.Awake();
		SetState(State.Idle);
		DoIdleState();
		m_warningIcon.SetPulseCallback(OnOverDoingPulse);
	}

	private void OnOverDoingPulse()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.CookingWarning, base.gameObject.layer);
	}

	private void DoIdleState()
	{
		m_progressBar.gameObject.SetActive(false);
		m_warningIcon.gameObject.SetActive(false);
		m_tick.gameObject.SetActive(false);
	}
}
