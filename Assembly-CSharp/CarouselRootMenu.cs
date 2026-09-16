using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(T17GridLayoutGroup))]
public class CarouselRootMenu : RootMenu, IBeginDragHandler, IDragHandler, IEndDragHandler, IEventSystemHandler
{
	public delegate void CarouselButtonClickedEvent(CarouselButton button);

	private CarouselButton[] m_carouselButtons;

	private T17GridLayoutGroup m_gridLayout;

	private RectTransform m_rectTransform;

	[SerializeField]
	[Range(0.01f, 0.6f)]
	protected float m_lerpTime = 1f;

	[SerializeField]
	[Range(0f, 1f)]
	private float m_adjustmentDistance = 0.8f;

	protected Coroutine m_buttonLerp;

	protected Coroutine m_deselectMenu;

	protected Coroutine m_dragCentering;

	protected CarouselButton m_currentButton;

	[SerializeField]
	private bool m_canInteractWithMouse = true;

	[SerializeField]
	protected float m_selectAfterDragDelay = 0.25f;

	private Vector2 m_dragPos;

	private bool m_isDragging;

	private PlayerManager m_playerManager;

	public CarouselButton[] Buttons
	{
		get
		{
			return m_carouselButtons;
		}
	}

	public event CarouselButtonClickedEvent CarouselButtonClicked;

	public CarouselButton GetCurrentButton()
	{
		return m_currentButton;
	}

	protected override void Awake()
	{
		base.Awake();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_gridLayout = base.gameObject.RequireComponent<T17GridLayoutGroup>();
		m_rectTransform = base.gameObject.RequireComponent<RectTransform>();
		CarouselButton carouselButton = null;
		m_carouselButtons = GetComponentsInChildren<CarouselButton>();
		for (int i = 0; i < m_carouselButtons.Length; i++)
		{
			CarouselButton curr = m_carouselButtons[i];
			curr.m_rootMenu = this;
			T17Button button = curr.Button as T17Button;
			if (button != null)
			{
				button.onClick.AddListener(delegate
				{
					OnClick(curr);
				});
				T17Button t17Button = button;
				t17Button.OnButtonDeselect = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button.OnButtonDeselect, new T17Button.T17ButtonDelegate(OnDeselected));
				if (m_canInteractWithMouse)
				{
					T17Button t17Button2 = button;
					t17Button2.OnButtonPointerEnter = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button2.OnButtonPointerEnter, (T17Button.T17ButtonDelegate)delegate
					{
						button.interactable = true;
					});
					T17Button t17Button3 = button;
					t17Button3.OnButtonPointerExit = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button3.OnButtonPointerExit, (T17Button.T17ButtonDelegate)delegate
					{
						button.interactable = IsButtonInteractable(curr);
					});
				}
				else
				{
					T17Button t17Button4 = button;
					t17Button4.OnButtonPointerEnter = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button4.OnButtonPointerEnter, (T17Button.T17ButtonDelegate)delegate
					{
						button.interactable = IsButtonInteractable(curr);
					});
					T17Button t17Button5 = button;
					t17Button5.OnButtonPointerExit = (T17Button.T17ButtonDelegate)Delegate.Combine(t17Button5.OnButtonPointerExit, (T17Button.T17ButtonDelegate)delegate
					{
						button.interactable = IsButtonInteractable(curr);
					});
				}
			}
			if (carouselButton != null)
			{
				ConnectHorizontal(carouselButton.Button, curr.Button);
			}
			carouselButton = curr;
		}
	}

	protected override void Start()
	{
		base.Start();
		if (base.CachedEventSystem != null)
		{
			SelectInitialButton();
		}
		else
		{
			m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _p, GamepadUser _n)
	{
		if (!(_n == null))
		{
			m_CachedEventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(_n);
			if (!(m_CachedEventSystem == null))
			{
				m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
				SelectInitialButton();
			}
		}
	}

	private void SetInteractible(CarouselButton button)
	{
		if (m_canInteractWithMouse)
		{
			button.Button.interactable = true;
		}
		else
		{
			button.Button.interactable = IsButtonInteractable(button);
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (base.CachedEventSystem != null)
		{
			CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
			base.CachedEventSystem.SetSelectedGameObject(componentsInChildren[componentsInChildren.Length / 2].Button.gameObject);
		}
		return base.Show(currentGamer, parent, invoker, hideInvoker);
	}

	private void SelectInitialButton()
	{
		CarouselButton initialButton = GetInitialButton();
		if (!(initialButton == null))
		{
			m_buttonLerp = StartCoroutine(LerpToButton(initialButton, true));
			base.CachedEventSystem.SetSelectedGameObject(initialButton.Button.gameObject);
		}
	}

	protected virtual CarouselButton GetInitialButton()
	{
		return (m_carouselButtons.Length <= 0) ? null : m_carouselButtons[m_carouselButtons.Length / 2];
	}

	protected virtual bool IsButtonInteractable(CarouselButton _button)
	{
		if (_button == m_currentButton)
		{
			return true;
		}
		if (base.CachedEventSystem != null)
		{
			T17StandaloneInputModule t17StandaloneInputModule = (T17StandaloneInputModule)base.CachedEventSystem.currentInputModule;
			if (t17StandaloneInputModule != null && t17StandaloneInputModule.WasUsingMouse)
			{
				return true;
			}
		}
		if (base.CachedEventSystem != null && base.CachedEventSystem.currentSelectedGameObject != null && base.CachedEventSystem.currentSelectedGameObject.IsInHierarchyOf(base.gameObject))
		{
			return true;
		}
		return false;
	}

	public void OnBeginDrag(PointerEventData _eventData)
	{
		m_isDragging = true;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, _eventData.position, null, out m_dragPos);
	}

	public void OnDrag(PointerEventData _eventData)
	{
		if (m_buttonLerp != null)
		{
			StopCoroutine(m_buttonLerp);
			m_buttonLerp = null;
		}
		Vector2 localPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, _eventData.position, null, out localPoint);
		RectOffset ourPadding = new RectOffset(Mathf.RoundToInt((float)m_gridLayout.padding.left + (localPoint.x - m_dragPos.x)), m_gridLayout.padding.right, m_gridLayout.padding.top, m_gridLayout.padding.bottom);
		m_gridLayout.ourPadding = ourPadding;
		m_gridLayout.ForceRefresh();
		m_dragPos = localPoint;
		RecenterMenu();
	}

	public void OnEndDrag(PointerEventData _eventData)
	{
		if (m_dragCentering != null)
		{
			StopCoroutine(m_dragCentering);
		}
		m_dragCentering = StartCoroutine(SelectAfterDrag());
		m_isDragging = false;
	}

	private IEnumerator SelectAfterDrag(bool _doInstant = false)
	{
		if (!_doInstant)
		{
			yield return new WaitForSeconds(m_selectAfterDragDelay);
		}
		if (m_buttonLerp != null)
		{
			StopCoroutine(m_buttonLerp);
			m_buttonLerp = null;
		}
		OnButtonSelected(GetButtonInFocusArea());
	}

	protected void OnClick(CarouselButton _carouselButton)
	{
		if (m_isDragging)
		{
			return;
		}
		if (_carouselButton == GetButtonInFocusArea())
		{
			if (this.CarouselButtonClicked != null)
			{
				this.CarouselButtonClicked(_carouselButton);
			}
		}
		else
		{
			HandleButtonSelection(_carouselButton);
		}
	}

	public void OnButtonSelected(CarouselButton _carouselButton)
	{
		if (m_isDragging || !(base.CachedEventSystem != null))
		{
			return;
		}
		T17StandaloneInputModule t17StandaloneInputModule = (T17StandaloneInputModule)base.CachedEventSystem.currentInputModule;
		if (t17StandaloneInputModule != null)
		{
			if (!t17StandaloneInputModule.WasUsingMouse || !Input.GetMouseButtonDown(0))
			{
				HandleButtonSelection(_carouselButton);
			}
			t17StandaloneInputModule.SetLastSelected(_carouselButton.gameObject);
		}
	}

	private void HandleButtonSelection(CarouselButton _carouselButton)
	{
		if (_carouselButton.gameObject.IsInHierarchyOf(m_gridLayout.gameObject))
		{
			if (m_deselectMenu != null)
			{
				StopCoroutine(m_deselectMenu);
				m_deselectMenu = null;
			}
			if (m_buttonLerp != null)
			{
				StopCoroutine(m_buttonLerp);
				m_buttonLerp = null;
			}
			if (m_dragCentering != null)
			{
				StopCoroutine(m_dragCentering);
				m_dragCentering = null;
			}
			CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				SetInteractible(componentsInChildren[i]);
			}
			RecenterMenu();
			if (componentsInChildren.Length > 0)
			{
				ConnectHorizontal(componentsInChildren[componentsInChildren.Length - 1].Button, componentsInChildren[0].Button);
			}
			m_buttonLerp = StartCoroutine(LerpToButton(_carouselButton));
		}
	}

	protected virtual void OnDeselected(T17Button _button)
	{
		if (m_deselectMenu == null)
		{
			m_deselectMenu = StartCoroutine(DeselectMenu());
		}
	}

	public void ShiftFocus(int _offset)
	{
		if (!(base.CachedEventSystem == null))
		{
			CarouselButton buttonInFocusArea = GetButtonInFocusArea();
			CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
			int i;
			for (i = 0; i < componentsInChildren.Length && !(componentsInChildren[i] == buttonInFocusArea); i++)
			{
			}
			Debug.LogError(i + ", " + (i + _offset) % componentsInChildren.Length);
			if (i != componentsInChildren.Length)
			{
				i = (i + _offset) % componentsInChildren.Length;
				base.CachedEventSystem.SetSelectedGameObject(componentsInChildren[i].Button.gameObject);
			}
		}
	}

	public void ShiftLeft()
	{
		ShiftFocus(-1);
	}

	public void ShiftRight()
	{
		ShiftFocus(1);
	}

	protected void MoveFrontToEnd()
	{
		CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
		componentsInChildren[0].transform.SetAsLastSibling();
		RectOffset rectOffset = new RectOffset(m_gridLayout.padding.left, m_gridLayout.padding.right, m_gridLayout.padding.top, m_gridLayout.padding.bottom);
		rectOffset.left += Mathf.RoundToInt(m_gridLayout.cellSize.x + m_gridLayout.spacing.x);
		m_gridLayout.ourPadding = rectOffset;
		m_gridLayout.ForceRefresh();
	}

	protected void MoveEndToFront()
	{
		CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
		componentsInChildren[componentsInChildren.Length - 1].transform.SetAsFirstSibling();
		RectOffset rectOffset = new RectOffset(m_gridLayout.padding.left, m_gridLayout.padding.right, m_gridLayout.padding.top, m_gridLayout.padding.bottom);
		rectOffset.left -= Mathf.RoundToInt(m_gridLayout.cellSize.x + m_gridLayout.spacing.x);
		m_gridLayout.ourPadding = rectOffset;
		m_gridLayout.ForceRefresh();
	}

	protected void ConnectHorizontal(Selectable _left, Selectable _right)
	{
		Navigation navigation = _left.navigation;
		Navigation navigation2 = _right.navigation;
		navigation.mode = Navigation.Mode.Explicit;
		navigation2.mode = Navigation.Mode.Explicit;
		navigation.selectOnRight = _right;
		navigation2.selectOnLeft = _left;
		_left.navigation = navigation;
		_right.navigation = navigation2;
	}

	protected void RecenterMenu()
	{
		float cellWidthWithSpacing = GetCellWidthWithSpacing();
		float num = m_gridLayout.padding.left;
		while (num > cellWidthWithSpacing || num < 0f - cellWidthWithSpacing)
		{
			if (m_gridLayout.padding.left < 0)
			{
				MoveFrontToEnd();
				num += cellWidthWithSpacing;
			}
			else
			{
				MoveEndToFront();
				num -= cellWidthWithSpacing;
			}
		}
	}

	protected CarouselButton GetButtonInFocusArea()
	{
		CarouselButton[] componentsInChildren = GetComponentsInChildren<CarouselButton>();
		float num = float.MaxValue;
		int num2 = -1;
		Camera main = Camera.main;
		Vector3 vector = main.WorldToScreenPoint(base.transform.position);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Vector3 vector2 = main.WorldToScreenPoint(componentsInChildren[i].transform.position);
			float sqrMagnitude = (vector2 - vector).sqrMagnitude;
			if (num2 == -1 || num > sqrMagnitude)
			{
				num2 = i;
				num = sqrMagnitude;
			}
		}
		if (num2 < 0)
		{
			num2 = componentsInChildren.Length / 2;
		}
		return componentsInChildren[num2];
	}

	protected IEnumerator LerpToButton(CarouselButton _carouselButton, bool _doInstant = false)
	{
		if (_carouselButton == null)
		{
			yield break;
		}
		if (!_doInstant)
		{
			yield return new WaitForEndOfFrame();
		}
		m_currentButton = _carouselButton;
		int idx = 0;
		CarouselButton[] buttons = GetComponentsInChildren<CarouselButton>();
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] == _carouselButton)
			{
				idx = i;
			}
			SetInteractible(buttons[i]);
		}
		m_gridLayout.childAlignment = TextAnchor.MiddleLeft;
		float initLeftPadding = m_gridLayout.padding.left;
		RectOffset newPadding = new RectOffset(m_gridLayout.padding.left, m_gridLayout.padding.right, m_gridLayout.padding.top, m_gridLayout.padding.bottom);
		float cellWidth = GetCellWidthWithSpacing();
		float finalLeftPadding = m_rectTransform.rect.width * 0.5f - m_gridLayout.cellSize.x * 0.5f - (float)idx * cellWidth;
		float deltaLeft = 0f;
		float m_progress = 0f;
		if (m_lerpTime > 0f && !_doInstant)
		{
			while (m_progress < 1f)
			{
				m_progress = Mathf.Min(m_progress + Time.deltaTime / m_lerpTime, 1f);
				deltaLeft += Mathf.Lerp(initLeftPadding, finalLeftPadding, m_progress) - (float)newPadding.left;
				if (initLeftPadding < finalLeftPadding)
				{
					while (deltaLeft > cellWidth * m_adjustmentDistance)
					{
						MoveEndToFront();
						deltaLeft -= cellWidth;
						initLeftPadding -= cellWidth;
						finalLeftPadding -= cellWidth;
					}
				}
				else
				{
					while (deltaLeft < (0f - cellWidth) * m_adjustmentDistance)
					{
						MoveFrontToEnd();
						deltaLeft += cellWidth;
						initLeftPadding += cellWidth;
						finalLeftPadding += cellWidth;
					}
				}
				newPadding.left = Mathf.RoundToInt(Mathf.Lerp(initLeftPadding, finalLeftPadding, m_progress));
				m_gridLayout.ourPadding = newPadding;
				m_gridLayout.ForceRefresh();
				yield return null;
			}
		}
		else
		{
			newPadding.left = Mathf.RoundToInt(finalLeftPadding);
			m_gridLayout.ourPadding = newPadding;
			m_gridLayout.ForceRefresh();
		}
		OnButtonFocusChanged(_carouselButton);
		m_buttonLerp = null;
		RecenterMenu();
	}

	protected virtual void OnButtonFocusChanged(CarouselButton _button)
	{
	}

	protected IEnumerator DeselectMenu()
	{
		yield return new WaitForEndOfFrame();
		CarouselButton[] buttons = GetComponentsInChildren<CarouselButton>();
		foreach (CarouselButton carouselButton in buttons)
		{
			if (carouselButton == m_currentButton)
			{
				carouselButton.Button.interactable = true;
			}
			else
			{
				carouselButton.Button.interactable = IsButtonInteractable(carouselButton);
			}
		}
		m_deselectMenu = null;
	}

	protected float GetCellWidthWithSpacing()
	{
		return m_gridLayout.cellSize.x + m_gridLayout.spacing.x;
	}

	protected void DisallowButton(CarouselButton _button)
	{
		m_carouselButtons = m_carouselButtons.AllRemoved_Predicate((CarouselButton x) => x == _button);
		if (_button != null)
		{
			_button.gameObject.SetActive(false);
		}
		CarouselButton carouselButton = null;
		for (int num = 0; num < m_carouselButtons.Length; num++)
		{
			CarouselButton carouselButton2 = m_carouselButtons[num];
			if (carouselButton != null)
			{
				ConnectHorizontal(carouselButton.Button, carouselButton2.Button);
			}
			carouselButton = carouselButton2;
		}
	}
}
