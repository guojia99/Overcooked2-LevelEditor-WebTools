using UnityEngine;
using UnityEngine.UI;

public class FixedAspectRatioLetterbox : MonoBehaviour
{
	[SerializeField]
	private Image m_top;

	[SerializeField]
	private Image m_bottom;

	[SerializeField]
	private Image m_left;

	[SerializeField]
	private Image m_right;

	private FixedAspectRatioManager m_manager;

	private void Start()
	{
		m_manager = base.gameObject.RequestComponent<FixedAspectRatioManager>();
		if (m_manager == null)
		{
			m_manager = base.gameObject.transform.parent.gameObject.RequireComponent<FixedAspectRatioManager>();
		}
		m_manager.RegisterOnResolutionChanged(ResizeLetterbox);
		m_manager.InitialiseComponent(ResizeLetterbox);
	}

	private void OnDestroy()
	{
		if (m_manager != null)
		{
			m_manager.UnregisterOnResolutionChanged(ResizeLetterbox);
		}
	}

	private void ResizeLetterbox(Rect _correctedRect, float _screenWidth, float _screenHeight)
	{
		if (m_top != null)
		{
			m_top.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0f, _screenHeight - _correctedRect.yMax * _screenHeight);
		}
		if (m_bottom != null)
		{
			m_bottom.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 0f, _screenHeight - _correctedRect.yMax * _screenHeight);
		}
		if (m_left != null)
		{
			m_left.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0f, _screenWidth - _correctedRect.xMax * _screenWidth);
		}
		if (m_right != null)
		{
			m_right.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 0f, _screenWidth - _correctedRect.xMax * _screenWidth);
		}
	}
}
