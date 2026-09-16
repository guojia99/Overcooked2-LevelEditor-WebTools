using UnityEngine;

[ExecuteInEditMode]
public class ScrollingListGUI : MonoBehaviour, IScrollingListUI
{
	[SerializeField]
	private ScrollingListWidgetConfig m_scrollingListConfig;

	[SerializeField]
	private bool m_worldSpace;

	private ScrollingListWidget m_scrollingListWidget;

	private Vector2 m_position;

	public void SetNames(string[] _names)
	{
		m_scrollingListWidget.SetNames(_names);
	}

	public void MoveUp()
	{
		m_scrollingListWidget.MoveUp();
	}

	public void MoveDown()
	{
		m_scrollingListWidget.MoveDown();
	}

	public int GetSelection()
	{
		return m_scrollingListWidget.GetSelection();
	}

	protected ScrollingListWidgetConfig GetConfig()
	{
		return m_scrollingListConfig;
	}

	protected virtual void OnGUI()
	{
		if (m_worldSpace)
		{
			m_scrollingListWidget.Draw(base.transform.position);
		}
		else
		{
			m_scrollingListWidget.Draw(null);
		}
	}

	protected virtual void Awake()
	{
		m_scrollingListWidget = new ScrollingListWidget(m_scrollingListConfig);
	}
}
