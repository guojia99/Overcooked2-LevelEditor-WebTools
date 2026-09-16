using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlayerSlipBehaviour : ClientSynchroniserBase, ITriggerReceiver
{
	private PlayerSlipBehaviour m_slipBehaviour;

	private static int m_iSlipped = Animator.StringToHash("Slipped");

	private PlayerIDProvider m_playerIDProvider;

	private Animator m_animator;

	private Transform m_attachPointSlip;

	private Transform m_attachPointImpact;

	private ParticleSystem m_slipParticles;

	private ParticleSystem m_impactParticles;

	private ParticleSystem m_streakParticles;

	public override EntityType GetEntityType()
	{
		return EntityType.PlayerSlip;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_slipBehaviour = (PlayerSlipBehaviour)synchronisedObject;
	}

	private void Start()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected override void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		base.OnDestroy();
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			Initialise();
		}
	}

	private void Initialise()
	{
		m_animator = base.gameObject.RequireComponentInImmediateChildren<Animator>();
		m_attachPointSlip = base.gameObject.transform.FindChildRecursive("PART_Slip_Locator");
		m_attachPointImpact = base.gameObject.transform.FindChildRecursive("Head");
		m_playerIDProvider = base.gameObject.RequireComponent<PlayerIDProvider>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PlayerSlipMessage playerSlipMessage = (PlayerSlipMessage)serialisable;
		switch (playerSlipMessage.m_msgType)
		{
		case PlayerSlipMessage.MsgType.Slip:
			m_animator.SetBool(m_iSlipped, true);
			m_slipParticles = SpawnParticleEffect(m_slipBehaviour.m_pfxReferences.m_slipEffect, m_attachPointSlip);
			m_streakParticles = SpawnParticleEffect(m_slipBehaviour.m_pfxReferences.m_streakEffect, m_attachPointSlip);
			GameUtils.TriggerAudio(GameOneShotAudioTag.PlayerSlip, base.gameObject.layer);
			if (m_playerIDProvider != null)
			{
				GameUtils.TriggerNXRumble(m_playerIDProvider.GetID(), GameOneShotAudioTag.Boom);
			}
			break;
		case PlayerSlipMessage.MsgType.Stand:
			m_animator.SetBool(m_iSlipped, false);
			break;
		case PlayerSlipMessage.MsgType.Finished:
			m_animator.SetBool(m_iSlipped, false);
			if (m_slipParticles != null)
			{
				m_slipParticles.Stop(true);
			}
			if (m_streakParticles != null)
			{
				m_streakParticles.Stop(true);
			}
			if (m_impactParticles != null)
			{
				m_impactParticles.Stop(true);
			}
			break;
		}
	}

	private ParticleSystem SpawnParticleEffect(GameObject _prefab, Transform _parent)
	{
		if (_prefab != null)
		{
			GameObject obj = _prefab.InstantiateOnParent(_parent, false);
			ParticleSystem result = obj.RequireComponentRecursive<ParticleSystem>();
			m_impactParticles.Play(true);
			return result;
		}
		return null;
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_slipBehaviour.m_impactTrigger)
		{
			m_impactParticles = SpawnParticleEffect(m_slipBehaviour.m_pfxReferences.m_impactEffect, m_attachPointImpact);
		}
	}
}
