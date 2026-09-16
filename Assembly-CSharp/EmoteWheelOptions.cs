using System;
using UnityEngine;

[Serializable]
public class EmoteWheelOptions : ScriptableObject
{
	public const int c_MaxEmotes = 6;

	[SerializeField]
	public float m_radius = 100f;

	[SerializeField]
	public GameObject m_wheelPrefab;

	[SerializeField]
	public EmoteWheelOption[] m_options = new EmoteWheelOption[6];

	[SerializeField]
	public EmoteWheelOption.Connection[] m_originConnections = new EmoteWheelOption.Connection[8];

	protected virtual void Awake()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			m_options[i].m_animTriggerHash = Animator.StringToHash(m_options[i].m_animTrigger);
		}
	}

	public EmoteWheelOption.Connection[] ConnectionsForButton(int _buttonIdx)
	{
		if (_buttonIdx < 0 || _buttonIdx > 7)
		{
			return m_originConnections;
		}
		return m_options[_buttonIdx].m_connections;
	}
}
