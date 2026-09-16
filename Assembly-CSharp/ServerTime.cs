using UnityEngine;

public class ServerTime
{
	public const float kTimeSyncFrequency = 3f;

	private static float m_fNextSyncTime;

	private static float m_fServerTime;

	private static float m_fLastTime;

	public static void Update()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		float num = realtimeSinceStartup - m_fLastTime;
		m_fServerTime += num;
		m_fLastTime = m_fServerTime;
		if (m_fServerTime > m_fNextSyncTime)
		{
			m_fNextSyncTime = 3f + Time.realtimeSinceStartup;
			ServerMessenger.TimeSync(m_fServerTime);
		}
	}

	public static void StartTime()
	{
		m_fServerTime = 0f;
		m_fLastTime = 0f;
	}
}
