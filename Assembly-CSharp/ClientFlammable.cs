using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientFlammable : ClientSynchroniserBase
{
	public delegate void IgnitionCallback(bool _onFire);

	private Flammable m_flammable;

	private static StaticList<ClientFlammable> s_objectsOnFire = new StaticList<ClientFlammable>();

	private static AudioSource s_audio;

	private static AudioSource s_igniteAudio;

	private float m_fireStrength;

	private float m_strengthVelocity;

	private GameObject m_fireEffect;

	private ProgressUIController m_fireGUIBar;

	private float m_hideUITime;

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

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		FlammableMessage flammableMessage = (FlammableMessage)serialisable;
		SetClampedFireStrength(flammableMessage.m_fireStrength);
		if (flammableMessage.m_onFire && !OnFire())
		{
			Ignite();
		}
		else if (!flammableMessage.m_onFire && OnFire())
		{
			Extinguish(false, flammableMessage.m_playerExtinguished);
		}
		if (OnFire())
		{
			m_strengthVelocity = flammableMessage.m_fireStrengthVelocity;
		}
	}

	public static IEnumerable<ClientFlammable> GetAllOnFire()
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

	protected override void OnDestroy()
	{
		Extinguish();
		base.OnDestroy();
	}

	protected override void OnDisable()
	{
		Extinguish();
		base.OnDisable();
	}

	public override void UpdateSynchronising()
	{
		if (s_objectsOnFire.Count == 0)
		{
			return;
		}
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		if (!OnFire())
		{
			return;
		}
		if (m_fireStrength >= 1f && m_hideUITime > 0f)
		{
			m_hideUITime -= deltaTime;
			if (m_hideUITime <= 0f)
			{
				m_fireGUIBar.gameObject.SetActive(false);
			}
		}
		if (Mathf.Abs(m_strengthVelocity) > 0f)
		{
			float clampedFireStrength = m_fireStrength + m_strengthVelocity * TimeManager.GetDeltaTime(base.gameObject);
			SetClampedFireStrength(clampedFireStrength);
		}
	}

	public bool OnFire()
	{
		return m_fireEffect != null;
	}

	private void Ignite()
	{
		if (!OnFire())
		{
			m_fireEffect = m_flammable.m_fireEffectPrefab.InstantiateOnParent(base.gameObject.transform);
			GameObject obj = GameUtils.InstantiateUIController(m_flammable.m_progressUIPrefab.gameObject, "HoverIconCanvas");
			m_fireGUIBar = obj.RequireComponent<ProgressUIController>();
			m_fireGUIBar.SetFollowTransform(base.transform, Vector3.zero);
			m_fireGUIBar.gameObject.SetActive(false);
			m_hideUITime = 0f;
			if (s_igniteAudio == null || !s_igniteAudio.isPlaying)
			{
				s_igniteAudio = GameUtils.TriggerAudio(GameOneShotAudioTag.FireIgnition, base.gameObject.layer);
			}
			if (s_audio == null)
			{
				s_audio = GameUtils.StartAudio(m_flammable.m_audioTag, s_objectsOnFire, base.gameObject.layer);
				GameUtils.StartNXRumble(m_flammable.m_audioTag);
			}
			SetClampedFireStrength(1f);
			s_objectsOnFire.Add(this);
			m_ignitionCallbacks(true);
			AdjustFireVolume();
		}
		else
		{
			SetClampedFireStrength(1f);
		}
	}

	private void Extinguish(bool shutdown = false, bool playerExtinguished = false)
	{
		if (!OnFire())
		{
			return;
		}
		ParticleSystem[] array = m_fireEffect.RequestComponentsRecursive<ParticleSystem>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Stop();
		}
		m_fireEffect = null;
		if (m_fireGUIBar != null && m_fireGUIBar.gameObject != null)
		{
			UnityEngine.Object.Destroy(m_fireGUIBar.gameObject);
			m_fireGUIBar = null;
		}
		AdjustFireVolume();
		if (s_audio != null && s_objectsOnFire.Count == 1)
		{
			GameUtils.StopNXRumble(m_flammable.m_audioTag);
			GameUtils.StopAudio(m_flammable.m_audioTag, s_objectsOnFire);
			s_audio = null;
		}
		s_objectsOnFire.Remove(this);
		if (s_objectsOnFire.Count == 0 && !shutdown && playerExtinguished)
		{
			OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
			if (overcookedAchievementManager != null)
			{
				overcookedAchievementManager.AddIDStat(7, 1, ControlPadInput.PadNum.One);
			}
		}
		m_ignitionCallbacks(false);
	}

	private void SetClampedFireStrength(float _value)
	{
		m_fireStrength = Mathf.Clamp01(_value);
		if (OnFire())
		{
			m_fireGUIBar.SetProgress(m_fireStrength);
			if (m_fireStrength < 1f)
			{
				m_fireGUIBar.gameObject.SetActive(true);
				m_hideUITime = 1f;
			}
		}
	}

	private void AdjustFireVolume()
	{
		if (s_audio != null)
		{
			s_audio.volume = MathUtils.ClampedRemap(s_objectsOnFire.Count, 0f, 5f, 0.2f, 0.6f);
		}
	}
}
