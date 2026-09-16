using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTerminalCosmeticDecisions : ClientSynchroniserBase, ITriggerReceiver
{
	private TerminalCosmeticDecisions m_decisions;

	private HoverIconUIController m_moveUI;

	private bool m_uiVisible = true;

	private ClientTerminal m_clientTerminal;

	private Interactable m_interactable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_decisions = (TerminalCosmeticDecisions)synchronisedObject;
		m_clientTerminal = base.gameObject.RequireComponent<ClientTerminal>();
		m_interactable = base.gameObject.RequireComponent<Interactable>();
		GameObject source = m_decisions.MoveIconPrefab.gameObject;
		GameObject obj = GameUtils.InstantiateHoverIconUIController(source, base.transform, "HoverIconCanvas", m_decisions.Iconoffset);
		m_moveUI = obj.RequireComponent<HoverIconUIController>();
		SetIconVisible(false);
	}

	protected void Update()
	{
		if ((bool)m_decisions)
		{
			SetIconVisible(m_clientTerminal.HasSession);
			if (m_clientTerminal.HasSession)
			{
				SetMaterialOnBits(m_decisions.InUseMaterial);
			}
			else if (m_interactable.enabled)
			{
				SetMaterialOnBits(m_decisions.ActiveMaterial);
			}
			else
			{
				SetMaterialOnBits(m_decisions.DisabledMaterial);
			}
			Vector2 cosmeticJoystickInput = m_clientTerminal.CosmeticJoystickInput;
			Vector3 euler = m_decisions.JoystickMaxAngle * new Vector3(cosmeticJoystickInput.y, 0f, 0f - cosmeticJoystickInput.x);
			m_decisions.ShaftTransform.rotation = Quaternion.Euler(euler);
			bool flag = m_clientTerminal.GetTerminal().m_pilotableObject.HasMoved();
			if (!m_decisions.IsPlaying && flag)
			{
				GameUtils.StartAudio(m_decisions.Moving, this, base.gameObject.layer);
				m_decisions.IsPlaying = true;
			}
			else if (m_decisions.IsPlaying && !flag)
			{
				GameUtils.StopAudio(m_decisions.Moving, this);
				m_decisions.IsPlaying = false;
			}
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_decisions.IsPlaying)
		{
			GameUtils.StopAudio(m_decisions.Moving, this);
			m_decisions.IsPlaying = false;
		}
	}

	private void SetIconVisible(bool _visible)
	{
		if (m_uiVisible != _visible)
		{
			m_uiVisible = _visible;
			m_moveUI.SetVisibility(_visible);
		}
	}

	private void SetMaterialOnBits(Material _material)
	{
		for (int i = 0; i < m_decisions.ColouredMeshes.Length; i++)
		{
			m_decisions.ColouredMeshes[i].material = _material;
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_interactable.m_onInteractStartedTrigger)
		{
			GameUtils.TriggerAudio(m_decisions.StartMoving, base.gameObject.layer);
		}
		else if (_trigger == m_interactable.m_onInteractEndedTrigger)
		{
			GameUtils.TriggerAudio(m_decisions.StopMoving, base.gameObject.layer);
		}
	}
}
