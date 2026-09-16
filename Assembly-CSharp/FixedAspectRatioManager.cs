using System;
using UnityEngine;

public class FixedAspectRatioManager : Manager
{
	[SerializeField]
	private Vector2 m_aspectRatio = new Vector2(16f, 9f);

	private Rect m_correctedRect = default(Rect);

	private float m_screenWidth;

	private float m_screenHeight;

	private bool m_bInitialised;

	private GenericVoid<Rect, float, float> OnResolutionChangedCallback = delegate
	{
	};

	public void InitialiseComponent(GenericVoid<Rect, float, float> callback)
	{
		if (m_bInitialised)
		{
			callback(m_correctedRect, m_screenWidth, m_screenHeight);
		}
	}

	private void LateUpdate()
	{
		if ((float)Screen.width != m_screenWidth || (float)Screen.height != m_screenHeight)
		{
			m_screenWidth = Screen.width;
			m_screenHeight = Screen.height;
			m_correctedRect = FixedAspectRatio.ComputeAspectRatioRect(m_aspectRatio.x, m_aspectRatio.y, m_screenWidth, m_screenHeight);
			if (OnResolutionChangedCallback != null)
			{
				OnResolutionChangedCallback(m_correctedRect, m_screenWidth, m_screenHeight);
			}
			m_bInitialised = true;
		}
	}

	public void RegisterOnResolutionChanged(GenericVoid<Rect, float, float> callback)
	{
		OnResolutionChangedCallback = (GenericVoid<Rect, float, float>)Delegate.Combine(OnResolutionChangedCallback, callback);
	}

	public void UnregisterOnResolutionChanged(GenericVoid<Rect, float, float> callback)
	{
		OnResolutionChangedCallback = (GenericVoid<Rect, float, float>)Delegate.Remove(OnResolutionChangedCallback, callback);
	}
}
