using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[AddComponentMenu("Event/T17 Standalone Input Module")]
public class T17StandaloneInputModule : PointerInputModule
{
	private bool m_bReadyForUse;

	private IPlayerManager m_IPlayerManager;

	private ILogicalValue m_HorizontalAxis;

	private ILogicalValue m_VerticalAxis;

	private ILogicalButton m_UpButton;

	private ILogicalButton m_DownButton;

	private ILogicalButton m_LeftButton;

	private ILogicalButton m_RightButton;

	private ILogicalButton m_SelectButton;

	private ILogicalButton m_CancelButton;

	private bool isTouchSupported;

	private GameObject m_LastSelectedGameObject;

	private GameObject m_LastMousedGameObject;

	private GameObject m_objWeSentPressEventTo;

	private float m_lastScreenWidth;

	private float m_lastScreenHeight;

	private bool m_wasFullscreen;

	private float m_CurrentMouseDelta;

	private bool m_bButtonPressed;

	private bool m_wasUsingMouse;

	private bool m_transitioningFromMouse;

	[SerializeField]
	[Tooltip("Makes an axis press always move only one UI selection. Enable if you do not want to allow scrolling through UI elements by holding an axis direction.")]
	private bool moveOneElementPerAxisPress;

	protected bool m_bProcessMouseEvents = true;

	private float m_PrevActionTime;

	private Vector2 m_LastMoveVector;

	private int m_ConsecutiveMoveCount;

	private Vector2 m_LastMousePosition;

	private Vector2 m_MousePosition;

	[SerializeField]
	[Tooltip("Makes an axis press always move only one UI selection. Enable if you do not want to allow scrolling through UI elements by holding an axis direction.")]
	private bool invertYAxis;

	[SerializeField]
	[Tooltip("Number of selection changes allowed per second when a movement button/axis is held in a direction.")]
	private float m_InputActionsPerSecond = 10f;

	[SerializeField]
	[Tooltip("Delay in seconds before vertical/horizontal movement starts repeating continouously when a movement direction is held.")]
	private float m_RepeatDelay;

	[SerializeField]
	[Tooltip("Allows the mouse to be used to select elements.")]
	private bool m_allowMouseInput = true;

	[SerializeField]
	[Tooltip("Allows the mouse to be used to select elements if the device also supports touch control.")]
	private bool m_allowMouseInputIfTouchSupported = true;

	[SerializeField]
	[FormerlySerializedAs("m_AllowActivationOnMobileDevice")]
	[Tooltip("Forces the module to always be active.")]
	private bool m_ForceModuleActive;

	public bool WasUsingMouse
	{
		get
		{
			return m_wasUsingMouse;
		}
	}

	public bool MoveOneElementPerAxisPress
	{
		get
		{
			return moveOneElementPerAxisPress;
		}
		set
		{
			moveOneElementPerAxisPress = value;
		}
	}

	public bool allowMouseInput
	{
		get
		{
			return m_allowMouseInput;
		}
		set
		{
			m_allowMouseInput = value;
		}
	}

	public bool allowMouseInputIfTouchSupported
	{
		get
		{
			return m_allowMouseInputIfTouchSupported;
		}
		set
		{
			m_allowMouseInputIfTouchSupported = value;
		}
	}

	protected bool isMouseSupported
	{
		get
		{
			if (!m_allowMouseInput)
			{
				return false;
			}
			return !isTouchSupported || m_allowMouseInputIfTouchSupported;
		}
	}

	public bool forceModuleActive
	{
		get
		{
			return m_ForceModuleActive;
		}
		set
		{
			m_ForceModuleActive = value;
		}
	}

	public float inputActionsPerSecond
	{
		get
		{
			return m_InputActionsPerSecond;
		}
		set
		{
			m_InputActionsPerSecond = value;
		}
	}

	public float repeatDelay
	{
		get
		{
			return m_RepeatDelay;
		}
		set
		{
			m_RepeatDelay = value;
		}
	}

	public bool InvertYAxis
	{
		get
		{
			return invertYAxis;
		}
		set
		{
			invertYAxis = value;
		}
	}

	protected T17StandaloneInputModule()
	{
	}

	protected override void Awake()
	{
		base.Awake();
		isTouchSupported = Input.touchSupported;
		m_lastScreenWidth = Screen.width;
		m_lastScreenHeight = Screen.height;
		m_wasFullscreen = Screen.fullScreen;
	}

	public override void UpdateModule()
	{
		if (m_bReadyForUse && isMouseSupported)
		{
			m_LastMousePosition = m_MousePosition;
			m_MousePosition = Input.mousePosition;
		}
	}

	public override bool IsModuleSupported()
	{
		return true;
	}

	public override bool ShouldActivateModule()
	{
		if (!base.ShouldActivateModule() || !m_bReadyForUse)
		{
			return false;
		}
		bool flag = m_ForceModuleActive;
		flag |= m_SelectButton.IsDown();
		flag |= m_CancelButton.IsDown();
		if (moveOneElementPerAxisPress)
		{
			flag |= m_UpButton.JustPressed() || m_DownButton.JustPressed();
			flag |= m_LeftButton.JustPressed() || m_RightButton.JustPressed();
		}
		else
		{
			flag |= !Mathf.Approximately(m_HorizontalAxis.GetValue(), 0f);
			flag |= !Mathf.Approximately(m_VerticalAxis.GetValue(), 0f);
		}
		flag |= IsMouseActive();
		if (isTouchSupported)
		{
			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch touch = Input.GetTouch(i);
				flag |= touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
			}
		}
		return flag;
	}

	protected bool IsMouseActive()
	{
		bool flag = false;
		if (isMouseSupported)
		{
			flag |= (m_MousePosition - m_LastMousePosition).sqrMagnitude > 0f;
			flag |= Input.GetMouseButtonDown(0);
		}
		return flag;
	}

	public override void ActivateModule()
	{
		base.ActivateModule();
		if (isMouseSupported)
		{
			m_LastMousePosition = (m_MousePosition = Input.mousePosition);
		}
		GameObject gameObject = base.eventSystem.currentSelectedGameObject;
		if (gameObject == null)
		{
			gameObject = base.eventSystem.firstSelectedGameObject;
		}
		base.eventSystem.SetSelectedGameObject(gameObject, GetBaseEventData());
	}

	public override void DeactivateModule()
	{
		base.DeactivateModule();
		ClearSelection();
	}

	public override void Process()
	{
		if (!m_bReadyForUse)
		{
			return;
		}
		bool flag = SendUpdateEventToSelectedObject();
		if (base.eventSystem.sendNavigationEvents)
		{
			if (!flag)
			{
				flag |= SendMoveEventToSelectedObject();
			}
			if (!flag)
			{
				SendSubmitEventToSelectedObject();
			}
		}
		if (!ProcessTouchEvents() && isMouseSupported && m_bProcessMouseEvents)
		{
			ProcessMouseEvent();
		}
		UpdateMouseKeyboardFocus();
	}

	private bool ProcessTouchEvents()
	{
		if (!isTouchSupported)
		{
			return false;
		}
		for (int i = 0; i < Input.touchCount; i++)
		{
			Touch touch = Input.GetTouch(i);
			if (touch.type != TouchType.Indirect)
			{
				bool pressed;
				bool released;
				PointerEventData touchPointerEventData = GetTouchPointerEventData(touch, out pressed, out released);
				ProcessTouchPress(touchPointerEventData, pressed, released);
				if (!released)
				{
					ProcessMove(touchPointerEventData);
					ProcessDrag(touchPointerEventData);
				}
				else
				{
					RemovePointerData(touchPointerEventData);
				}
			}
		}
		return Input.touchCount > 0;
	}

	private void ProcessTouchPress(PointerEventData pointerEvent, bool pressed, bool released)
	{
		GameObject gameObject = pointerEvent.pointerCurrentRaycast.gameObject;
		if (pressed)
		{
			pointerEvent.eligibleForClick = true;
			pointerEvent.delta = Vector2.zero;
			pointerEvent.dragging = false;
			pointerEvent.useDragThreshold = true;
			pointerEvent.pressPosition = pointerEvent.position;
			pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;
			DeselectIfSelectionChanged(gameObject, pointerEvent);
			if (pointerEvent.pointerEnter != gameObject)
			{
				HandlePointerExitAndEnter(pointerEvent, gameObject);
				pointerEvent.pointerEnter = gameObject;
			}
			GameObject gameObject2 = ExecuteEvents.ExecuteHierarchy(gameObject, pointerEvent, ExecuteEvents.pointerDownHandler);
			if (gameObject2 == null)
			{
				gameObject2 = ExecuteEvents.GetEventHandler<IPointerClickHandler>(gameObject);
			}
			float unscaledTime = Time.unscaledTime;
			if (gameObject2 == pointerEvent.lastPress)
			{
				float num = unscaledTime - pointerEvent.clickTime;
				if (num < 0.3f)
				{
					pointerEvent.clickCount++;
				}
				else
				{
					pointerEvent.clickCount = 1;
				}
				pointerEvent.clickTime = unscaledTime;
			}
			else
			{
				pointerEvent.clickCount = 1;
			}
			pointerEvent.pointerPress = gameObject2;
			pointerEvent.rawPointerPress = gameObject;
			pointerEvent.clickTime = unscaledTime;
			pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(gameObject);
			if (pointerEvent.pointerDrag != null)
			{
				ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
			}
		}
		if (released)
		{
			ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
			GameObject eventHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(gameObject);
			if (pointerEvent.pointerPress == eventHandler && pointerEvent.eligibleForClick)
			{
				ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
			}
			else if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
			{
				ExecuteEvents.ExecuteHierarchy(gameObject, pointerEvent, ExecuteEvents.dropHandler);
			}
			pointerEvent.eligibleForClick = false;
			pointerEvent.pointerPress = null;
			pointerEvent.rawPointerPress = null;
			if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
			{
				ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
			}
			pointerEvent.dragging = false;
			pointerEvent.pointerDrag = null;
			if (pointerEvent.pointerDrag != null)
			{
				ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
			}
			pointerEvent.pointerDrag = null;
			ExecuteEvents.ExecuteHierarchy(pointerEvent.pointerEnter, pointerEvent, ExecuteEvents.pointerExitHandler);
			pointerEvent.pointerEnter = null;
		}
	}

	protected bool SendSubmitEventToSelectedObject()
	{
		if (base.eventSystem.currentSelectedGameObject == null || !m_bReadyForUse)
		{
			m_SelectButton.ClaimPressEvent();
			m_SelectButton.ClaimReleaseEvent();
			m_CancelButton.ClaimPressEvent();
			m_CancelButton.ClaimReleaseEvent();
			return false;
		}
		BaseEventData baseEventData = GetBaseEventData();
		if (m_SelectButton.JustPressed())
		{
			ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.submitHandler);
		}
		if (m_CancelButton.JustPressed())
		{
			ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.cancelHandler);
		}
		return baseEventData.used;
	}

	private Vector2 GetRawMoveVector()
	{
		if (!m_bReadyForUse)
		{
			return Vector2.zero;
		}
		Vector2 zero = Vector2.zero;
		bool flag = false;
		bool flag2 = false;
		if (moveOneElementPerAxisPress)
		{
			float num = 0f;
			if (m_RightButton.JustPressed())
			{
				num = 1f;
			}
			else if (m_LeftButton.JustPressed())
			{
				num = -1f;
			}
			float num2 = 0f;
			if (m_UpButton.JustPressed())
			{
				num2 = 1f;
			}
			else if (m_DownButton.JustPressed())
			{
				num2 = -1f;
			}
			zero.x += num;
			zero.y += num2;
		}
		else
		{
			zero.x += m_HorizontalAxis.GetValue();
			zero.y += m_VerticalAxis.GetValue() * (float)((!invertYAxis) ? 1 : (-1));
		}
		bool flag3 = m_UpButton.JustPressed();
		bool flag4 = m_DownButton.JustPressed();
		bool flag5 = m_LeftButton.JustPressed();
		bool flag6 = m_RightButton.JustPressed();
		flag2 = flag2 || flag3 || flag4;
		if (flag || flag5 || flag6)
		{
			if (zero.x < 0f)
			{
				zero.x = -1f;
			}
			else if (zero.x > 0f)
			{
				zero.x = 1f;
			}
			else
			{
				zero.x = ((!flag6) ? (-1f) : 1f);
			}
		}
		if (flag2)
		{
			if (zero.y < 0f)
			{
				zero.y = -1f;
			}
			else if (zero.y > 0f)
			{
				zero.y = 1f;
			}
			else
			{
				zero.y = ((!flag3) ? (-1f) : 1f);
			}
		}
		return zero;
	}

	protected bool SendMoveEventToSelectedObject()
	{
		if (!m_bReadyForUse)
		{
			return false;
		}
		float unscaledTime = Time.unscaledTime;
		m_bButtonPressed = false;
		Vector2 rawMoveVector = GetRawMoveVector();
		if (Mathf.Approximately(rawMoveVector.x, 0f) && Mathf.Approximately(rawMoveVector.y, 0f))
		{
			m_ConsecutiveMoveCount = 0;
			m_transitioningFromMouse = false;
			return false;
		}
		m_bButtonPressed = true;
		if (m_wasUsingMouse || m_transitioningFromMouse)
		{
			ClearEventSystemLogicalButtons();
			return false;
		}
		bool flag = Vector2.Dot(rawMoveVector, m_LastMoveVector) > 0f;
		bool flag2 = CheckButtonOrKeyMovement(unscaledTime);
		bool flag3 = flag2;
		if (!flag3)
		{
			flag3 = ((!(m_RepeatDelay > 0f)) ? (unscaledTime > m_PrevActionTime + 1f / m_InputActionsPerSecond) : ((!flag || m_ConsecutiveMoveCount != 1) ? (unscaledTime > m_PrevActionTime + 1f / m_InputActionsPerSecond) : (unscaledTime > m_PrevActionTime + m_RepeatDelay)));
		}
		if (!flag3)
		{
			return false;
		}
		AxisEventData axisEventData = GetAxisEventData(rawMoveVector.x, rawMoveVector.y, 0.6f);
		if (axisEventData.moveDir == MoveDirection.None)
		{
			return false;
		}
		ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, axisEventData, ExecuteEvents.moveHandler);
		if (!flag)
		{
			m_ConsecutiveMoveCount = 0;
		}
		m_ConsecutiveMoveCount++;
		m_PrevActionTime = unscaledTime;
		m_LastMoveVector = rawMoveVector;
		return axisEventData.used;
	}

	private bool CheckButtonOrKeyMovement(float time)
	{
		if (!m_bReadyForUse)
		{
			return false;
		}
		bool flag = false;
		flag |= m_UpButton.JustPressed() || m_DownButton.JustPressed();
		return flag | (m_LeftButton.JustPressed() || m_RightButton.JustPressed());
	}

	protected void ProcessMouseEvent()
	{
		ProcessMouseEvent(0);
	}

	protected void ProcessMouseEvent(int id)
	{
		MouseState mousePointerEventData = GetMousePointerEventData();
		MouseButtonEventData eventData = mousePointerEventData.GetButtonState(PointerEventData.InputButton.Left).eventData;
		float num = Screen.width;
		float num2 = Screen.height;
		bool fullScreen = Screen.fullScreen;
		if (m_lastScreenWidth != num || m_lastScreenHeight != num2 || m_wasFullscreen != fullScreen)
		{
			m_lastScreenWidth = num;
			m_lastScreenHeight = num2;
			m_wasFullscreen = fullScreen;
			eventData.buttonData.delta = Vector2.zero;
			m_CurrentMouseDelta = 0f;
		}
		else
		{
			m_CurrentMouseDelta = eventData.buttonData.delta.magnitude;
		}
		ProcessMousePress(eventData);
		if (m_CurrentMouseDelta != 0f)
		{
			ProcessMove(eventData.buttonData);
			m_LastMousedGameObject = eventData.buttonData.pointerCurrentRaycast.gameObject;
			m_wasUsingMouse = true;
		}
		else
		{
			GameObject gameObject = null;
			GameObject gameObject2 = null;
			if (m_wasUsingMouse)
			{
				gameObject = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(eventData.buttonData.pointerEnter);
				gameObject2 = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(eventData.buttonData.pointerCurrentRaycast.gameObject);
			}
			if (!m_wasUsingMouse || (m_wasUsingMouse && gameObject != gameObject2))
			{
				ExecuteEvents.ExecuteHierarchy(eventData.buttonData.pointerEnter, eventData.buttonData, ExecuteEvents.pointerExitHandler);
			}
		}
		ProcessDrag(eventData.buttonData);
		ProcessMousePress(mousePointerEventData.GetButtonState(PointerEventData.InputButton.Right).eventData);
		ProcessDrag(mousePointerEventData.GetButtonState(PointerEventData.InputButton.Right).eventData.buttonData);
		ProcessMousePress(mousePointerEventData.GetButtonState(PointerEventData.InputButton.Middle).eventData);
		ProcessDrag(mousePointerEventData.GetButtonState(PointerEventData.InputButton.Middle).eventData.buttonData);
		if (!Mathf.Approximately(eventData.buttonData.scrollDelta.sqrMagnitude, 0f))
		{
			GameObject eventHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(eventData.buttonData.pointerCurrentRaycast.gameObject);
			ExecuteEvents.ExecuteHierarchy(eventHandler, eventData.buttonData, ExecuteEvents.scrollHandler);
		}
	}

	protected bool SendUpdateEventToSelectedObject()
	{
		if (base.eventSystem.currentSelectedGameObject == null || !m_bReadyForUse)
		{
			return false;
		}
		BaseEventData baseEventData = GetBaseEventData();
		ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.updateSelectedHandler);
		return baseEventData.used;
	}

	protected void ProcessMousePress(MouseButtonEventData data)
	{
		PointerEventData buttonData = data.buttonData;
		GameObject eventHandler = buttonData.pointerCurrentRaycast.gameObject;
		if (data.PressedThisFrame())
		{
			buttonData.eligibleForClick = true;
			buttonData.delta = Vector2.zero;
			buttonData.dragging = false;
			buttonData.useDragThreshold = true;
			buttonData.pressPosition = buttonData.position;
			buttonData.pointerPressRaycast = buttonData.pointerCurrentRaycast;
			DeselectIfSelectionChanged(eventHandler, buttonData);
			GameObject gameObject = ExecuteEvents.ExecuteHierarchy(eventHandler, buttonData, ExecuteEvents.pointerDownHandler);
			if (gameObject == null)
			{
				gameObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(eventHandler);
			}
			m_objWeSentPressEventTo = gameObject;
			float unscaledTime = Time.unscaledTime;
			if (gameObject == buttonData.lastPress)
			{
				float num = unscaledTime - buttonData.clickTime;
				if (num < 0.3f)
				{
					buttonData.clickCount++;
				}
				else
				{
					buttonData.clickCount = 1;
				}
				buttonData.clickTime = unscaledTime;
			}
			else
			{
				buttonData.clickCount = 1;
			}
			buttonData.pointerPress = gameObject;
			buttonData.rawPointerPress = eventHandler;
			buttonData.clickTime = unscaledTime;
			buttonData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(eventHandler);
			if (buttonData.pointerDrag != null)
			{
				ExecuteEvents.Execute(buttonData.pointerDrag, buttonData, ExecuteEvents.initializePotentialDrag);
			}
		}
		if (!data.ReleasedThisFrame())
		{
			return;
		}
		if (buttonData.pointerPress != null)
		{
			ExecuteEvents.Execute(buttonData.pointerPress, buttonData, ExecuteEvents.pointerUpHandler);
		}
		else
		{
			ProcessMove(data.buttonData);
			eventHandler = ExecuteEvents.GetEventHandler<ISelectHandler>(data.buttonData.pointerCurrentRaycast.gameObject);
			if (eventHandler != ExecuteEvents.GetEventHandler<ISelectHandler>(m_objWeSentPressEventTo))
			{
				ExecuteEvents.ExecuteHierarchy(m_objWeSentPressEventTo, buttonData, ExecuteEvents.pointerUpHandler);
			}
			if (eventHandler != base.eventSystem.currentSelectedGameObject)
			{
				base.eventSystem.SetSelectedGameObject(null);
			}
			if (eventHandler != null)
			{
				SetLastSelected(eventHandler);
			}
			m_wasUsingMouse = true;
			buttonData = data.buttonData;
		}
		GameObject eventHandler2 = ExecuteEvents.GetEventHandler<IPointerClickHandler>(eventHandler);
		if (buttonData.pointerPress == eventHandler2 && buttonData.eligibleForClick)
		{
			ExecuteEvents.Execute(buttonData.pointerPress, buttonData, ExecuteEvents.pointerClickHandler);
		}
		else if (buttonData.pointerDrag != null && buttonData.dragging)
		{
			ExecuteEvents.ExecuteHierarchy(eventHandler, buttonData, ExecuteEvents.dropHandler);
		}
		buttonData.eligibleForClick = false;
		buttonData.pointerPress = null;
		buttonData.rawPointerPress = null;
		if (buttonData.pointerDrag != null && buttonData.dragging)
		{
			ExecuteEvents.Execute(buttonData.pointerDrag, buttonData, ExecuteEvents.endDragHandler);
		}
		buttonData.dragging = false;
		buttonData.pointerDrag = null;
		if (eventHandler != buttonData.pointerEnter)
		{
			HandlePointerExitAndEnter(buttonData, null);
			HandlePointerExitAndEnter(buttonData, eventHandler);
		}
	}

	public void Initialize()
	{
		m_HorizontalAxis = PlayerInputLookup.GetUIValue(PlayerInputLookup.LogicalValueID.MovementX);
		m_VerticalAxis = PlayerInputLookup.GetUIValue(PlayerInputLookup.LogicalValueID.MovementY);
		m_UpButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIUp);
		m_DownButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIDown);
		m_LeftButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UILeft);
		m_RightButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIRight);
		m_SelectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart);
		m_CancelButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		m_bReadyForUse = true;
	}

	private void UpdateMouseKeyboardFocus()
	{
		if (base.eventSystem == null)
		{
			return;
		}
		T17EventSystem t17EventSystem = (T17EventSystem)base.eventSystem;
		if (t17EventSystem == null)
		{
			return;
		}
		if (m_CurrentMouseDelta != 0f && (!m_wasUsingMouse || m_LastMousedGameObject != base.eventSystem.currentSelectedGameObject))
		{
			base.eventSystem.SetSelectedGameObject(null);
			if (m_LastMousedGameObject != null)
			{
				Selectable selectable = m_LastMousedGameObject.GetComponent<Selectable>();
				if (selectable == null)
				{
					selectable = m_LastMousedGameObject.GetComponentInParent<Selectable>();
				}
				if (selectable != null)
				{
					m_LastSelectedGameObject = selectable.gameObject;
				}
			}
		}
		if (m_bButtonPressed && m_wasUsingMouse)
		{
			GameObject gameObject = null;
			gameObject = ((!(t17EventSystem.currentSelectedGameObject == null)) ? base.eventSystem.currentSelectedGameObject : m_LastSelectedGameObject);
			m_wasUsingMouse = false;
			ClearSelection();
			base.eventSystem.SetSelectedGameObject(gameObject);
			m_transitioningFromMouse = true;
		}
	}

	public void SetLastSelected(GameObject _lastSelected)
	{
		m_LastMousedGameObject = _lastSelected;
		m_LastSelectedGameObject = _lastSelected;
	}

	protected void OnApplicationFocus(bool focus)
	{
		m_bProcessMouseEvents = focus;
		base.eventSystem.sendNavigationEvents = focus;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		OnApplicationFocus(Application.isFocused);
	}

	public void ClearEventSystemLogicalButtons()
	{
		m_UpButton.ClaimPressEvent();
		m_UpButton.ClaimReleaseEvent();
		m_DownButton.ClaimPressEvent();
		m_DownButton.ClaimReleaseEvent();
		m_LeftButton.ClaimPressEvent();
		m_LeftButton.ClaimReleaseEvent();
		m_RightButton.ClaimPressEvent();
		m_RightButton.ClaimReleaseEvent();
		m_SelectButton.ClaimPressEvent();
		m_SelectButton.ClaimReleaseEvent();
		m_CancelButton.ClaimPressEvent();
		m_CancelButton.ClaimReleaseEvent();
	}
}
