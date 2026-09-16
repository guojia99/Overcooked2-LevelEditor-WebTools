using System;
using UnityEngine;

public abstract class ScrollingListUIContainer : UISubElementContainer, IScrollingListUI
{
	[Serializable]
	public abstract class NameData
	{
	}

	[SerializeField]
	private int m_numLines = 10;

	[SerializeField]
	private GameObject m_unselectedTextPrefab;

	[SerializeField]
	private GameObject m_selectedTextPrefab;

	[SerializeField]
	[HideInInspector]
	private RectTransform m_textPlane;

	[SerializeField]
	[HideInInspector]
	private GameObject[] m_textElements;

	[SerializeField]
	[HideInInspector]
	private GameObject m_selectedText;

	[SerializeField]
	private int m_selectionId;

	private int m_anchorId;

	protected ScrollingListEntry GetSelectedEntry()
	{
		return m_selectedText.RequireComponent<ScrollingListEntry>();
	}

	protected ScrollingListEntry[] GetUnselectedEntries()
	{
		return m_textElements.ConvertAll(GameObjectUtils.RequireComponent<ScrollingListEntry>);
	}

	public void SetNumberOfLines(int _numLines)
	{
		m_numLines = _numLines;
		while (m_selectionId >= m_anchorId + m_numLines)
		{
			SetAnchorElement(m_anchorId + 1);
		}
		RefreshSubElements();
	}

	protected void OnSetNames()
	{
		RefreshSubElements();
	}

	protected abstract NameData[] GetNameData();

	public void MoveUp()
	{
		if (m_selectionId > 0)
		{
			SetSelection(m_selectionId - 1);
			if (m_selectionId < m_anchorId)
			{
				SetAnchorElement(m_anchorId - 1);
			}
		}
	}

	public void MoveDown()
	{
		if (m_selectionId + 1 < GetNameData().Length)
		{
			SetSelection(m_selectionId + 1);
			if (m_selectionId >= m_anchorId + m_numLines)
			{
				SetAnchorElement(m_anchorId + 1);
			}
		}
	}

	public int GetSelection()
	{
		return m_selectionId;
	}

	protected override void OnCreateSubObjects(GameObject _container)
	{
		m_textPlane = CreateRect(_container, "TextPlane");
		float num = 1f / (float)m_numLines;
		NameData[] nameData = GetNameData();
		Array.Resize(ref m_textElements, nameData.Length);
		for (int i = 0; i < nameData.Length; i++)
		{
			GameObject gameObject = m_unselectedTextPrefab.InstantiateOnParent(m_textPlane);
			gameObject.hideFlags = HideFlags.NotEditable;
			ScrollingListEntry scrollingListEntry = gameObject.RequireComponent<ScrollingListEntry>();
			scrollingListEntry.SetNameData(nameData[i]);
			RectTransform rectTransform = gameObject.RequireComponent<RectTransform>();
			rectTransform.anchorMin = new Vector2(0f, 1f - (float)(i + 1) * num);
			rectTransform.anchorMax = new Vector2(1f, 1f - (float)i * num);
			rectTransform.pivot = new Vector2(0f, 1f);
			rectTransform.offsetMin = new Vector2(0f, 0f);
			rectTransform.offsetMax = new Vector2(0f, 0f);
			rectTransform.localScale = Vector2.one;
			m_textElements[i] = gameObject;
		}
		m_selectedText = m_selectedTextPrefab.InstantiateOnParent(m_textPlane);
		m_selectedText.hideFlags = HideFlags.NotEditable;
		SetSelection(m_selectionId);
		SetAnchorElement(0);
	}

	private void SetAnchorElement(int _anchor)
	{
		m_anchorId = _anchor;
		m_textPlane.anchorMin = new Vector2(0f, (float)m_anchorId / (float)m_numLines);
		m_textPlane.anchorMax = new Vector2(1f, 1f + (float)m_anchorId / (float)m_numLines);
		for (int i = 0; i < GetNameData().Length; i++)
		{
			m_textElements[i].SetActive(i >= m_anchorId && i < m_anchorId + m_numLines && i != m_selectionId);
		}
	}

	private void SetSelection(int _selectionId)
	{
		if (m_textElements.TryAtIndex(m_selectionId) != null)
		{
			m_textElements[m_selectionId].SetActive(true);
		}
		m_selectionId = _selectionId;
		if (m_textElements.TryAtIndex(m_selectionId) != null)
		{
			m_textElements[m_selectionId].SetActive(false);
			RectTransform rectTransform = m_textElements[m_selectionId].gameObject.RequireComponent<RectTransform>();
			RectTransform rectTransform2 = m_selectedText.gameObject.RequireComponent<RectTransform>();
			rectTransform2.anchorMin = rectTransform.anchorMin;
			rectTransform2.anchorMax = rectTransform.anchorMax;
			rectTransform2.pivot = rectTransform.pivot;
			rectTransform2.offsetMin = rectTransform.offsetMin;
			rectTransform2.offsetMax = rectTransform.offsetMax;
			rectTransform2.localScale = rectTransform.localScale;
			ScrollingListEntry scrollingListEntry = m_selectedText.RequireComponent<ScrollingListEntry>();
			scrollingListEntry.SetNameData(GetNameData()[m_selectionId]);
		}
	}
}
