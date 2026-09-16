using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCannonCosmeticDecisions : ClientSynchroniserBase
{
	private CannonCosmeticDecisions m_cannonCosmeticDecisions;

	private ClientCannon m_clientCannon;

	private Cannon m_cannon;

	private Animator m_animator;

	private GameObject m_loadedObject;

	private PlayerAnimationDecisions m_playerAnimations;

	private PlayerControls m_playerControls;

	private const string m_occupiedString = "IsOccupied";

	private const string m_fireString = "CannonFire";

	private const string m_targetInUse = "isInUse";

	private int m_targetVisualsActiveCount;

	private Color[] m_colours;

	private MeshRenderer[] m_targetMeshRenderers;

	private Animator m_targetAnimator;

	private ParticleSystem m_targetGlow;

	private float m_defaultGlowAlpha;

	private object m_fuseAudioToken = new object();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cannonCosmeticDecisions = (CannonCosmeticDecisions)synchronisedObject;
		m_clientCannon = base.gameObject.RequireComponent<ClientCannon>();
		m_cannon = base.gameObject.RequireComponent<Cannon>();
		m_animator = m_cannonCosmeticDecisions.m_cannonAnimator;
		m_clientCannon.RegisterOnLoadedCallback(Load);
		m_clientCannon.RegisterOnUnloadedCallback(Unload);
		m_clientCannon.RegisterOnLaunchedCallback(Launch);
		AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
		m_colours = new Color[avatarDirectoryData.Colours.Length];
		for (int i = 0; i < m_colours.Length; i++)
		{
			m_colours[i] = avatarDirectoryData.Colours[i].MaskColour;
		}
		m_targetMeshRenderers = m_cannonCosmeticDecisions.m_targetVisuals.RequestComponentsRecursive<MeshRenderer>();
		m_targetAnimator = m_cannonCosmeticDecisions.m_targetVisuals.RequireComponentRecursive<Animator>();
		m_targetGlow = m_cannonCosmeticDecisions.m_targetVisuals.RequestComponentRecursive<ParticleSystem>();
		m_defaultGlowAlpha = m_targetGlow.main.startColor.color.a;
		SetTargetColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		SetGlowColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		m_cannonCosmeticDecisions.m_fireFX.SetActive(false);
		m_cannonCosmeticDecisions.m_fuseFX.SetActive(false);
		ShowTarget(false);
	}

	public void Load(GameObject _objToLoad)
	{
		m_loadedObject = _objToLoad;
		m_playerAnimations = m_loadedObject.RequestComponent<PlayerAnimationDecisions>();
		if (m_playerAnimations != null)
		{
			m_playerAnimations.SetInCannon(true);
			GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Cannon_Enter, base.gameObject.layer);
		}
		m_playerControls = m_loadedObject.RequestComponent<PlayerControls>();
		if (m_playerControls != null)
		{
			uint uEntityID = EntitySerialisationRegistry.GetEntry(m_playerControls.gameObject).m_Header.m_uEntityID;
			if (ClientUserSystem.m_Users.Count == 1)
			{
				Color color = m_colours[0];
				SetTargetColour(color);
				SetGlowColour(color);
			}
			for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
			{
				if (ClientUserSystem.m_Users._items[i].EntityID == uEntityID)
				{
					Color color2 = m_colours[i];
					SetTargetColour(color2);
					SetGlowColour(color2);
					break;
				}
			}
		}
		m_animator.SetBool("IsOccupied", true);
		m_cannonCosmeticDecisions.m_fireFX.SetActive(false);
		m_cannonCosmeticDecisions.m_fuseFX.SetActive(true);
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Fuse_Ignite, base.gameObject.layer);
		GameUtils.StartAudio(GameLoopingAudioTag.DLC_08_Cannon_Fuse, m_fuseAudioToken, base.gameObject.layer);
		ShowTarget(true);
	}

	public void Unload(GameObject _obj)
	{
		if (m_playerAnimations != null)
		{
			m_playerAnimations.SetInCannon(false);
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Cannon_Enter, base.gameObject.layer);
		GameUtils.StopAudio(GameLoopingAudioTag.DLC_08_Cannon_Fuse, m_fuseAudioToken);
		SetTargetColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		SetGlowColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		m_loadedObject = null;
		m_animator.SetBool("IsOccupied", false);
		m_cannonCosmeticDecisions.m_fuseFX.SetActive(false);
		ShowTarget(false);
	}

	public void Launch(GameObject _obj)
	{
		if (m_playerAnimations != null)
		{
			m_playerAnimations.SetCannonSpeed(m_cannon.m_animation.m_CurveTime * 2f);
			m_playerAnimations.FireCannon();
			m_playerAnimations.SetInCannon(false);
		}
		m_cannonCosmeticDecisions.m_fireFX.SetActive(true);
		m_cannonCosmeticDecisions.m_fuseFX.SetActive(false);
		m_loadedObject = null;
		m_animator.SetTrigger("CannonFire");
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Cannon_Fire, base.gameObject.layer);
		GameUtils.StopAudio(GameLoopingAudioTag.DLC_08_Cannon_Fuse, m_fuseAudioToken);
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Fuse_Death, base.gameObject.layer);
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Cannon_Crowd, base.gameObject.layer);
		SetTargetColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		SetGlowColour(m_cannonCosmeticDecisions.m_defaultTargetColour);
		ShowTarget(false);
	}

	public void OnTrigger(string _triggerMessage)
	{
		if (!string.IsNullOrEmpty(m_cannonCosmeticDecisions.m_aimStartTrigger) && m_cannonCosmeticDecisions.m_aimStartTrigger == _triggerMessage)
		{
			ShowTarget(true);
		}
		if (!string.IsNullOrEmpty(m_cannonCosmeticDecisions.m_aimEndTrigger) && m_cannonCosmeticDecisions.m_aimEndTrigger == _triggerMessage)
		{
			ShowTarget(false);
		}
	}

	private void ShowTarget(bool show)
	{
		if (show)
		{
			m_targetVisualsActiveCount++;
		}
		if (!show && m_targetVisualsActiveCount > 0)
		{
			m_targetVisualsActiveCount--;
		}
		if (m_targetVisualsActiveCount > 0)
		{
			m_targetAnimator.SetBool("isInUse", true);
			SetTargetAlpha(1f);
			SetGlowAlpha(m_defaultGlowAlpha);
		}
		else
		{
			m_targetAnimator.SetBool("isInUse", false);
			SetTargetAlpha(m_cannonCosmeticDecisions.m_disabledTargetAlpha);
			SetGlowAlpha(m_cannonCosmeticDecisions.m_disabledTargetAlpha);
		}
	}

	private void SetTargetColour(Color _colour)
	{
		for (int i = 0; i < m_targetMeshRenderers.Length; i++)
		{
			m_targetMeshRenderers[i].material.SetColor("_Color", _colour);
			if (m_targetMeshRenderers[i].material.HasProperty("_EmissionColor"))
			{
				m_targetMeshRenderers[i].material.SetColor("_EmissionColor", _colour);
			}
		}
	}

	private void SetTargetAlpha(float alpha)
	{
		for (int i = 0; i < m_targetMeshRenderers.Length; i++)
		{
			Color color = m_targetMeshRenderers[i].material.color;
			color.a = alpha;
			m_targetMeshRenderers[i].material.SetColor("_Color", color);
			if (m_targetMeshRenderers[i].material.HasProperty("_EmissionColor"))
			{
				m_targetMeshRenderers[i].material.SetColor("_EmissionColor", color);
			}
		}
	}

	private void SetGlowColour(Color _colour)
	{
		ParticleSystem.MainModule main = m_targetGlow.main;
		float a = main.startColor.color.a;
		main.startColor = new Color(_colour.r, _colour.g, _colour.b, a);
		m_targetGlow.RestartPFX();
	}

	private void SetGlowAlpha(float alpha)
	{
		ParticleSystem.MainModule main = m_targetGlow.main;
		Color color = main.startColor.color;
		color.a = alpha;
		main.startColor = color;
		m_targetGlow.RestartPFX();
	}
}
