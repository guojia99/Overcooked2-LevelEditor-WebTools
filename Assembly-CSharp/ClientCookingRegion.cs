using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCookingRegion : ClientSynchroniserBase
{
	private class SharedInfo
	{
		public List<ClientCookingRegion> m_allRegions = new List<ClientCookingRegion>();

		public List<ClientCookingRegion> m_activeRegions = new List<ClientCookingRegion>();

		public float m_lastIgniteTime = float.MinValue;

		public float m_lastExtinguishTime = float.MinValue;
	}

	private CookingRegion m_CookingRegion;

	private TriggerRecorder m_recorder;

	private GridManager m_gridManager;

	private GridIndex m_gridIndex;

	private const string c_burnerMatEmissParam = "_EmissiveColour";

	private int m_burnerMatEmissParamID = Shader.PropertyToID("_EmissiveColour");

	private Material[] m_burnerMaterials;

	private IEnumerator m_transitionRoutine;

	private ParticleSystem.Particle[] m_particles;

	private bool m_wasBaseEnabled;

	private static SharedInfo s_sharedInfo;

	private const float c_timeBeforeAudioRetrigger = 0.25f;

	private object m_fireAudioToken = new object();

	private GameObject m_prevOccupant;

	public override EntityType GetEntityType()
	{
		return EntityType.CookingRegion;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_CookingRegion = (CookingRegion)synchronisedObject;
		m_gridManager = GameUtils.GetGridManager(base.transform);
		m_gridIndex = m_gridManager.GetGridLocationFromPos(base.transform.position);
		m_recorder = base.gameObject.RequireComponent<TriggerRecorder>();
		m_wasBaseEnabled = m_CookingRegion.enabled;
		OnEnableChanged(m_wasBaseEnabled);
		if (m_CookingRegion.m_burnerRenderer != null)
		{
			m_burnerMaterials = m_CookingRegion.m_burnerRenderer.materials;
			m_burnerMaterials.AllRemoved_Predicate((Material x) => x == null || !x.HasProperty(m_burnerMatEmissParamID));
		}
	}

	protected void Awake()
	{
		if (s_sharedInfo == null)
		{
			s_sharedInfo = new SharedInfo();
		}
		s_sharedInfo.m_allRegions.Add(this);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		bool flag = m_CookingRegion.enabled;
		if (flag != m_wasBaseEnabled)
		{
			OnEnableChanged(flag);
			m_wasBaseEnabled = flag;
		}
		if (m_transitionRoutine != null && !m_transitionRoutine.MoveNext())
		{
			m_transitionRoutine = null;
		}
		if (!(m_CookingRegion != null))
		{
			return;
		}
		bool flag2 = AnyPFXPlaying();
		if (m_CookingRegion.m_TriggerArea != null && m_CookingRegion.enabled != m_CookingRegion.m_TriggerArea.enabled)
		{
			m_CookingRegion.m_TriggerArea.enabled = m_CookingRegion.enabled || flag2;
		}
		if (m_CookingRegion.enabled || flag2)
		{
			List<Collider> recentCollisions = m_recorder.GetRecentCollisions();
			for (int i = 0; i < recentCollisions.Count; i++)
			{
				Collider collider = recentCollisions[i];
				IClientCookable clientCookable = collider.gameObject.RequestInterface<IClientCookable>();
				if (clientCookable != null && clientCookable.GetRequiredStationType() == m_CookingRegion.m_StationType)
				{
					UpdateFlameParticles(collider);
					break;
				}
			}
			GameObject gameObject = m_gridManager.GetGridOccupant(m_gridIndex);
			if (gameObject != null)
			{
				GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(gameObject.transform.position);
				if (gridLocationFromPos != m_gridIndex)
				{
					gameObject = null;
				}
			}
			if (gameObject != null && gameObject != m_prevOccupant)
			{
				IClientCookingRegionNotified[] array = gameObject.RequestInterfacesRecursive<IClientCookingRegionNotified>();
				for (int j = 0; j < array.Length; j++)
				{
					if (array[j] as MonoBehaviour != null)
					{
						array[j].EnterCookingRegion();
						m_prevOccupant = gameObject;
					}
				}
			}
			else
			{
				if (!(gameObject == null) || !(m_prevOccupant != null))
				{
					return;
				}
				IClientCookingRegionNotified[] array2 = m_prevOccupant.RequestInterfacesRecursive<IClientCookingRegionNotified>();
				for (int k = 0; k < array2.Length; k++)
				{
					if (array2[k] as MonoBehaviour != null)
					{
						array2[k].ExitCookingRegion();
						m_prevOccupant = null;
					}
				}
			}
		}
		else
		{
			if (!(m_prevOccupant != null))
			{
				return;
			}
			IClientCookingRegionNotified[] array3 = m_prevOccupant.RequestInterfacesRecursive<IClientCookingRegionNotified>();
			for (int l = 0; l < array3.Length; l++)
			{
				if (array3[l] as MonoBehaviour != null)
				{
					array3[l].ExitCookingRegion();
					m_prevOccupant = null;
				}
			}
		}
	}

	private bool AnyPFXPlaying()
	{
		for (int i = 0; i < m_CookingRegion.m_flameEffects.Length; i++)
		{
			ParticleSystem particleSystem = m_CookingRegion.m_flameEffects[i];
			if (!(particleSystem == null) && particleSystem.isPlaying)
			{
				return true;
			}
		}
		return false;
	}

	private void SetPFXEnabled(bool bOn)
	{
		for (int i = 0; i < m_CookingRegion.m_flameEffects.Length; i++)
		{
			ParticleSystem particleSystem = m_CookingRegion.m_flameEffects[i];
			if (!(particleSystem == null))
			{
				if (bOn)
				{
					particleSystem.Play();
				}
				else
				{
					particleSystem.Stop();
				}
			}
		}
	}

	private void UpdateFlameParticles(Collider _collider)
	{
		for (int i = 0; i < m_CookingRegion.m_flameEffects.Length; i++)
		{
			ParticleSystem particleSystem = m_CookingRegion.m_flameEffects[i];
			if (particleSystem == null)
			{
				continue;
			}
			Vector3 center = _collider.bounds.center;
			Vector3 position = particleSystem.transform.position;
			float sqrMagnitude = new Vector2(center.x - position.x, center.z - position.z).sqrMagnitude;
			if (sqrMagnitude > m_CookingRegion.m_flameOffRadius * m_CookingRegion.m_flameOffRadius)
			{
				if (!particleSystem.isEmitting && m_CookingRegion.enabled)
				{
					particleSystem.Play();
				}
				continue;
			}
			float num = m_CookingRegion.m_heightCurve.Evaluate(Mathf.Sqrt(sqrMagnitude));
			if (num > 0.01f)
			{
				if (!particleSystem.isEmitting && m_CookingRegion.enabled)
				{
					particleSystem.Play();
				}
			}
			else if (particleSystem.isEmitting)
			{
				particleSystem.Stop();
			}
			if (!particleSystem.isPlaying)
			{
				continue;
			}
			if (m_particles == null || m_particles.Length < particleSystem.particleCount)
			{
				m_particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
			}
			int particles = particleSystem.GetParticles(m_particles);
			float num2 = position.y + num;
			bool flag = false;
			for (int j = 0; j < particles; j++)
			{
				ParticleSystem.Particle particle = m_particles[j];
				if (particle.position.y >= num2)
				{
					m_particles[j].remainingLifetime = -1f;
					flag = true;
				}
			}
			if (flag)
			{
				particleSystem.SetParticles(m_particles, particles);
			}
		}
	}

	private IEnumerator TransitionEnabledRoutine(bool bOn)
	{
		if (m_CookingRegion.m_glowEffect != null)
		{
			if (bOn)
			{
				m_CookingRegion.m_glowEffect.Play();
			}
			else
			{
				m_CookingRegion.m_glowEffect.Stop();
			}
		}
		SetPFXEnabled(bOn);
		int layer = base.gameObject.layer;
		float timeLeft = m_CookingRegion.m_fadeDuration;
		float percentEnabled = 0f;
		Color[] emissionColors = null;
		if (m_burnerMaterials != null)
		{
			emissionColors = new Color[m_burnerMaterials.Length];
			for (int i = 0; i < m_burnerMaterials.Length; i++)
			{
				emissionColors[i] = m_burnerMaterials[i].GetColor(m_burnerMatEmissParamID);
			}
		}
		while (timeLeft >= 0f)
		{
			timeLeft -= TimeManager.GetDeltaTime(layer);
			percentEnabled = Mathf.Clamp01(timeLeft / Mathf.Max(m_CookingRegion.m_fadeDuration, 1E-06f));
			if (bOn)
			{
				percentEnabled = 1f - percentEnabled;
			}
			if (m_burnerMaterials != null)
			{
				for (int j = 0; j < m_burnerMaterials.Length; j++)
				{
					Color color = emissionColors[j];
					m_burnerMaterials[j].SetColor(m_burnerMatEmissParamID, new Color(color.r, color.g, color.b, percentEnabled));
				}
			}
			yield return null;
		}
	}

	private void OnEnableChanged(bool bOn)
	{
		m_transitionRoutine = TransitionEnabledRoutine(bOn);
		if (bOn)
		{
			s_sharedInfo.m_activeRegions.Add(this);
			if (s_sharedInfo.m_activeRegions.Count == 1)
			{
				GameUtils.StartAudio(GameLoopingAudioTag.DLC_04_Embers, m_fireAudioToken, base.gameObject.layer);
			}
			if (ClientTime.Time() - s_sharedInfo.m_lastIgniteTime > 0.25f)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_04_FlameIgnite, base.gameObject.layer);
				s_sharedInfo.m_lastIgniteTime = ClientTime.Time();
			}
		}
		else
		{
			s_sharedInfo.m_activeRegions.Remove(this);
			if (s_sharedInfo.m_activeRegions.Count == 0)
			{
				GameUtils.StopAudio(GameLoopingAudioTag.DLC_04_Embers, m_fireAudioToken);
			}
			if (ClientTime.Time() - s_sharedInfo.m_lastExtinguishTime > 0.25f)
			{
				GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_04_FlameDie, base.gameObject.layer);
				s_sharedInfo.m_lastExtinguishTime = ClientTime.Time();
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		s_sharedInfo.m_allRegions.Remove(this);
		if (s_sharedInfo.m_allRegions.Count == 0)
		{
			s_sharedInfo = null;
		}
	}
}
