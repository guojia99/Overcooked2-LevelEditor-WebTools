using System;
using UnityEngine;
using UnityEngine.UI;

public class ButtonHoverIcon : MonoBehaviour
{
	[SerializeField]
	private Vector3 m_offset;

	[SerializeField]
	private GameObject m_iconPrefab;

	[SerializeField]
	private SemanticIconLookup.Semantic m_semantic = SemanticIconLookup.Semantic.Generic;

	private ControllerIconLookup m_controlIconLookup;

	private GameObject m_promptUIObject;

	private Image m_icon;

	private SemanticIconLookup m_semanticIconLookup;

	private PlayerManager m_playerManager;

	private bool m_visible;

	private HoverIconUIController m_hoverIconController;

	public HoverIconUIController HoverIconController
	{
		get
		{
			return m_hoverIconController;
		}
	}

	public void SetVisibility(bool _vis)
	{
		if (m_visible != _vis)
		{
			m_visible = _vis;
			if (base.enabled && m_hoverIconController != null)
			{
				m_hoverIconController.SetVisibility(_vis);
			}
		}
	}

	protected virtual void OnEnable()
	{
		if (m_visible && m_hoverIconController != null)
		{
			m_hoverIconController.SetVisibility(true);
		}
	}

	protected virtual void OnDisable()
	{
		if (m_visible && m_hoverIconController != null)
		{
			m_hoverIconController.SetVisibility(false);
		}
	}

	protected virtual void Awake()
	{
		m_semanticIconLookup = GameUtils.RequireManager<SemanticIconLookup>();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_promptUIObject = GameUtils.InstantiateHoverIconUIController(m_iconPrefab, base.transform, "HoverIconCanvas", m_offset);
		m_hoverIconController = m_promptUIObject.RequireComponent<HoverIconUIController>();
		GameObject obj = m_promptUIObject.transform.Find("Icon").gameObject;
		m_icon = obj.RequireComponent<Image>();
		m_icon.sprite = m_semanticIconLookup.GetIcon(m_semantic);
		m_hoverIconController.SetVisibility(m_visible);
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(OnRegenerateControls));
	}

	protected virtual void OnDestroy()
	{
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(OnRegenerateControls));
		UnityEngine.Object.Destroy(m_promptUIObject);
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _b, GamepadUser _a)
	{
		RefreshIcon();
	}

	private void OnRegenerateControls()
	{
		RefreshIcon();
	}

	private void RefreshIcon()
	{
		if (m_icon != null)
		{
			m_icon.sprite = m_semanticIconLookup.GetIcon(m_semantic);
		}
	}
}
