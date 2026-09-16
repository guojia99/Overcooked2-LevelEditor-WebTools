using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlayerRespawnBehaviour : ClientSynchroniserBase
{
	private PlayerRespawnBehaviour m_PlayerRespawnBehaviour;

	private GameObject m_spawnEffect;

	private ParticleSystem m_spawnEffectParticleSystem;

	private GameObject m_fallEffectInstance;

	private ClientChefSynchroniser m_clientChefSynchroniser;

	private GameObject m_hoverIcon;

	private HoverIconUIController m_hoverIconUiController;

	private RespawnCounterUIController m_respawnUiController;

	private Suppressor m_controlsSuppressor;

	private bool m_isRespawning;

	private static readonly string k_activeLayer = "Players";

	private static readonly string k_respawnLayer = "PlayersRespawn";

	private int m_respawnLayerMask;

	private int m_activeLayerMask;

	private IEnumerator m_currentRespawn;

	private static int m_iDead = Animator.StringToHash("Dead");

	public bool IsRespawning
	{
		get
		{
			return m_isRespawning;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_PlayerRespawnBehaviour = (PlayerRespawnBehaviour)synchronisedObject;
		m_spawnEffect = m_PlayerRespawnBehaviour.m_spawnEffect.InstantiateOnParent(m_PlayerRespawnBehaviour.m_startParent);
		m_spawnEffectParticleSystem = m_spawnEffect.RequestComponent<ParticleSystem>();
		m_spawnEffectParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		m_clientChefSynchroniser = base.gameObject.RequestComponent<ClientChefSynchroniser>();
		m_respawnLayerMask = LayerMask.NameToLayer(k_respawnLayer);
		m_activeLayerMask = LayerMask.NameToLayer(k_activeLayer);
		m_hoverIcon = GameUtils.InstantiateHoverIconUIController(m_PlayerRespawnBehaviour.m_respawnCounterPrefab, m_PlayerRespawnBehaviour.m_startParent, "HoverIconCanvas", m_PlayerRespawnBehaviour.m_startLocation);
		m_hoverIconUiController = m_hoverIcon.RequireComponent<HoverIconUIController>();
		m_respawnUiController = m_hoverIcon.RequireComponent<RespawnCounterUIController>();
		m_hoverIcon.gameObject.SetActive(false);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_hoverIcon != null)
		{
			Object.Destroy(m_hoverIcon.gameObject);
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnBehaviour;
	}

	public override void UpdateSynchronising()
	{
		if (m_currentRespawn != null)
		{
			if (!m_currentRespawn.MoveNext() || !MultiplayerController.IsSynchronisationActive())
			{
				m_currentRespawn = null;
			}
			else
			{
				m_spawnEffect.transform.position = base.gameObject.transform.position;
			}
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		RespawnMessage respawnMessage = (RespawnMessage)serialisable;
		if (respawnMessage.m_Phase == RespawnMessage.Phase.Begin && m_currentRespawn == null)
		{
			m_currentRespawn = BeginRespawn(respawnMessage.m_RespawnType);
			m_currentRespawn.MoveNext();
		}
	}

	private IEnumerator BeginRespawn(RespawnCollider.RespawnType respawnType)
	{
		if (m_isRespawning)
		{
			yield break;
		}
		m_isRespawning = true;
		base.gameObject.RequireComponent<Collider>().enabled = false;
		RigidbodyMotion motion = null;
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			motion = m_PlayerRespawnBehaviour.m_playerControls.Motion;
			motion.SetVelocity(Vector3.zero);
		}
		WindAccumulator windAccumulator = base.gameObject.RequireComponent<WindAccumulator>();
		windAccumulator.Reset();
		m_PlayerRespawnBehaviour.m_playerControls.m_bRespawning = true;
		ParticleSystem pfx = base.gameObject.RequestComponentRecursive<ParticleSystem>();
		if (pfx != null)
		{
			pfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
		Animator animator = base.gameObject.RequireComponentRecursive<Animator>();
		animator.SetBool(m_iDead, true);
		PlayerIDProvider idProvider = base.gameObject.RequireComponent<PlayerIDProvider>();
		GameUtils.TriggerNXRumble(idProvider.GetID(), GameOneShotAudioTag.Boom);
		switch (respawnType)
		{
		case RespawnCollider.RespawnType.FallDeath:
			m_fallEffectInstance = m_PlayerRespawnBehaviour.m_fallingEffect.InstantiateOnParent(m_PlayerRespawnBehaviour.m_startParent);
			m_fallEffectInstance.transform.position = base.transform.position;
			GameUtils.TriggerAudio(m_PlayerRespawnBehaviour.m_fallAudioTag, base.gameObject.layer);
			break;
		case RespawnCollider.RespawnType.Drowning:
			GameUtils.TriggerAudio(m_PlayerRespawnBehaviour.m_drownAudioTag, base.gameObject.layer);
			break;
		case RespawnCollider.RespawnType.Car:
			m_PlayerRespawnBehaviour.m_playerControls.m_bApplyGravity = false;
			m_PlayerRespawnBehaviour.m_playerControls.Motion.SetKinematic(true);
			GameUtils.TriggerAudio(m_PlayerRespawnBehaviour.m_carHitAudioTag, base.gameObject.layer);
			break;
		}
		animator.SetTrigger(respawnType.ToDescription());
		StartCoroutine(RunSwitchToNextDelayed(m_PlayerRespawnBehaviour.m_switchDelay));
		IEnumerator parallel = CoroutineUtils.ParallelRoutine(new IEnumerator[2]
		{
			RunHoverCounter(),
			RunAvatarDestroy(respawnType)
		});
		while (parallel.MoveNext())
		{
			if ((ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession()) && respawnType == RespawnCollider.RespawnType.Drowning)
			{
				Vector3 velocity = motion.GetVelocity();
				velocity.y = Mathf.Max(m_PlayerRespawnBehaviour.m_maxDrowingVerticalVelocity, velocity.y);
				motion.SetVelocity(velocity);
			}
			yield return null;
		}
		if (m_PlayerRespawnBehaviour.m_windAccumulator != null)
		{
			m_PlayerRespawnBehaviour.m_windAccumulator.enabled = false;
		}
		IEnumerator spawn = RunSpawnAtSpawnPoint();
		while (spawn.MoveNext())
		{
			yield return null;
		}
		m_isRespawning = false;
		m_PlayerRespawnBehaviour.m_playerControls.m_bRespawning = false;
		if (pfx != null)
		{
			pfx.Play();
		}
		m_PlayerRespawnBehaviour.m_playerControls.m_bApplyGravity = true;
		if (idProvider.IsLocallyControlled() || ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_PlayerRespawnBehaviour.m_playerControls.Motion.SetKinematic(false);
		}
	}

	private IEnumerator RunSwitchToNextDelayed(float seconds)
	{
		IEnumerator wait = CoroutineUtils.TimerRoutine(seconds, base.gameObject.layer);
		while (wait.MoveNext())
		{
			yield return null;
		}
		PlayerInputLookup.Player player = m_PlayerRespawnBehaviour.m_playerControls.PlayerIDProvider.GetID();
		PlayerSwitchingManager switchingManager = GameUtils.RequireManager<PlayerSwitchingManager>();
		if (switchingManager.SelectedAvatar(player) == m_PlayerRespawnBehaviour.m_playerControls)
		{
			switchingManager.ForceSwitchToNext(player);
		}
	}

	public void MoveRespawnPoint(Vector3 _startLocation, Transform _startParent = null)
	{
		m_PlayerRespawnBehaviour.m_startLocation = _startLocation;
		m_PlayerRespawnBehaviour.m_startParent = _startParent;
		if (m_hoverIcon != null)
		{
			RespawnCounterUIController respawnCounterUIController = m_hoverIcon.RequireComponent<RespawnCounterUIController>();
			respawnCounterUIController.SetFollowTransform(m_PlayerRespawnBehaviour.m_startParent, m_PlayerRespawnBehaviour.m_startLocation);
		}
	}

	private IEnumerator RunHoverCounter()
	{
		m_hoverIcon.gameObject.SetActive(true);
		m_hoverIconUiController.SetFollowTransform(m_PlayerRespawnBehaviour.m_startParent, m_PlayerRespawnBehaviour.m_startLocation);
		m_respawnUiController.SetTarget(m_PlayerRespawnBehaviour.gameObject);
		m_respawnUiController.SetCountdown(m_PlayerRespawnBehaviour.m_respawnTime);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(base.gameObject);
		uint uEntityID = entry.m_Header.m_uEntityID;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (uEntityID == user.EntityID || uEntityID == user.Entity2ID)
			{
				m_respawnUiController.SetTeam(user.Team);
				m_respawnUiController.SetPlayerNum(i);
			}
		}
		IEnumerator wait = CoroutineUtils.TimerRoutine(m_PlayerRespawnBehaviour.m_respawnTime, base.gameObject.layer);
		while (wait.MoveNext())
		{
			yield return null;
		}
		m_hoverIcon.gameObject.SetActive(false);
	}

	private IEnumerator RunAvatarDestroy(RespawnCollider.RespawnType respawnType)
	{
		PauseMovement();
		int idx = m_PlayerRespawnBehaviour.m_respawnParams.FindIndex((PlayerRespawnBehaviour.RespawnParams x) => x.m_deathBy == respawnType);
		if (idx != -1)
		{
			PlayerRespawnBehaviour.RespawnParams respawnParams = m_PlayerRespawnBehaviour.m_respawnParams[idx];
			float totalRespawnEffectDuration = respawnParams.m_delay + respawnParams.m_duration;
			if (respawnParams.m_type == PlayerRespawnBehaviour.RespawnParams.Type.Fade)
			{
				IEnumerator wait = CoroutineUtils.TimerRoutine(respawnParams.m_delay, base.gameObject.layer);
				while (wait.MoveNext())
				{
					yield return null;
				}
				SkinnedMeshRenderer[] allRenderers = base.gameObject.RequestComponentsRecursive<SkinnedMeshRenderer>();
				Material[] allMaterials = allRenderers.ConvertAll((SkinnedMeshRenderer x) => x.material);
				for (int num = 0; num < allRenderers.Length; num++)
				{
					Material material = new Material(allRenderers[num].material);
					material.shader = m_PlayerRespawnBehaviour.m_fadeShader;
					allRenderers[num].material = material;
				}
				float prop = 0f;
				while (prop < 1f)
				{
					for (int num2 = 0; num2 < allRenderers.Length; num2++)
					{
						SkinnedMeshRenderer skinnedMeshRenderer = allRenderers[num2];
						if (null != skinnedMeshRenderer && null != skinnedMeshRenderer.material && skinnedMeshRenderer.material.HasProperty("_Color"))
						{
							Color color = allRenderers[num2].material.color;
							color.a = 1f - prop;
							allRenderers[num2].material.color = color;
						}
					}
					prop += TimeManager.GetDeltaTime(base.gameObject) / respawnParams.m_duration;
					yield return null;
				}
				if (m_clientChefSynchroniser != null && m_PlayerRespawnBehaviour.m_playerControls.ControlScheme != null)
				{
					m_clientChefSynchroniser.Pause();
				}
				DeactivateAndMove();
				for (int num3 = 0; num3 < allRenderers.Length; num3++)
				{
					SkinnedMeshRenderer skinnedMeshRenderer2 = allRenderers[num3];
					if (null != skinnedMeshRenderer2)
					{
						skinnedMeshRenderer2.material = allMaterials[num3];
						if (null != skinnedMeshRenderer2.material && skinnedMeshRenderer2.material.HasProperty("_Color"))
						{
							Color color2 = skinnedMeshRenderer2.material.color;
							color2.a = 1f;
							skinnedMeshRenderer2.material.color = color2;
						}
					}
				}
			}
			else if (respawnParams.m_type == PlayerRespawnBehaviour.RespawnParams.Type.Scale)
			{
				IEnumerator wait2 = CoroutineUtils.TimerRoutine(respawnParams.m_delay, base.gameObject.layer);
				while (wait2.MoveNext())
				{
					yield return null;
				}
				float progress = respawnParams.m_duration;
				wait2 = CoroutineUtils.TimerRoutine(respawnParams.m_duration, base.gameObject.layer);
				while (wait2.MoveNext())
				{
					progress = Mathf.Clamp01(progress - TimeManager.GetDeltaTime(base.gameObject.layer) / respawnParams.m_duration);
					base.transform.localScale = new Vector3(progress, progress, progress);
					yield return null;
				}
				wait2 = CoroutineUtils.TimerRoutine(m_PlayerRespawnBehaviour.m_respawnTime - totalRespawnEffectDuration, base.gameObject.layer);
				while (wait2.MoveNext())
				{
					yield return null;
				}
				if (m_clientChefSynchroniser != null && m_PlayerRespawnBehaviour.m_playerControls.ControlScheme != null)
				{
					m_clientChefSynchroniser.Pause();
				}
				DeactivateAndMove();
			}
			else if (respawnParams.m_type == PlayerRespawnBehaviour.RespawnParams.Type.None)
			{
				IEnumerator wait3 = CoroutineUtils.TimerRoutine(m_PlayerRespawnBehaviour.m_respawnTime, base.gameObject.layer);
				while (wait3.MoveNext())
				{
					yield return null;
				}
				if (m_clientChefSynchroniser != null && m_PlayerRespawnBehaviour.m_playerControls.ControlScheme != null)
				{
					m_clientChefSynchroniser.Pause();
				}
				DeactivateAndMove();
			}
		}
		else
		{
			IEnumerator wait4 = CoroutineUtils.TimerRoutine(m_PlayerRespawnBehaviour.m_respawnTime, base.gameObject.layer);
			while (wait4.MoveNext())
			{
				yield return null;
			}
			if (m_clientChefSynchroniser != null && m_PlayerRespawnBehaviour.m_playerControls.ControlScheme != null)
			{
				m_clientChefSynchroniser.Pause();
			}
			DeactivateAndMove();
		}
		if (m_fallEffectInstance != null)
		{
			Object.Destroy(m_fallEffectInstance);
		}
	}

	private void DeactivateAndMove()
	{
		SetRendererActive(false);
		SetCharacterCollisionsEnabled(false);
		base.transform.SetParent(m_PlayerRespawnBehaviour.m_startParent, false);
		base.transform.localPosition = m_PlayerRespawnBehaviour.m_startLocation;
	}

	public IEnumerator RunSpawnAtSpawnPoint()
	{
		base.transform.SetParent(m_PlayerRespawnBehaviour.m_startParent, false);
		base.transform.localPosition = m_PlayerRespawnBehaviour.m_startLocation;
		base.transform.localScale = Vector3.one;
		base.gameObject.RequireComponent<Collider>().enabled = true;
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_PlayerRespawnBehaviour.m_playerControls.Motion.SetVelocity(new Vector3(0f, 0f, 0f));
		}
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = base.gameObject.RequireComponent<ServerWorldObjectSynchroniser>();
			if (serverWorldObjectSynchroniser != null)
			{
				serverWorldObjectSynchroniser.ResumeAllClients();
			}
		}
		if (m_clientChefSynchroniser != null)
		{
			while (!m_clientChefSynchroniser.IsReadyToResume())
			{
				yield return null;
			}
			m_clientChefSynchroniser.Resume();
		}
		if (m_spawnEffectParticleSystem != null)
		{
			m_spawnEffect.transform.SetParent(m_PlayerRespawnBehaviour.m_startParent);
			m_spawnEffect.transform.localPosition = m_PlayerRespawnBehaviour.m_startLocation;
			m_spawnEffectParticleSystem.Play();
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.PlayerSpawn, base.gameObject.layer);
		Animator animator = base.gameObject.RequireComponentRecursive<Animator>();
		animator.SetBool(m_iDead, false);
		IEnumerator wait = CoroutineUtils.TimerRoutine(m_PlayerRespawnBehaviour.m_particleTime, base.gameObject.layer);
		while (wait.MoveNext())
		{
			yield return null;
		}
		if (m_spawnEffectParticleSystem != null)
		{
			m_spawnEffectParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
		if (m_PlayerRespawnBehaviour.m_windAccumulator != null)
		{
			m_PlayerRespawnBehaviour.m_windAccumulator.enabled = true;
		}
		SetRendererActive(true);
		SetCharacterCollisionsEnabled(true);
		ResumeMovement();
	}

	private void SetRendererActive(bool bActive)
	{
		Animator componentInChildren = base.gameObject.GetComponentInChildren<Animator>(true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(bActive);
		}
	}

	private void SetCharacterCollisionsEnabled(bool bEnabled)
	{
		base.gameObject.SetObjectLayer((!bEnabled) ? m_respawnLayerMask : m_activeLayerMask);
	}

	private void PauseMovement()
	{
		if (m_PlayerRespawnBehaviour.m_playerControls.PlayerIDProvider.IsLocallyControlled())
		{
			m_controlsSuppressor = m_PlayerRespawnBehaviour.m_playerControls.Suppress(this);
		}
	}

	private void ResumeMovement()
	{
		if (m_PlayerRespawnBehaviour.m_playerControls.PlayerIDProvider.IsLocallyControlled())
		{
			m_controlsSuppressor.Release();
			m_controlsSuppressor = null;
		}
	}
}
