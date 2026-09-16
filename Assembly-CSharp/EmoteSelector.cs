using System;
using System.Collections.Generic;
using UnityEngine;

public class EmoteSelector
{
	protected class EmoteButton
	{
		private GameObject m_container;

		private GameObject m_normal;

		private GameObject m_highlight;

		public Vector3 Position
		{
			get
			{
				return m_container.transform.localPosition;
			}
		}

		public EmoteButton(GameObject _container, GameObject _normal, GameObject _highlight)
		{
			m_container = _container;
			m_normal = _normal;
			m_highlight = _highlight;
		}

		public void SetHighlighted(bool _highlighted)
		{
			if (!m_highlight.activeInHierarchy && _highlighted)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.UIEmoteToggle, m_container.layer);
			}
			m_normal.SetActive(!_highlighted);
			m_highlight.SetActive(_highlighted);
		}
	}

	private EmoteWheel m_emoteWheel;

	private EmoteButton[] m_options = new EmoteButton[6];

	private GameObject m_gameObject;

	private RectTransform m_rect;

	private RectTransform m_selector;

	private EmoteButton m_selected;

	private UI_Move m_uiMove;

	private Transform m_canvasRect;

	private Vector3 m_prevMousePos;

	private Vector2 m_analogPos = new Vector2(0f, 0f);

	private float m_analogOffsetThreshold;

	public EmoteSelector(EmoteWheel _emoteWheel, Transform _follow = null)
	{
		m_emoteWheel = _emoteWheel;
		m_gameObject = UnityEngine.Object.Instantiate(m_emoteWheel.m_emoteWheelOptions.m_wheelPrefab);
		if (m_emoteWheel.ForUI)
		{
			m_gameObject.transform.SetParent(m_emoteWheel.m_uiPlayer.EmoteWheelAnchor, false);
		}
		else
		{
			HoverIconUIController hoverIconUIController = m_gameObject.RequestComponent<HoverIconUIController>();
			if (hoverIconUIController == null)
			{
				hoverIconUIController = m_gameObject.AddComponent<HoverIconUIController>();
			}
			m_gameObject.transform.SetParent(GameUtils.GetNamedCanvas("ScalingHUDCanvas").transform, false);
			hoverIconUIController.SetFollowTransform(_follow, new Vector2(0f, 0f));
			hoverIconUIController.ShouldFollow = _follow != null;
		}
		m_rect = m_gameObject.RequestComponent<RectTransform>();
		m_rect.rotation = Quaternion.Euler(0f, 0f, 0f);
		m_uiMove = m_gameObject.RequestComponent<UI_Move>();
		Canvas canvas = m_gameObject.RequestComponentUpwardsRecursive<Canvas>();
		if (canvas != null)
		{
			m_canvasRect = canvas.transform;
		}
		m_selector = m_gameObject.transform.FindChildRecursive("Selector") as RectTransform;
		if (m_selector == null)
		{
		}
		SpawnEmoteOptions();
		Hide();
	}

	private void SpawnEmoteOptions()
	{
		float radius = m_emoteWheel.m_emoteWheelOptions.m_radius;
		for (int i = 0; i < m_emoteWheel.m_emoteWheelOptions.m_options.Length; i++)
		{
			if (!(m_emoteWheel.m_emoteWheelOptions.m_options[i].m_wheelButtonPrefab == null) && !(m_emoteWheel.m_emoteWheelOptions.m_options[i].m_wheelButtonHighlightPrefab == null))
			{
				float x = radius * Mathf.Cos(-(float)Math.PI / 3f * (float)i);
				float y = radius * Mathf.Sin(-(float)Math.PI / 3f * (float)i);
				GameObject gameObject = new GameObject("Emote_" + i);
				gameObject.transform.SetParent(m_gameObject.transform, false);
				gameObject.transform.localPosition = new Vector3(x, y, 0f);
				GameObject gameObject2 = UnityEngine.Object.Instantiate(m_emoteWheel.m_emoteWheelOptions.m_options[i].m_wheelButtonPrefab, gameObject.transform, false);
				gameObject2.transform.localPosition = new Vector3(0f, 0f, 0f);
				GameObject gameObject3 = UnityEngine.Object.Instantiate(m_emoteWheel.m_emoteWheelOptions.m_options[i].m_wheelButtonHighlightPrefab, gameObject.transform, false);
				gameObject3.transform.localPosition = new Vector3(0f, 0f, 0f);
				gameObject3.SetActive(false);
				m_options[i] = new EmoteButton(gameObject, gameObject2, gameObject3);
			}
		}
	}

	public bool IsActive()
	{
		return m_gameObject.activeInHierarchy;
	}

	public void Show()
	{
		if (Input.mousePresent)
		{
			m_prevMousePos = Input.mousePosition;
		}
		SetSelected(-1);
		m_analogOffsetThreshold = 0f;
		m_analogPos = new Vector2(0f, 0f);
		Update(0f, 0f);
		m_gameObject.SetActive(true);
	}

	public void Hide()
	{
		m_gameObject.SetActive(false);
	}

	private void Update(float _x, float _y)
	{
		Vector2 vector = new Vector2(_x, _y);
		float num = m_emoteWheel.m_emoteWheelOptions.m_radius * m_emoteWheel.m_emoteWheelOptions.m_radius;
		if (Mathf.Abs(vector.sqrMagnitude) > num)
		{
			vector = new Vector3(_x, _y, 0f).normalized * m_emoteWheel.m_emoteWheelOptions.m_radius;
		}
		m_selector.anchoredPosition = new Vector3(vector.x, vector.y, 0f);
	}

	public void AnalogUpdate(float _x, float _y)
	{
		Vector2 vector = new Vector2(_x, _y);
		float magnitude = (m_analogPos - vector).magnitude;
		if (Mathf.Abs(magnitude) > m_analogOffsetThreshold)
		{
			int closestButton = GetClosestButton(_x, _y);
			if (closestButton != -1)
			{
				SetSelected(closestButton);
				Update(m_selected.Position.x, m_selected.Position.y);
				int closestButton2 = GetClosestButton(_x, _y, m_selected);
				float magnitude2 = (vector - m_selected.Position.XY()).magnitude;
				float num = ((closestButton2 == -1) ? vector.magnitude : (vector - m_options[closestButton2].Position.XY()).magnitude);
				m_analogOffsetThreshold = num - magnitude2;
				m_analogPos.Set(_x, _y);
			}
			else
			{
				SetSelected(-1);
				Update(0f, 0f);
				m_analogOffsetThreshold = m_emoteWheel.m_emoteWheelOptions.m_radius * 0.5f;
				m_analogPos.Set(_x, _y);
			}
		}
	}

	public void Update(EmoteWheelOption.Connection.Direction _direction)
	{
		if (m_selected == null)
		{
			int selected = GetSelected();
			if (selected != -1)
			{
				SetSelected(selected);
			}
		}
		EmoteWheelOption.Connection[] array = m_emoteWheel.m_emoteWheelOptions.ConnectionsForButton((m_selected == null) ? (-1) : m_options.FindIndex_Predicate((EmoteButton x) => x == m_selected));
		int connectedTo = array[(int)_direction].m_connectedTo;
		if (connectedTo != -2)
		{
			SetSelected(connectedTo);
			if (m_selected != null)
			{
				Update(m_selected.Position.x, m_selected.Position.y);
			}
			else
			{
				Update(0f, 0f);
			}
		}
	}

	public void PointerUpdate()
	{
		if (Input.mousePresent)
		{
			Vector3 vector = m_prevMousePos - Input.mousePosition;
			if (vector.x != 0f || vector.y != 0f)
			{
				Vector3 a = ((!(m_uiMove != null)) ? Vector2.zero : m_uiMove.Offset);
				a = a.MultipliedBy(m_canvasRect.lossyScale);
				Vector2 localPoint;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rect, Input.mousePosition - a, null, out localPoint);
				Update(localPoint.x, localPoint.y);
				SetSelected(GetClosestButton(localPoint.x, localPoint.y));
			}
			m_prevMousePos = Input.mousePosition;
		}
	}

	protected void SetSelected(int _idx)
	{
		int num = -1;
		if (m_selected != null)
		{
			num = m_options.FindIndex_Predicate((EmoteButton x) => x == m_selected);
		}
		if (_idx != num && num != -1)
		{
			m_options[num].SetHighlighted(false);
		}
		if (_idx != -1)
		{
			m_selected = m_options[_idx];
			m_options[_idx].SetHighlighted(true);
		}
		else
		{
			m_selected = null;
		}
	}

	public int GetSelected()
	{
		return GetClosestButton(m_selector.anchoredPosition.x, m_selector.anchoredPosition.y);
	}

	protected int GetClosestButton(float _x, float _y, EmoteButton _exclude = null)
	{
		Vector2 pos = new Vector2(_x, _y);
		float sqDistToCenter = Vector2.SqrMagnitude(pos);
		Generic<float, EmoteButton> scoreFunction = delegate(EmoteButton _emote)
		{
			if (_exclude == _emote)
			{
				return float.MaxValue;
			}
			float num = Vector2.SqrMagnitude(_emote.Position.XY() - pos);
			return (num > sqDistToCenter) ? float.MaxValue : num;
		};
		KeyValuePair<int, EmoteButton> selected = m_options.FindLowestScoring(scoreFunction);
		if (selected.Value == null || (float)selected.Key == float.MaxValue)
		{
			return -1;
		}
		return m_options.FindIndex_Predicate((EmoteButton x) => x == selected.Value);
	}

	public void Destroy()
	{
		UnityEngine.Object.Destroy(m_gameObject);
	}
}
