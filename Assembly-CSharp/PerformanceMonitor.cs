using UnityEngine;

public class PerformanceMonitor : DebugDisplay
{
	private const int MAX_FPS = 500;

	private int[] m_FPSCounts = new int[501];

	private int m_numSamples;

	private bool m_bStarted;

	private string m_displayString = string.Empty;

	public override void OnSetUp()
	{
		m_displayString = "PStats: Idle";
	}

	public override void OnUpdate()
	{
		if (m_bStarted)
		{
			AddSample(1f / Time.deltaTime);
		}
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		DrawText(ref rect, style, m_displayString);
	}

	public void Begin()
	{
		Reset();
		m_bStarted = true;
		m_displayString = "PStats: Recording";
	}

	public void End()
	{
		m_bStarted = false;
		m_displayString = "PStats:  10th:" + GetPercentile(10f) + "  25th:" + GetPercentile(25f) + "  50th:" + GetPercentile(50f) + "  75th:" + GetPercentile(75f) + "  90th:" + GetPercentile(90f) + "  Av:" + GetMeanFPS() + "  Sd:" + GetStdDev();
	}

	public float GetMedianFPS()
	{
		return GetPercentile(50f);
	}

	public float GetMeanFPS()
	{
		float num = 0f;
		for (int i = 0; i < 501; i++)
		{
			num += (float)(i * m_FPSCounts[i]);
		}
		return num / (float)m_numSamples;
	}

	public float GetStdDev()
	{
		float meanFPS = GetMeanFPS();
		float num = 0f;
		for (int i = 0; i < 501; i++)
		{
			if (m_FPSCounts[i] > 0)
			{
				float num2 = (float)i - meanFPS;
				num += num2 * num2 * (float)m_FPSCounts[i];
			}
		}
		float f = num / (float)m_numSamples;
		return Mathf.Sqrt(f);
	}

	private void AddSample(float fps)
	{
		int num = Mathf.Clamp((int)(fps + 0.5f), 0, 500);
		m_FPSCounts[num]++;
		m_numSamples++;
	}

	private void Reset()
	{
		m_numSamples = 0;
		for (int i = 0; i < 501; i++)
		{
			m_FPSCounts[i] = 0;
		}
	}

	private float GetPercentile(float percentile)
	{
		percentile = Mathf.Clamp(percentile, 0f, 100f);
		int num = Mathf.CeilToInt((float)m_numSamples * (percentile / 100f));
		int num2 = 0;
		int i;
		for (i = 0; i < 501; i++)
		{
			num2 += m_FPSCounts[i];
			if (num <= num2)
			{
				break;
			}
		}
		return i;
	}
}
