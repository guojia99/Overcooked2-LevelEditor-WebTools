using UnityEngine;

namespace GameModes.Horde
{
	public class HordeTargetUIController : HoverIconUIController
	{
		public enum State
		{
			Idle = 0,
			Repairing = 1,
			UnderAttack = 2,
			Broken = 3
		}

		[SerializeField]
		private State m_currentState;

		[SerializeField]
		private ProgressBarUI m_progressBar;

		[SerializeField]
		private ImagePulser m_warningIcon;

		[SerializeField]
		private float m_warningIconThreshold = 1f;

		[SerializeField]
		private float[] m_pulseMultipliers = new float[1] { 1f };

		private float m_progressAnticipation;

		public State CurrentState
		{
			get
			{
				return m_currentState;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			SetState(State.Idle);
			m_progressBar.gameObject.SetActive(false);
			m_warningIcon.gameObject.SetActive(false);
			m_warningIcon.SetPulseCallback(OnWarningPulse);
		}

		public void SetState(State _state)
		{
			if (_state != m_currentState)
			{
				switch (_state)
				{
				case State.Idle:
					m_progressBar.gameObject.SetActive(false);
					m_warningIcon.gameObject.SetActive(false);
					break;
				case State.Repairing:
					m_progressBar.gameObject.SetActive(true);
					m_warningIcon.gameObject.SetActive(false);
					break;
				case State.UnderAttack:
					m_progressBar.gameObject.SetActive(false);
					m_warningIcon.gameObject.SetActive(true);
					break;
				case State.Broken:
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
		}

		public void SetProgress(float _value)
		{
			m_progressBar.SetValue(_value);
			float num = _value - m_progressAnticipation;
			if (num <= m_warningIconThreshold)
			{
				int num2 = (int)Mathf.Round(MathUtils.ClampedRemap(num, 0f, m_warningIconThreshold, (float)m_pulseMultipliers.Length - 0.51f, -0.49f));
				m_warningIcon.SetPulseSpeedMultiplier(m_pulseMultipliers[num2]);
				m_warningIcon.gameObject.SetActive(m_currentState == State.UnderAttack);
			}
			else
			{
				m_warningIcon.gameObject.SetActive(false);
			}
		}

		public void SetProgressWarningAnticipation(float _value)
		{
			m_progressAnticipation = _value;
		}

		private void OnWarningPulse()
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.CookingWarning, base.gameObject.layer);
		}
	}
}
