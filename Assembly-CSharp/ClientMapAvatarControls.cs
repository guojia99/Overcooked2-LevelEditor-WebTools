using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMapAvatarControls : ClientSynchroniserBase
{
	private bool m_bStarted;

	private MapAvatarControls m_avatarControls;

	private MapAvatarGroundCast m_groundCast;

	private Vector3 m_initialPosition;

	private Transform m_Transform;

	private ILogicalButton[] m_hornButtons;

	private MapAvatarTransformer m_avatarTransformer;

	private IClientMapSelectable m_currentSelectable;

	private uint m_currentSelectableEntityID;

	private static readonly int m_iSpeed = Animator.StringToHash("Speed");

	protected void Awake()
	{
		if (m_avatarControls == null)
		{
			m_avatarControls = GetComponent<MapAvatarControls>();
		}
		m_groundCast = base.gameObject.RequireComponent<MapAvatarGroundCast>();
		m_avatarTransformer = base.gameObject.RequireComponent<MapAvatarTransformer>();
		m_initialPosition = base.transform.position;
		m_Transform = base.transform;
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		m_avatarControls.SetOriginalVanRotation(m_avatarControls.m_vanMesh.transform.rotation);
	}

	public void Update()
	{
		if (m_bStarted && m_avatarControls != null && m_avatarControls.enabled)
		{
			Update_Horn();
			Update_Movement();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void Update_Horn()
	{
		if (TimeManager.IsPaused(base.gameObject) || m_hornButtons == null)
		{
			return;
		}
		for (int i = 0; i < m_hornButtons.Length; i++)
		{
			if (m_hornButtons[i] != null && m_hornButtons[i].JustPressed())
			{
				ClientMessenger.MapAvatarHorn(i);
			}
		}
	}

	private void Update_Movement()
	{
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
		{
			m_avatarControls.UpdateMovement();
		}
		m_avatarControls.m_animator.SetFloat(m_iSpeed, m_avatarControls.GetSpeed());
		m_avatarControls.OrientateVan(TimeManager.GetDeltaTime(base.gameObject), m_groundCast.GetGroundNormal());
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_avatarControls = (MapAvatarControls)synchronisedObject;
		m_hornButtons = BuildHornButtons();
	}

	public override void ApplyServerUpdate(Serialisable serialisable)
	{
		HandleMapAvatarMessage(serialisable);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		HandleMapAvatarMessage(serialisable);
	}

	private void HandleMapAvatarMessage(Serialisable serialisable)
	{
		MapAvatarControlsMessage mapAvatarControlsMessage = (MapAvatarControlsMessage)serialisable;
		if (mapAvatarControlsMessage.m_bHorns[0])
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.VanHorn, base.gameObject.layer);
		}
		if (mapAvatarControlsMessage.m_bHorns[1])
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.VanHorn02, base.gameObject.layer);
		}
		if (mapAvatarControlsMessage.m_bHorns[2])
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.VanHorn03, base.gameObject.layer);
		}
		if (mapAvatarControlsMessage.m_bHorns[3])
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.VanHorn04, base.gameObject.layer);
		}
		if (mapAvatarControlsMessage.m_bDash)
		{
			m_avatarControls.DashStarted();
			switch (m_avatarTransformer.CurrentType)
			{
			case MapAvatarTransformer.VanType.LAND:
				GameUtils.TriggerAudio(m_avatarControls.m_dashTags.m_landTag, base.gameObject.layer);
				break;
			case MapAvatarTransformer.VanType.WATER:
				GameUtils.TriggerAudio(m_avatarControls.m_dashTags.m_waterTag, base.gameObject.layer);
				break;
			case MapAvatarTransformer.VanType.FLYING:
				GameUtils.TriggerAudio(m_avatarControls.m_dashTags.m_flyingTag, base.gameObject.layer);
				break;
			}
		}
		if (m_currentSelectableEntityID == mapAvatarControlsMessage.CurrentSelectableEntityId)
		{
			return;
		}
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(mapAvatarControlsMessage.CurrentSelectableEntityId);
		if ((mapAvatarControlsMessage.CurrentSelectableEntityId == 0 || entry != m_currentSelectable) && m_currentSelectable != null)
		{
			m_currentSelectable.AvatarLeavingSelectable(m_avatarControls);
			m_currentSelectable = null;
		}
		if (entry != null)
		{
			IClientMapSelectable clientMapSelectable = entry.m_GameObject.RequestInterfaceRecursive<IClientMapSelectable>();
			if (clientMapSelectable != null)
			{
				m_currentSelectable = clientMapSelectable;
				clientMapSelectable.AvatarEnteringSelectable(m_avatarControls);
			}
		}
		m_currentSelectableEntityID = mapAvatarControlsMessage.CurrentSelectableEntityId;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.WorldMapVanControls;
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		GameState state = gameStateMessage.m_State;
		if (state != GameState.RunMapUnfoldRoutine)
		{
			return;
		}
		ParticleSystem[] array = base.gameObject.RequestComponentsRecursive<ParticleSystem>();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].isPlaying)
			{
				array[i].RestartPFX();
			}
		}
		m_bStarted = true;
	}

	private ILogicalButton[] BuildHornButtons()
	{
		ILogicalButton[] array = new ILogicalButton[4];
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			ILogicalButton logicalButton;
			if (i < ClientUserSystem.m_Users.Count && ClientUserSystem.m_Users._items[i].IsLocal)
			{
				logicalButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.Horn, (PlayerInputLookup.Player)num);
				num++;
			}
			else
			{
				logicalButton = new ComboLogicalButton(new ILogicalButton[0]);
			}
			array[i] = logicalButton;
		}
		return array;
	}
}
