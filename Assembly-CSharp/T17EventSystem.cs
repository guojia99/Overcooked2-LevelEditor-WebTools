using InControl;
using UnityEngine;
using UnityEngine.EventSystems;

public class T17EventSystem : EventSystem
{
	private GameObject m_LastRequestedSelectedGameobject;

	private GameObject m_ObjectToSetAtTheEndOfUpdate;

	private GamepadUser m_AssignedGamepadUser;

	private T17StandaloneInputModule m_T17Module;

	private SuppressionController m_suppressionController = new SuppressionController();

	public GamepadUser AssignedGamepadUser
	{
		get
		{
			return m_AssignedGamepadUser;
		}
	}

	public SuppressionController SuppressionController
	{
		get
		{
			return m_suppressionController;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		T17EventSystemsManager.Instance.RegisterEventSystem(this);
		InControlInputModule component = GetComponent<InControlInputModule>();
		if (component != null)
		{
			Object.Destroy(component);
		}
		m_T17Module = GetComponent<T17StandaloneInputModule>();
	}

	public void ResetSystem()
	{
		m_AssignedGamepadUser = null;
		m_suppressionController.Reset();
	}

	protected override void Update()
	{
		if (m_suppressionController.IsSuppressed())
		{
			m_suppressionController.UpdateSuppressors();
			if (!m_suppressionController.IsSuppressed())
			{
				m_T17Module.ClearEventSystemLogicalButtons();
			}
		}
		else
		{
			EventSystem eventSystem = EventSystem.current;
			EventSystem.current = this;
			base.Update();
			EventSystem.current = eventSystem;
		}
	}

	protected override void OnApplicationFocus(bool bHasFocus)
	{
	}

	private void LateUpdate()
	{
		if (m_suppressionController.IsSuppressed())
		{
			return;
		}
		if (m_ObjectToSetAtTheEndOfUpdate != null && base.currentSelectedGameObject != m_ObjectToSetAtTheEndOfUpdate)
		{
			base.SetSelectedGameObject(m_ObjectToSetAtTheEndOfUpdate);
			T17StandaloneInputModule t17StandaloneInputModule = (T17StandaloneInputModule)base.currentInputModule;
			if (t17StandaloneInputModule != null)
			{
				t17StandaloneInputModule.SetLastSelected(m_ObjectToSetAtTheEndOfUpdate);
			}
		}
		if (base.currentSelectedGameObject != null)
		{
			m_LastRequestedSelectedGameobject = base.currentSelectedGameObject;
		}
		m_ObjectToSetAtTheEndOfUpdate = null;
	}

	public void ForceDeselectSelectionObject()
	{
		base.SetSelectedGameObject((GameObject)null);
		m_LastRequestedSelectedGameobject = null;
	}

	public new void SetSelectedGameObject(GameObject target)
	{
		m_LastRequestedSelectedGameobject = target;
		m_ObjectToSetAtTheEndOfUpdate = target;
	}

	public new void SetSelectedGameObject(GameObject selected, BaseEventData pointer)
	{
		base.SetSelectedGameObject(selected, pointer);
	}

	public GameObject GetLastRequestedSelectedGameobject()
	{
		return m_LastRequestedSelectedGameobject;
	}

	public GameObject GetPendingSelectedGameObject()
	{
		return m_ObjectToSetAtTheEndOfUpdate;
	}

	public void SetAssignedGamepadUser(GamepadUser gamepadUser)
	{
		m_AssignedGamepadUser = gamepadUser;
		if (m_AssignedGamepadUser != null)
		{
			m_T17Module.inputActionsPerSecond = 5f;
			m_T17Module.InvertYAxis = true;
			m_T17Module.Initialize();
		}
	}

	public void ResetInputModule()
	{
		if (m_T17Module != null)
		{
			m_T17Module.Initialize();
		}
	}

	public Suppressor Disable(Object _suppressor)
	{
		return m_suppressionController.AddSuppressor(_suppressor);
	}

	public void ReleaseSuppressor(Suppressor _suppressor)
	{
		_suppressor.Release();
		m_suppressionController.UpdateSuppressors();
	}

	public bool IsDisabled()
	{
		if (m_suppressionController != null)
		{
			return m_suppressionController.IsSuppressed();
		}
		return false;
	}
}
