using System;
using System.Collections.Generic;
using UnityEngine;

public class SpinnerIconManager : Manager
{
	public enum SpinnerIconType
	{
		Save = 0,
		Load = 1,
		Error = 2,
		Warning = 3,
		SpinnerDialog = 4,
		SavingDisabled = 5,
		COUNT = 6
	}

	[Serializable]
	public class SpinnerIcon
	{
		public SpinnerIconType m_type;

		public CanvasRenderer m_icon;

		public Animator m_animator;

		public float m_minTime;

		[Mask(typeof(SpinnerIconType))]
		public int m_mask;

		[HideInInspector]
		public SuppressionController m_arbitrators = new SuppressionController();

		private float m_startTime;

		public void Initialise()
		{
			m_startTime = Time.time - m_minTime;
		}

		public bool Update(bool[] _activeMask)
		{
			m_arbitrators.UpdateSuppressors();
			if (m_animator != null)
			{
				m_animator.speed = 1f;
			}
			if (IsVisible(_activeMask))
			{
				m_icon.gameObject.SetActive(true);
				return true;
			}
			m_icon.gameObject.SetActive(false);
			return false;
		}

		public bool IsVisible(bool[] _activeMask)
		{
			return (m_arbitrators.IsSuppressed() || Time.time - m_startTime < m_minTime) && CanShow(_activeMask);
		}

		public void ResetStartTime()
		{
			m_startTime = Time.time;
		}

		private bool CanShow(bool[] _activeMask)
		{
			for (int i = 0; i < _activeMask.Length; i++)
			{
				if (_activeMask[i] && MaskUtils.HasFlag(m_mask, (SpinnerIconType)i))
				{
					return false;
				}
			}
			return true;
		}
	}

	private static SpinnerIconManager m_instance;

	private bool[] m_activeMask = new bool[6];

	public List<SpinnerIcon> m_spinnerIcons;

	public static SpinnerIconManager Instance
	{
		get
		{
			return m_instance;
		}
	}

	private void Awake()
	{
		if (m_instance != null)
		{
			UnityEngine.Object.Destroy(this);
			return;
		}
		m_instance = this;
		for (int i = 0; i < m_spinnerIcons.Count; i++)
		{
			m_spinnerIcons[i].Initialise();
		}
	}

	public Suppressor Show(SpinnerIconType _type, UnityEngine.Object _arbitrator, bool _resetTimer = true)
	{
		SpinnerIcon spinnerIcon = FindEntry(_type);
		if (_resetTimer)
		{
			spinnerIcon.ResetStartTime();
		}
		return spinnerIcon.m_arbitrators.AddSuppressor(_arbitrator);
	}

	protected SpinnerIcon FindEntry(SpinnerIconType _type)
	{
		for (int i = 0; i < m_spinnerIcons.Count; i++)
		{
			SpinnerIcon spinnerIcon = m_spinnerIcons[i];
			if (spinnerIcon.m_type == _type)
			{
				return spinnerIcon;
			}
		}
		return null;
	}

	private void Update()
	{
		for (int i = 0; i < m_spinnerIcons.Count; i++)
		{
			SpinnerIcon spinnerIcon = m_spinnerIcons[i];
			spinnerIcon.Update(m_activeMask);
			m_activeMask[(int)spinnerIcon.m_type] = spinnerIcon.IsVisible(m_activeMask);
		}
	}

	private void OnDestroy()
	{
		if (m_instance == this)
		{
			m_instance = null;
		}
	}
}
