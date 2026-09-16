using UnityEngine;

public class FPS_No_String_Allocs
{
	private string[] m_PreAllocatedReturnStrings;

	private const float MIN_FPS = 1f;

	private const float MAX_FPS = 66f;

	private const int NUM_VALUES = 500;

	private const float STEP = 0.13f;

	private const float INV_STEP = 7.692308f;

	private const int NUM_FPS_SAMPLES = 10;

	private float[] m_FPS = new float[10];

	private float m_AveFPS;

	private int m_OldFPSSlot;

	public FPS_No_String_Allocs()
	{
		m_PreAllocatedReturnStrings = new string[502];
		float num = 1f;
		for (int i = 0; i < 500; i++)
		{
			m_PreAllocatedReturnStrings[i] = string.Format("FPS: {0:F2}", num);
			num += 0.13f;
		}
		m_PreAllocatedReturnStrings[500] = "FPS: ^^^^^ ";
		m_PreAllocatedReturnStrings[501] = "FPS: _____ ";
	}

	public void Update()
	{
		m_FPS[m_OldFPSSlot] = Time.deltaTime;
		float num = 0f;
		for (int i = 0; i < 10; i++)
		{
			num += m_FPS[i];
		}
		num = 10f / num;
		m_OldFPSSlot++;
		m_AveFPS = num;
		if (m_OldFPSSlot >= 10)
		{
			m_OldFPSSlot = 0;
		}
	}

	public string GetString()
	{
		int num = (int)((m_AveFPS - 1f) * 7.692308f);
		if (num > 500)
		{
			num = 500;
		}
		else if (num < 0)
		{
			num = 501;
		}
		return m_PreAllocatedReturnStrings[num];
	}

	public float AverageFPS()
	{
		return m_AveFPS;
	}
}
