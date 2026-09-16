using System.Collections.Generic;
using UnityEngine;

public class RecipeFlowGUI : UIControllerBase
{
	public class RecipeWidgetData
	{
		public RecipeWidgetUIController m_widget;

		public float m_timeLimit;

		public int m_order;

		public VoidGeneric<ElementToken> m_expirationCallback;

		public RecipeWidgetData(RecipeWidgetUIController _widget, float _prop, int _order, float _timeLimit, VoidGeneric<ElementToken> _expirationCallback)
		{
			m_widget = _widget;
			m_order = _order;
			m_timeLimit = _timeLimit;
			m_expirationCallback = _expirationCallback;
		}
	}

	public struct ElementToken
	{
		private RecipeWidgetData m_widget;

		public ElementToken(RecipeWidgetData _widget)
		{
			m_widget = _widget;
		}

		public static bool operator ==(ElementToken _token1, ElementToken _token2)
		{
			return _token1.m_widget == _token2.m_widget;
		}

		public static bool operator !=(ElementToken _token1, ElementToken _token2)
		{
			return _token1.m_widget != _token2.m_widget;
		}
	}

	[SerializeField]
	private float m_distanceBetweenOrders = 5f;

	[SerializeField]
	private float m_distanceFromEndOfScreen = 5f;

	[SerializeField]
	private int m_maxOrdersAllowed = 5;

	[SerializeField]
	private RecipeWidgetUIController m_recipeWidgetPrefab;

	private int m_nextIndex;

	private Camera m_mainCamera;

	private List<RecipeWidgetData> m_widgets = new List<RecipeWidgetData>();

	private List<RecipeWidgetData> m_dyingWidgets = new List<RecipeWidgetData>();

	private bool[] m_occupiedTables;

	private List<RecipeWidgetData> m_ordererWidgets = new List<RecipeWidgetData>();

	public ElementToken AddElement(OrderDefinitionNode _data, float _timeLimit, VoidGeneric<ElementToken> _expirationCallback)
	{
		int tableNumber = ClaimUnoccupiedTable();
		GameObject obj = GameUtils.InstantiateUIController(m_recipeWidgetPrefab.gameObject, base.transform as RectTransform);
		RecipeWidgetUIController recipeWidgetUIController = obj.RequireComponent<RecipeWidgetUIController>();
		recipeWidgetUIController.SetupFromOrderDefinition(_data, tableNumber);
		RecipeWidgetData recipeWidgetData = new RecipeWidgetData(recipeWidgetUIController, 0f, m_nextIndex, _timeLimit, _expirationCallback);
		m_nextIndex++;
		m_widgets.Add(recipeWidgetData);
		return new ElementToken(recipeWidgetData);
	}

	private int ClaimUnoccupiedTable()
	{
		int num = 0;
		for (int i = 0; i < m_occupiedTables.Length; i++)
		{
			if (!m_occupiedTables[i])
			{
				num++;
			}
		}
		int num2 = Random.Range(0, num);
		for (int j = 0; j < m_occupiedTables.Length; j++)
		{
			if (!m_occupiedTables[j])
			{
				if (num2 == 0)
				{
					m_occupiedTables[j] = true;
					return j;
				}
				num2--;
			}
		}
		return -1;
	}

	private void ReleaseTable(int _tableId)
	{
		m_occupiedTables[_tableId] = false;
	}

	private int GetMaxOrderNumber()
	{
		return m_maxOrdersAllowed;
	}

	public bool IsFull()
	{
		int maxOrderNumber = GetMaxOrderNumber();
		return maxOrderNumber <= m_widgets.Count;
	}

	public bool IsEmpty()
	{
		return m_widgets.Count == 0;
	}

	public void AdjustTimeLimitForElement(ElementToken _token, float _newTimeLimit)
	{
		RecipeWidgetData data = GetData(_token);
		data.m_timeLimit = _newTimeLimit;
	}

	public float GetTimePropRemainingForElement(ElementToken _token)
	{
		RecipeWidgetData data = GetData(_token);
		return data.m_widget.GetTimePropRemaining();
	}

	public void ResetElementTimer(ElementToken _token)
	{
		RecipeWidgetData data = GetData(_token);
		data.m_widget.SetTimePropRemaining(1f);
	}

	public void SetElementStuck(ElementToken _token)
	{
		RecipeWidgetData data = GetData(_token);
		data.m_widget.SetIsStuck();
	}

	public RecipeWidgetData GetData(ElementToken _token)
	{
		return m_widgets.Find((RecipeWidgetData obj) => new ElementToken(obj) == _token);
	}

	public void PlayAnimationOnElement(ElementToken _token, WidgetAnimation _animation)
	{
		RecipeWidgetData data = GetData(_token);
		data.m_widget.PlayAnimation(_animation);
	}

	public void RemoveElement(ElementToken _token, WidgetAnimation _deathAnim = null)
	{
		for (int num = m_widgets.Count - 1; num >= 0; num--)
		{
			RecipeWidgetData recipeWidgetData = m_widgets[num];
			if (new ElementToken(recipeWidgetData) == _token)
			{
				if (_deathAnim != null)
				{
					ReleaseTable(recipeWidgetData.m_widget.GetTableNumber());
					recipeWidgetData.m_widget.PlayAnimation(_deathAnim);
					m_dyingWidgets.Add(recipeWidgetData);
				}
				m_widgets.RemoveAt(num);
			}
		}
	}

	public void PlayAnimation(WidgetAnimation _animation)
	{
		for (int i = 0; i < m_widgets.Count; i++)
		{
			m_widgets[i].m_widget.PlayAnimation(_animation);
		}
	}

	private void Awake()
	{
		m_mainCamera = Camera.main;
		m_occupiedTables = new bool[GetMaxOrderNumber()];
	}

	private void OnDestroy()
	{
	}

	public void UpdateTimers(float _dt)
	{
		for (int i = 0; i < m_widgets.Count; i++)
		{
			float timePropRemaining = m_widgets[i].m_widget.GetTimePropRemaining();
			float num = timePropRemaining;
			num = Mathf.Max(num - _dt / m_widgets[i].m_timeLimit, 0f);
			m_widgets[i].m_widget.SetTimePropRemaining(num);
			if (timePropRemaining > 0f && num <= 0f)
			{
				m_widgets[i].m_expirationCallback(new ElementToken(m_widgets[i]));
			}
		}
	}

	private void Update()
	{
		m_dyingWidgets.RemoveAll(delegate(RecipeWidgetData obj)
		{
			if (!obj.m_widget.IsPlayingAnimation())
			{
				Object.Destroy(obj.m_widget.gameObject);
				return true;
			}
			return false;
		});
		LayoutWidgets();
	}

	private void LayoutWidgets()
	{
		FindAllWidgetsOrdered(ref m_ordererWidgets);
		float distanceFromEndOfScreen = m_distanceFromEndOfScreen;
		float distanceBetweenOrders = m_distanceBetweenOrders;
		float num = distanceFromEndOfScreen - distanceBetweenOrders;
		for (int i = 0; i < m_ordererWidgets.Count; i++)
		{
			RecipeWidgetUIController widget = m_ordererWidgets[i].m_widget;
			float width = widget.GetBounds().width;
			RectTransformExtension rectTransformExtension = widget.gameObject.RequireComponent<RectTransformExtension>();
			float num2 = num + distanceBetweenOrders;
			rectTransformExtension.AnchorOffset = new Vector2(0f, 0f);
			rectTransformExtension.PixelOffset = new Vector2(num2, 0f);
			num = num2 + width;
		}
	}

	private void FindAllWidgetsOrdered(ref List<RecipeWidgetData> _widgets)
	{
		_widgets.Clear();
		_widgets.AddRange(m_widgets);
		for (int i = 0; i < m_dyingWidgets.Count; i++)
		{
			RecipeWidgetData recipeWidgetData = m_dyingWidgets[i];
			int num = -1;
			for (int j = 0; j < _widgets.Count; j++)
			{
				if (_widgets[j].m_order > recipeWidgetData.m_order)
				{
					num = j;
					break;
				}
			}
			if (num == -1)
			{
				_widgets.Add(recipeWidgetData);
			}
			else
			{
				_widgets.Insert(num, recipeWidgetData);
			}
		}
	}
}
