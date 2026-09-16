using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/Toggle", 32)]
public class T17Toggle : Toggle, IT17EventHelper
{
	[SerializeField]
	private GameOneShotAudioTag m_selectAudioTag = GameOneShotAudioTag.UIHighlight;

	[SerializeField]
	private GameOneShotAudioTag m_submitAudioTag = GameOneShotAudioTag.UISelect;

	public bool m_bPlaySound = true;

	private T17EventSystem m_EventSystem;

	private int m_audioLayer;

	protected override void Start()
	{
		base.Start();
		m_audioLayer = LayerMask.NameToLayer("Administration");
	}

	public void SetEventSystem(T17EventSystem gamersEventSystem)
	{
		m_EventSystem = gamersEventSystem;
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			if (IsInteractable() && base.navigation.mode != Navigation.Mode.None)
			{
				m_EventSystem.SetSelectedGameObject(base.gameObject, eventData);
			}
			base.OnPointerDown(eventData);
		}
	}

	public override void Select()
	{
		if (!m_EventSystem.alreadySelecting)
		{
			m_EventSystem.SetSelectedGameObject(base.gameObject);
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

	public override void OnSelect(BaseEventData eventData)
	{
		base.OnSelect(eventData);
		if (m_bPlaySound)
		{
			GameUtils.TriggerAudio(m_selectAudioTag, m_audioLayer);
		}
	}

	public override void OnPointerClick(PointerEventData eventData)
	{
		if (IsActive() && IsInteractable())
		{
			if (m_bPlaySound)
			{
				GameUtils.TriggerAudio(m_submitAudioTag, m_audioLayer);
			}
			base.OnPointerClick(eventData);
		}
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		if (m_bPlaySound)
		{
			GameUtils.TriggerAudio(m_selectAudioTag, m_audioLayer);
		}
	}

	public override void OnSubmit(BaseEventData eventData)
	{
		if (IsActive() && IsInteractable())
		{
			if (m_bPlaySound)
			{
				GameUtils.TriggerAudio(m_submitAudioTag, m_audioLayer);
			}
			base.OnSubmit(eventData);
		}
	}
}
