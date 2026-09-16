using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
public class PlayerRespawnBehaviour : MonoBehaviour
{
	[Serializable]
	public struct RespawnParams
	{
		public enum Type
		{
			Fade = 0,
			Scale = 1,
			None = 2
		}

		[SerializeField]
		public RespawnCollider.RespawnType m_deathBy;

		[SerializeField]
		public Type m_type;

		[SerializeField]
		public float m_delay;

		[SerializeField]
		public float m_duration;

		public RespawnParams(RespawnCollider.RespawnType _deathBy, Type _type, float _delay, float _duration)
		{
			m_deathBy = _deathBy;
			m_type = _type;
			m_delay = _delay;
			m_duration = _duration;
		}
	}

	[SerializeField]
	public float m_respawnTime = 5f;

	[SerializeField]
	public float m_switchDelay = 0.5f;

	[SerializeField]
	public float m_particleTime = 1f;

	[SerializeField]
	public float m_maxDrowingVerticalVelocity = -0.1f;

	[SerializeField]
	public GameObject m_spawnEffect;

	[SerializeField]
	public GameObject m_fallingEffect;

	[SerializeField]
	public GameObject m_respawnCounterPrefab;

	[SerializeField]
	public Shader m_fadeShader;

	[SerializeField]
	public GameOneShotAudioTag m_fallAudioTag = GameOneShotAudioTag.PlayerFall;

	[SerializeField]
	public GameOneShotAudioTag m_drownAudioTag = GameOneShotAudioTag.PlayerDive;

	[SerializeField]
	public GameOneShotAudioTag m_carHitAudioTag = GameOneShotAudioTag.PlayerSlip;

	[SerializeField]
	public List<RespawnParams> m_respawnParams = new List<RespawnParams>
	{
		new RespawnParams(RespawnCollider.RespawnType.Hit, RespawnParams.Type.Fade, 1f, 2f),
		new RespawnParams(RespawnCollider.RespawnType.Car, RespawnParams.Type.Fade, 1f, 2f)
	};

	public PlayerControls m_playerControls;

	public WindAccumulator m_windAccumulator;

	public Vector3 m_startLocation;

	public Transform m_startParent;

	private GroundCast m_groundCast;

	private bool m_initialGroundFound;

	private void Awake()
	{
		m_playerControls = base.gameObject.RequireComponent<PlayerControls>();
		m_windAccumulator = base.gameObject.RequestComponent<WindAccumulator>();
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (!GameUtils.GetLevelConfig().m_disableDynamicParenting)
		{
			m_groundCast = base.gameObject.RequireComponent<GroundCast>();
			if (m_groundCast != null)
			{
				m_groundCast.RegisterGroundChangedCallback(OnGroundChange);
			}
		}
	}

	protected void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
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
		if (!m_initialGroundFound)
		{
			m_startParent = base.transform.parent;
			m_startLocation = base.transform.localPosition;
		}
	}

	private void OnGroundChange(Collider collider)
	{
		if (m_initialGroundFound || !m_groundCast.HasGroundContact())
		{
			return;
		}
		Collider groundCollider = m_groundCast.GetGroundCollider();
		if (!(groundCollider != null))
		{
			return;
		}
		IParentable parentable = groundCollider.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
		if (parentable != null)
		{
			Transform attachPoint = parentable.GetAttachPoint(base.gameObject);
			if (attachPoint != null)
			{
				m_startParent = attachPoint;
				m_startLocation = m_startParent.InverseTransformPoint(base.transform.position);
				m_initialGroundFound = true;
			}
		}
	}
}
