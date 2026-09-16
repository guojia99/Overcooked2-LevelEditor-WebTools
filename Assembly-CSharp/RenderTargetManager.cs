using System.Collections.Generic;
using UnityEngine;

public class RenderTargetManager : MonoBehaviour
{
	public delegate void OnRTRecreated();

	private class RT
	{
		public RenderTexture m_RenderTarget;

		public int m_ID;

		public bool m_bUsed;

		public int m_Width;

		public int m_Height;

		public int m_Depth;

		public RenderTextureFormat m_Format;

		public string m_DebugName;
	}

	private static List<RT> m_ListOfRTs = new List<RT>();

	private static int m_DebugCount = 0;

	public static OnRTRecreated m_OnRTRecreated;

	private static RT CreateNewRT(int width, int height, int depth, RenderTextureFormat format, string debugName)
	{
		RT rT = new RT();
		rT.m_ID = 6 + m_ListOfRTs.Count;
		rT.m_RenderTarget = new RenderTexture(width, height, depth, format);
		rT.m_RenderTarget.filterMode = FilterMode.Point;
		rT.m_RenderTarget.Create();
		rT.m_Width = width;
		rT.m_Height = height;
		rT.m_Depth = depth;
		rT.m_Format = format;
		rT.m_DebugName = debugName;
		m_ListOfRTs.Add(rT);
		return rT;
	}

	public static void DebugMinorAlloc()
	{
		int ID = 0;
		RequestRenderTarget(640 + m_DebugCount, 480, 0, RenderTextureFormat.ARGB32, ref ID, "DebugMinorAlloc");
		m_DebugCount++;
		Debug.Log("   ****** RenderTaret Debugs " + m_DebugCount);
	}

	public static RenderTexture RequestRenderTarget(int width, int height, int depth, RenderTextureFormat format, ref int ID, string debugName = "DN")
	{
		int count = m_ListOfRTs.Count;
		bool flag = false;
		for (int i = 0; i < count; i++)
		{
			RT rT = m_ListOfRTs[i];
			if (!rT.m_bUsed && rT.m_Width == width && rT.m_Height == height && rT.m_Depth == depth && rT.m_Format == format)
			{
				if (!rT.m_RenderTarget.IsCreated())
				{
					CheckForLostRTs();
				}
				rT.m_bUsed = true;
				ID = rT.m_ID;
				return rT.m_RenderTarget;
			}
		}
		if (!flag)
		{
			RT rT2 = CreateNewRT(width, height, depth, format, debugName);
			rT2.m_bUsed = true;
			ID = rT2.m_ID;
			return rT2.m_RenderTarget;
		}
		return null;
	}

	public static void ReleaseRenderTarget(ref int ID)
	{
		int count = m_ListOfRTs.Count;
		if (ID > 0)
		{
			for (int i = 0; i < count; i++)
			{
				if (m_ListOfRTs[i].m_ID == ID)
				{
					m_ListOfRTs[i].m_bUsed = false;
					ID = 0;
					break;
				}
			}
		}
		else if (ID == 0)
		{
			Debug.Log(" *****  ");
		}
	}

	public static void DebugInfo()
	{
		int count = m_ListOfRTs.Count;
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			if (m_ListOfRTs[i].m_bUsed)
			{
				Debug.Log("  *** Used   " + m_ListOfRTs[i].m_DebugName + "    ID=" + m_ListOfRTs[i].m_ID);
				num++;
			}
		}
		Debug.Log("   *** Render Target  " + num + "  used    out of " + count);
	}

	public static void CheckForLostRTs()
	{
		int count = m_ListOfRTs.Count;
		for (int i = 0; i < count; i++)
		{
			RT rT = m_ListOfRTs[i];
			if (rT != null && rT.m_RenderTarget != null && !rT.m_RenderTarget.IsCreated())
			{
				rT.m_RenderTarget.Create();
			}
		}
		if (m_OnRTRecreated != null)
		{
			m_OnRTRecreated();
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		CheckForLostRTs();
	}
}
