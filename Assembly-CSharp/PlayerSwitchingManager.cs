using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(CampaignKitchenLoaderManager))]
[ExecutionDependency(typeof(CompetitiveKitchenLoaderManager))]
public class PlayerSwitchingManager : Manager
{
	public class PlayerComparer : IEqualityComparer<PlayerInputLookup.Player>
	{
		public bool Equals(PlayerInputLookup.Player x, PlayerInputLookup.Player y)
		{
			return x == y;
		}

		public int GetHashCode(PlayerInputLookup.Player obj)
		{
			return (int)obj;
		}
	}

	private struct TransitionParticle
	{
		public IEnumerator routine;

		public EntitySerialisationEntry from;

		public EntitySerialisationEntry to;

		public ParticleSystem particleLoop;
	}

	private class AvatarSet
	{
		private int m_activeAvatar = -1;

		public GameObject[] Avatars;

		public ILogicalButton[] SwitchButtons;

		public PlayerInputLookup.Player PlayerId;

		public int ActiveAvatar
		{
			get
			{
				return m_activeAvatar;
			}
			set
			{
				m_activeAvatar = value;
				for (int i = 0; i < Avatars.Length; i++)
				{
					PlayerControls playerControls = Avatars[i].RequireComponent<PlayerControls>();
					playerControls.SetDirectlyUnderPlayerControl(i == m_activeAvatar);
				}
			}
		}

		public PlayerControls SelectedAvatar
		{
			get
			{
				if (ActiveAvatar != -1 && ActiveAvatar < Avatars.Length)
				{
					return Avatars[ActiveAvatar].RequireComponent<PlayerControls>();
				}
				return null;
			}
		}
	}

	private enum PreviousOrNext
	{
		Previous = 0,
		Next = 1
	}

	[SerializeField]
	private float m_transitionLinearSpeed = 45f;

	[SerializeField]
	private float m_transitionAngularSpeed = (float)Math.PI * 8f;

	[SerializeField]
	private float m_transitionSpiralRadius = 0.5f;

	[SerializeField]
	private ParticleSystem m_transitionParticlePrefab;

	[SerializeField]
	private ParticleSystem m_transitionStartParticlePrefab;

	[SerializeField]
	private ParticleSystem m_transitionEndParticlePrefab;

	private Dictionary<PlayerInputLookup.Player, AvatarSet> m_avatarSets = new Dictionary<PlayerInputLookup.Player, AvatarSet>(new PlayerComparer());

	private FastList<AvatarSet> m_AvatarSetList = new FastList<AvatarSet>();

	private GenericVoid m_OnChefsSetupComplete;

	private FastList<TransitionParticle> m_TransitionParticles = new FastList<TransitionParticle>(2);

	public event VoidGeneric<PlayerInputLookup.Player, PlayerControls> AvatarSelectChangeCallback = delegate
	{
	};

	public PlayerControls SelectedAvatar(PlayerInputLookup.Player _player)
	{
		AvatarSet value = null;
		m_avatarSets.TryGetValue(_player, out value);
		return (value == null) ? null : value.SelectedAvatar;
	}

	public void ForceSwitchToNext(PlayerInputLookup.Player _player)
	{
		if (base.enabled && m_avatarSets.ContainsKey(_player))
		{
			AvatarSet avatarSet = m_avatarSets[_player];
			SelectNextActiveAvatar(avatarSet, PreviousOrNext.Next, true);
		}
	}

	private void Awake()
	{
		m_OnChefsSetupComplete = InitialiseAvatars;
		DisconnectionHandler.OnChefBeingDeleted = (GenericVoid<EntitySerialisationEntry>)Delegate.Combine(DisconnectionHandler.OnChefBeingDeleted, new GenericVoid<EntitySerialisationEntry>(OnChefDeleted));
	}

	private void OnDestroy()
	{
		DisconnectionHandler.OnChefBeingDeleted = (GenericVoid<EntitySerialisationEntry>)Delegate.Remove(DisconnectionHandler.OnChefBeingDeleted, new GenericVoid<EntitySerialisationEntry>(OnChefDeleted));
	}

	public void InitialiseAvatars()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
		for (int i = 0; i < 4; i++)
		{
			AvatarSet avatarSet = new AvatarSet();
			PlayerInputLookup.Player playerId = (PlayerInputLookup.Player)i;
			avatarSet.Avatars = array.FindAll((GameObject x) => x.RequireComponent<PlayerIDProvider>().GetID() == playerId);
			avatarSet.PlayerId = playerId;
			avatarSet.ActiveAvatar = 0;
			for (int num = 0; num < avatarSet.Avatars.Length; num++)
			{
				GameObject obj = avatarSet.Avatars[num];
				ActivationCallback activationCallback = obj.RequireComponent<ActivationCallback>();
				int acopy = num;
				activationCallback.DeactivateCallbacks += delegate
				{
					if (this != null && base.gameObject != null && avatarSet.ActiveAvatar == acopy)
					{
						SelectNextActiveAvatar(avatarSet);
					}
				};
			}
			avatarSet.SwitchButtons = new ILogicalButton[2];
			avatarSet.SwitchButtons[0] = new ComboLogicalButton(new ILogicalButton[0]);
			avatarSet.SwitchButtons[1] = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.PlayerSwitch, playerId);
			m_avatarSets.Add(playerId, avatarSet);
			m_AvatarSetList.Add(avatarSet);
		}
	}

	private void SelectNextActiveAvatar(AvatarSet _avatarSet, PreviousOrNext _direction = PreviousOrNext.Next, bool _force = false)
	{
		if (_avatarSet.Avatars.Length <= 0)
		{
			return;
		}
		int activeAvatar = _avatarSet.ActiveAvatar;
		for (int i = 1; i < _avatarSet.Avatars.Length; i++)
		{
			int num = MathUtils.Wrap(_avatarSet.ActiveAvatar + ((_direction != PreviousOrNext.Next) ? (-i) : i), 0, _avatarSet.Avatars.Length);
			if (_avatarSet.Avatars[num] != null)
			{
				PlayerControls playerControls = _avatarSet.Avatars[num].RequireComponent<PlayerControls>();
				bool flag = (playerControls.enabled && !playerControls.IsSuppressed() && !TimeManager.IsPaused(playerControls.gameObject)) || playerControls.AllowSwitchingWhenDisabled;
				if (_force || flag)
				{
					SwitchAvatars(_avatarSet, num);
					break;
				}
			}
		}
	}

	private void Update()
	{
		for (int i = 0; i < m_AvatarSetList.Count; i++)
		{
			UpdateAvatarSet(m_AvatarSetList._items[i]);
		}
		for (int num = m_TransitionParticles.Count - 1; num >= 0; num--)
		{
			if (!m_TransitionParticles._items[num].routine.MoveNext())
			{
				m_TransitionParticles._items[num].particleLoop.Stop();
				m_TransitionParticles.RemoveAt(num);
			}
		}
	}

	private void UpdateAvatarSet(AvatarSet _set)
	{
		if (_set.Avatars.Length != 0)
		{
			int num = _set.SwitchButtons.FindIndex_Predicate((ILogicalButton x) => x.JustPressed());
			if (num != -1)
			{
				PreviousOrNext previousOrNext = (PreviousOrNext)num;
				SelectNextActiveAvatar(_set, (PreviousOrNext)num);
			}
		}
	}

	private void SwitchAvatars(AvatarSet _set, int _avatarId)
	{
		if (_set.ActiveAvatar != _avatarId && _set.ActiveAvatar != -1)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_set.SelectedAvatar.gameObject);
			EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(_set.Avatars[_avatarId]);
			ParticleSystem particleLoop = m_transitionParticlePrefab.InstantiatePFX(_set.Avatars[_avatarId].transform.position);
			m_TransitionParticles.Add(new TransitionParticle
			{
				routine = RunTransitionParticle(_set.SelectedAvatar.gameObject, _set.Avatars[_avatarId], particleLoop),
				from = entry,
				to = entry2,
				particleLoop = particleLoop
			});
		}
		_set.ActiveAvatar = _avatarId;
		this.AvatarSelectChangeCallback(_set.PlayerId, _set.SelectedAvatar);
	}

	private void OnChefDeleted(EntitySerialisationEntry chef)
	{
		for (int num = m_TransitionParticles.Count - 1; num >= 0; num--)
		{
			if (chef.m_Header.m_uEntityID == m_TransitionParticles._items[num].from.m_Header.m_uEntityID || chef.m_Header.m_uEntityID == m_TransitionParticles._items[num].to.m_Header.m_uEntityID)
			{
				m_TransitionParticles._items[num].particleLoop.Stop();
				m_TransitionParticles.RemoveAt(num);
			}
		}
	}

	private IEnumerator RunTransitionParticle(GameObject _from, GameObject _to, ParticleSystem particleLoop)
	{
		if (_from != null && _to != null)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.ChangeChef, base.gameObject.layer);
			ParticleSystem emitParticle = m_transitionStartParticlePrefab.InstantiatePFX(_from.transform.position);
			Vector3 diff = _to.transform.position - _from.transform.position;
			float length = diff.magnitude + 1E-07f;
			Vector3 forward = diff / length;
			Vector3 up = Vector3.up;
			Vector3 right = ((Vector3.Dot(forward, Vector3.up) != 1f) ? Vector3.Cross(up, forward) : Vector3.right);
			float prop = 0f;
			float angle = 0f;
			particleLoop.transform.position = _from.transform.position;
			particleLoop.RestartPFX();
			while (prop < 1f && _from != null && _to != null)
			{
				float dt = TimeManager.GetDeltaTime(base.gameObject.layer);
				float distance = m_transitionLinearSpeed * dt;
				length = (_to.transform.position - _from.transform.position).magnitude;
				prop = Mathf.Clamp01(prop + distance / length);
				angle += m_transitionAngularSpeed * dt;
				float offsetDistance = MathUtils.ClampedRemap(Mathf.Abs(prop - 0.5f), 0f, 0.5f, m_transitionSpiralRadius, 0f);
				Vector3 offset = offsetDistance * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle));
				Vector3 linePos = Vector3.Lerp(_from.transform.position, _to.transform.position, prop);
				particleLoop.transform.position = linePos + offset;
				yield return null;
			}
			ParticleSystem endParticle = m_transitionEndParticlePrefab.InstantiatePFX(_to.transform);
			endParticle.gameObject.layer = base.gameObject.layer;
		}
	}
}
