using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerFlammable : ServerSynchroniserBase
{
	private enum VelocityToken
	{
		Encourage = 0,
		Fight = 1,
		Recover = 2,
		COUNT = 3
	}

	private struct ActiveVelocities
	{
		public float Velocity;

		public VelocityToken Token;
	}

	public delegate void IgnitionCallback(bool _onFire);

	private Flammable m_flammable;

	private FlammableMessage m_data = new FlammableMessage();

	private bool m_updatePending;

	private FastList<ActiveVelocities> m_velocityStack = new FastList<ActiveVelocities>(3);

	private float m_prevVelocity;

	private static StaticList<ServerFlammable> s_objectsOnFire = new StaticList<ServerFlammable>();

	private FireConfigData m_fireConfigData;

	private MonoBehaviour[] m_immolatedComponents;

	private float m_fireStrength;

	private List<ServerFlammable> m_fireEncouragedBy = new List<ServerFlammable>();

	private ServerFlammable[] m_prevProximateFlammables = new ServerFlammable[8];

	private ServerFlammable[] m_currProximateFlammables = new ServerFlammable[8];

	private float m_encourageSupressedTimer;

	private float m_cooldownSupressedTimer;

	private bool m_canCatchFire = true;

	private bool m_onFire;

	private bool m_playerExtinguished;

	private GridManager m_gridManager;

	private IgnitionCallback m_ignitionCallbacks = delegate
	{
	};

	public static event VoidGeneric<int> OnObjectsOnFireChanged
	{
		add
		{
			s_objectsOnFire.OnObjectsChanged += value;
		}
		remove
		{
			s_objectsOnFire.OnObjectsChanged -= value;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.Flammable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_flammable = (Flammable)synchronisedObject;
	}

	private void AddActiveVelocity(VelocityToken _token, float _velocity)
	{
		bool flag = false;
		for (int i = 0; i < m_velocityStack.Count; i++)
		{
			ActiveVelocities activeVelocities = m_velocityStack._items[i];
			if (activeVelocities.Token == _token)
			{
				flag = true;
				ActiveVelocities activeVelocities2 = activeVelocities;
				activeVelocities2.Velocity += _velocity;
				m_velocityStack._items[i] = activeVelocities2;
				break;
			}
		}
		if (!flag)
		{
			ActiveVelocities item = new ActiveVelocities
			{
				Token = _token,
				Velocity = _velocity
			};
			m_velocityStack.Add(item);
		}
	}

	private void RemoveActiveVelocity(VelocityToken _token)
	{
		for (int num = m_velocityStack.Count - 1; num >= 0; num--)
		{
			ActiveVelocities activeVelocities = m_velocityStack._items[num];
			if (activeVelocities.Token == _token)
			{
				m_velocityStack.RemoveAt(num);
				break;
			}
		}
	}

	private float GetActiveVelocity(VelocityToken _token)
	{
		for (int i = 0; i < m_velocityStack.Count; i++)
		{
			ActiveVelocities activeVelocities = m_velocityStack._items[i];
			if (activeVelocities.Token == _token)
			{
				return activeVelocities.Velocity;
			}
		}
		return 0f;
	}

	private float GetTotalVelocity()
	{
		float num = 0f;
		for (int i = 0; i < m_velocityStack.Count; i++)
		{
			num += m_velocityStack._items[i].Velocity;
		}
		return num;
	}

	private void ResetActiveVelocity(VelocityToken _token)
	{
		for (int i = 0; i < m_velocityStack.Count; i++)
		{
			ActiveVelocities activeVelocities = m_velocityStack._items[i];
			if (activeVelocities.Token == _token)
			{
				activeVelocities.Velocity = 0f;
				m_velocityStack._items[i] = activeVelocities;
				break;
			}
		}
	}

	private void SynchroniseClientState()
	{
		m_data.m_playerExtinguished = m_playerExtinguished;
		m_data.m_onFire = m_onFire;
		m_data.m_fireStrength = m_fireStrength;
		m_data.m_fireStrengthVelocity = GetTotalVelocity();
		SendServerEvent(m_data);
	}

	public static IEnumerable<ServerFlammable> GetAllOnFire()
	{
		return s_objectsOnFire.GetContents();
	}

	public void RegisterIgnitionCallback(IgnitionCallback _callback)
	{
		m_ignitionCallbacks = (IgnitionCallback)Delegate.Combine(m_ignitionCallbacks, _callback);
	}

	public void UnregisterIgnitionCallback(IgnitionCallback _callback)
	{
		m_ignitionCallbacks = (IgnitionCallback)Delegate.Remove(m_ignitionCallbacks, _callback);
	}

	private void Awake()
	{
		m_immolatedComponents = new MonoBehaviour[3]
		{
			base.gameObject.GetComponent<Interactable>(),
			base.gameObject.GetComponent<PickupItemSpawner>(),
			base.gameObject.GetComponent<Workstation>()
		};
		if (GameUtils.GetLevelConfig().m_hazardInfo != null)
		{
			m_fireConfigData = GameUtils.GetLevelConfig().m_hazardInfo.FireConfigData;
		}
		m_gridManager = GameUtils.GetGridManager(base.transform);
	}

	public override void OnDestroy()
	{
		_Extinguish(true);
		base.OnDestroy();
	}

	protected override void OnDisable()
	{
		_Extinguish(false);
		base.OnDisable();
	}

	private void SetImmolatedComponentsEnabled(bool _enabled)
	{
		for (int i = 0; i < m_immolatedComponents.Length; i++)
		{
			MonoBehaviour monoBehaviour = m_immolatedComponents[i];
			if (monoBehaviour != null)
			{
				monoBehaviour.enabled = _enabled;
			}
		}
	}

	public void SetCanCatchFire(bool _canCatchFire)
	{
		m_canCatchFire = _canCatchFire;
	}

	public bool OnFire()
	{
		return m_onFire;
	}

	private bool CanCatchFire()
	{
		return m_canCatchFire && m_fireConfigData != null;
	}

	public void Ignite()
	{
		if (CanCatchFire() && !OnFire())
		{
			SetClampedFireStrength(1f);
			SetImmolatedComponentsEnabled(false);
			s_objectsOnFire.Add(this);
			m_ignitionCallbacks(true);
			m_encourageSupressedTimer = 0f;
			m_onFire = true;
			m_updatePending = true;
		}
		else if (CanCatchFire() && OnFire())
		{
			SetClampedFireStrength(1f);
			m_updatePending = true;
		}
	}

	public void StartEncourageFire(ServerFlammable _flammable)
	{
		m_fireEncouragedBy.Add(_flammable);
		EncourageFire(_flammable);
	}

	public void EncourageFire(ServerFlammable _flammable)
	{
		if (m_encourageSupressedTimer <= 0f)
		{
			float num = CalculateFlammabilityTime(m_fireEncouragedBy, this);
			AddActiveVelocity(VelocityToken.Encourage, 1f / num);
		}
	}

	public void StopEncourageFire(ServerFlammable _flamable)
	{
		m_fireEncouragedBy.Remove(_flamable);
	}

	public void FightFire(float _timeToExtinguish, float _deltaTime, bool player = false)
	{
		if (OnFire())
		{
			float num = 1f / _timeToExtinguish;
			AddActiveVelocity(VelocityToken.Fight, 0f - num);
			SetClampedFireStrength(m_fireStrength - num * _deltaTime);
			m_encourageSupressedTimer = m_fireConfigData.EncouragementSupressedTime;
			if (m_fireStrength == 0f)
			{
				_Extinguish(false, player);
			}
		}
	}

	public void Extinguish()
	{
		_Extinguish(false);
	}

	public void _Extinguish(bool _shutdown, bool player = false)
	{
		if (!OnFire())
		{
			return;
		}
		SetImmolatedComponentsEnabled(true);
		s_objectsOnFire.Remove(this);
		m_onFire = false;
		m_playerExtinguished = player;
		m_ignitionCallbacks(false);
		for (int i = 0; i < m_currProximateFlammables.Length; i++)
		{
			ServerFlammable serverFlammable = m_currProximateFlammables[i];
			if (serverFlammable != null)
			{
				serverFlammable.StopEncourageFire(this);
				m_currProximateFlammables[i] = null;
			}
		}
		RemoveActiveVelocity(VelocityToken.Encourage);
		RemoveActiveVelocity(VelocityToken.Fight);
		RemoveActiveVelocity(VelocityToken.Recover);
		if (!_shutdown)
		{
			m_updatePending = true;
		}
	}

	private void SetClampedFireStrength(float _value)
	{
		m_fireStrength = Mathf.Clamp01(_value);
	}

	private void OnFireIncrease(float _velocity, float _deltaTime)
	{
		if (CanCatchFire() && m_encourageSupressedTimer <= 0f)
		{
			m_cooldownSupressedTimer = m_fireConfigData.CooldownSupressedTime;
			if (GetActiveVelocity(VelocityToken.Recover) == 0f)
			{
				AddActiveVelocity(VelocityToken.Recover, 1f / m_fireConfigData.FireRecoveryTime);
			}
			SetClampedFireStrength(m_fireStrength + _velocity * _deltaTime);
			if (m_fireStrength == 1f)
			{
				Ignite();
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_flammable.m_startOnFire)
		{
			if (!OnFire())
			{
				Ignite();
			}
			m_flammable.m_startOnFire = false;
		}
		if (s_objectsOnFire.Count == 0 && !m_updatePending)
		{
			return;
		}
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		if (m_fireEncouragedBy.Count > 0 && m_fireStrength < 1f)
		{
			float activeVelocity = GetActiveVelocity(VelocityToken.Encourage);
			float velocity = ((!(activeVelocity < deltaTime)) ? activeVelocity : 1f);
			OnFireIncrease(velocity, deltaTime);
		}
		if (OnFire())
		{
			ServerFlammable[] prevProximateFlammables = m_prevProximateFlammables;
			m_prevProximateFlammables = m_currProximateFlammables;
			m_currProximateFlammables = prevProximateFlammables;
			GetProximateFlammables(ref m_currProximateFlammables);
			for (int i = 0; i < m_currProximateFlammables.Length; i++)
			{
				ServerFlammable serverFlammable = m_currProximateFlammables[i];
				ServerFlammable serverFlammable2 = m_prevProximateFlammables[i];
				if (serverFlammable != serverFlammable2)
				{
					if (serverFlammable != null)
					{
						serverFlammable.StartEncourageFire(this);
					}
					if (serverFlammable2 != null)
					{
						serverFlammable2.StopEncourageFire(this);
					}
				}
				else if (serverFlammable != null)
				{
					serverFlammable.EncourageFire(this);
				}
			}
			if (m_fireStrength < 1f)
			{
				float activeVelocity2 = GetActiveVelocity(VelocityToken.Recover);
				OnFireIncrease(activeVelocity2, deltaTime);
			}
		}
		else if (m_fireStrength > 0f && m_cooldownSupressedTimer <= 0f)
		{
			SetClampedFireStrength(m_fireStrength - 1f / m_fireConfigData.CooldownTime * deltaTime);
		}
		if (m_encourageSupressedTimer > 0f)
		{
			m_encourageSupressedTimer -= deltaTime;
		}
		if (m_cooldownSupressedTimer > 0f)
		{
			m_cooldownSupressedTimer = m_encourageSupressedTimer - deltaTime;
		}
		float totalVelocity = GetTotalVelocity();
		if (totalVelocity != m_prevVelocity)
		{
			m_updatePending = true;
		}
		if (m_updatePending)
		{
			SynchroniseClientState();
			m_updatePending = false;
		}
		ResetActiveVelocity(VelocityToken.Encourage);
		ResetActiveVelocity(VelocityToken.Fight);
		ResetActiveVelocity(VelocityToken.Recover);
		m_prevVelocity = totalVelocity;
	}

	private static float CalculateFlammabilityTime(List<ServerFlammable> _instigators, ServerFlammable _target)
	{
		for (int i = 0; i < _instigators.Count; i++)
		{
			if (_instigators[i].m_flammable.m_overrideTargetFlammability)
			{
				return _instigators[i].m_flammable.m_overrideTargetFlammabilityTime;
			}
		}
		return _target.m_fireConfigData.FlammabilityTime;
	}

	private void GetProximateFlammables(ref ServerFlammable[] _proximateFlammables)
	{
		float num = m_flammable.m_fireSpreadRadius * m_flammable.m_fireSpreadRadius;
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(base.transform.position);
		int num2 = -1;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				if (i == 0 && j == 0)
				{
					continue;
				}
				num2++;
				GridIndex index = new GridIndex(gridLocationFromPos.X + i, gridLocationFromPos.Y, gridLocationFromPos.Z + j);
				Vector3 posFromGridLocation = m_gridManager.GetPosFromGridLocation(index);
				if ((base.transform.position - posFromGridLocation).sqrMagnitude >= num)
				{
					m_currProximateFlammables[num2] = null;
					continue;
				}
				GameObject gridOccupant = m_gridManager.GetGridOccupant(index);
				if (gridOccupant == null)
				{
					m_currProximateFlammables[num2] = null;
				}
				else if (gridOccupant != base.gameObject)
				{
					m_currProximateFlammables[num2] = ComponentCache<ServerFlammable>.GetComponent(gridOccupant);
				}
			}
		}
	}
}
