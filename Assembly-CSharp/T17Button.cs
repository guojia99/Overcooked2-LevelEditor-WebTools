using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/T17BUtton", 30)]
public class T17Button : Button, IT17EventHelper
{
	public delegate void T17ButtonDelegate(T17Button sender);

	public delegate void T17ButtonMoveDelegate(Selectable from, Selectable to, MoveDirection direction);

	private Text m_ButtonText;

	public T17ButtonDelegate OnButtonSelect;

	public T17ButtonDelegate OnButtonDeselect;

	public T17ButtonMoveDelegate OnButtonMove;

	public T17ButtonDelegate OnButtonPointerEnter;

	public T17ButtonDelegate OnButtonPointerExit;

	public bool m_bPlaySound = true;

	private int m_audioLayer;

	public bool m_bShowTooltip;

	public bool m_bLocalizeTooltip = true;

	public string m_TooltipTag = "Text.UI.ButtonTooltip";

	private T17EventSystem m_EventSystem;

	private GamepadUser m_GamepadUser;

	public T17Image m_lockImage;

	[SerializeField]
	private GameOneShotAudioTag m_SelectAudioTag = GameOneShotAudioTag.UIHighlight;

	[SerializeField]
	private GameOneShotAudioTag m_SubmitAudioTag = GameOneShotAudioTag.UISelect;

	public GameOneShotAudioTag SelectAudioTag
	{
		get
		{
			return m_SelectAudioTag;
		}
		set
		{
			m_SelectAudioTag = value;
		}
	}

	public GameOneShotAudioTag SubmitAudioTag
	{
		get
		{
			return m_SubmitAudioTag;
		}
		set
		{
			m_SubmitAudioTag = value;
		}
	}

	public override Selectable FindSelectableOnDown()
	{
		Selectable selectable = base.FindSelectableOnDown();
		if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()))
		{
			return selectable.FindSelectableOnDown();
		}
		return selectable;
	}

	public override Selectable FindSelectableOnLeft()
	{
		Selectable selectable = base.FindSelectableOnLeft();
		if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()))
		{
			return selectable.FindSelectableOnLeft();
		}
		return selectable;
	}

	public override Selectable FindSelectableOnRight()
	{
		Selectable selectable = base.FindSelectableOnRight();
		if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()))
		{
			return selectable.FindSelectableOnRight();
		}
		return selectable;
	}

	public override Selectable FindSelectableOnUp()
	{
		Selectable selectable = base.FindSelectableOnUp();
		if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable()))
		{
			return selectable.FindSelectableOnUp();
		}
		return selectable;
	}

	protected override void Start()
	{
		base.Start();
		m_ButtonText = GetComponentInChildren<Text>(true);
		m_audioLayer = LayerMask.NameToLayer("Administration");
	}

	public void SetText(string text)
	{
		if (m_ButtonText != null)
		{
			m_ButtonText.text = text;
		}
	}

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		if (OnButtonSelect != null)
		{
			OnButtonSelect(this);
		}
		if (m_bPlaySound && !(eventData is PointerEventData))
		{
			GameUtils.TriggerAudio(m_SelectAudioTag, m_audioLayer);
		}
		if (m_bShowTooltip && T17TooltipManager.Instance != null)
		{
			T17TooltipManager.Instance.Show(m_TooltipTag, !m_bLocalizeTooltip);
		}
	}

	public override void OnSubmit(BaseEventData eventData)
	{
		base.OnSubmit(eventData);
		if (m_bPlaySound)
		{
			GameUtils.TriggerAudio(m_SubmitAudioTag, m_audioLayer);
		}
	}

	public override void OnDeselect(BaseEventData eventData)
	{
		base.OnDeselect(eventData);
		if (OnButtonDeselect != null)
		{
			OnButtonDeselect(this);
		}
		if (m_bShowTooltip && T17TooltipManager.Instance != null)
		{
			T17TooltipManager.Instance.Show(string.Empty, true);
		}
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		base.OnPointerDown(eventData);
		if (m_bPlaySound)
		{
			GameUtils.TriggerAudio(m_SubmitAudioTag, m_audioLayer);
		}
	}

	public override void Select()
	{
		if (IsThereAnEventSystem() && !m_EventSystem.alreadySelecting)
		{
			m_EventSystem.SetSelectedGameObject(base.gameObject);
		}
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		if (OnButtonPointerEnter != null)
		{
			OnButtonPointerEnter(this);
		}
		if (m_bPlaySound)
		{
			GameUtils.TriggerAudio(m_SelectAudioTag, m_audioLayer);
		}
		if (m_bShowTooltip && T17TooltipManager.Instance != null)
		{
			T17TooltipManager.Instance.Show(m_TooltipTag, !m_bLocalizeTooltip);
		}
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);
		if (OnButtonPointerExit != null)
		{
			OnButtonPointerExit(this);
		}
		if (m_bShowTooltip && T17TooltipManager.Instance != null)
		{
			T17TooltipManager.Instance.Show(string.Empty, true);
		}
	}

	public override void OnMove(AxisEventData eventData)
	{
		base.OnMove(eventData);
		if (OnButtonMove != null)
		{
			Selectable to = null;
			switch (eventData.moveDir)
			{
			case MoveDirection.Up:
				to = base.navigation.selectOnUp;
				break;
			case MoveDirection.Down:
				to = base.navigation.selectOnDown;
				break;
			case MoveDirection.Left:
				to = base.navigation.selectOnLeft;
				break;
			case MoveDirection.Right:
				to = base.navigation.selectOnRight;
				break;
			}
			OnButtonMove(this, to, eventData.moveDir);
		}
	}

	public T17EventSystem GetDomain()
	{
		return m_EventSystem;
	}

	public GameObject GetGameobject()
	{
		return base.gameObject;
	}

	public bool IsThereAnEventSystem()
	{
		if (m_EventSystem != null)
		{
			return true;
		}
		return false;
	}

	public void SetEventSystem(T17EventSystem gamersEventSystem = null)
	{
		m_EventSystem = gamersEventSystem;
	}
}
