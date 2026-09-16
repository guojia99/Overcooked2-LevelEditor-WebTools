using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWokEffectsCosmeticDecisions : ClientSynchroniserBase, IClientCookingRegionNotified
{
	private enum EffectState
	{
		Off = 0,
		TransitionOff = 1,
		TransitionOn = 2,
		On = 3
	}

	private WokEffectsCosmeticDecisions m_wokCosmetics;

	private ParticleSystem.EmissionModule[] m_emissionModules;

	private float[] m_initialEmissionRoT;

	private GridManager m_gridManager;

	private const string c_materialParam = "_FlameOnOff";

	private int m_materialParamID = Shader.PropertyToID("_FlameOnOff");

	private Material m_flameMaterial;

	private EffectState m_state = EffectState.TransitionOff;

	private float m_progress;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_wokCosmetics = (WokEffectsCosmeticDecisions)synchronisedObject;
		m_emissionModules = new ParticleSystem.EmissionModule[m_wokCosmetics.m_particleSystems.Length];
		m_initialEmissionRoT = new float[m_wokCosmetics.m_particleSystems.Length];
		for (int i = 0; i < m_wokCosmetics.m_particleSystems.Length; i++)
		{
			ParticleSystem particleSystem = m_wokCosmetics.m_particleSystems[i];
			if (!(particleSystem == null))
			{
				ParticleSystem.EmissionModule emission = particleSystem.emission;
				m_initialEmissionRoT[i] = emission.rateOverTimeMultiplier;
				m_emissionModules[i] = emission;
			}
		}
		m_gridManager = GameUtils.GetGridManager(base.transform);
		if (m_wokCosmetics.m_flameRenderer != null)
		{
			m_flameMaterial = m_wokCosmetics.m_flameRenderer.material;
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_emissionModules == null)
		{
			return;
		}
		switch (m_state)
		{
		case EffectState.TransitionOff:
			if (m_wokCosmetics.m_transitionDuration > 0f)
			{
				m_progress = Mathf.Max(m_progress - TimeManager.GetDeltaTime(base.gameObject.layer) / m_wokCosmetics.m_transitionDuration, 0f);
			}
			else
			{
				m_progress = 0f;
			}
			if (m_progress <= 0.05f)
			{
				m_progress = 0f;
				m_state = EffectState.Off;
			}
			SetEffectsProgress(m_progress);
			break;
		case EffectState.Off:
			break;
		case EffectState.TransitionOn:
			if (m_wokCosmetics.m_transitionDuration > 0f)
			{
				m_progress = Mathf.Min(m_progress + TimeManager.GetDeltaTime(base.gameObject.layer) / m_wokCosmetics.m_transitionDuration, 1f);
			}
			else
			{
				m_progress = 1f;
			}
			if (m_progress >= 0.95f)
			{
				m_progress = 1f;
				m_state = EffectState.On;
			}
			SetEffectsProgress(m_progress);
			break;
		case EffectState.On:
			break;
		}
	}

	private void SetEffectsProgress(float _progress)
	{
		for (int i = 0; i < m_emissionModules.Length; i++)
		{
			ParticleSystem.EmissionModule emissionModule = m_emissionModules[i];
			emissionModule.rateOverTimeMultiplier = m_initialEmissionRoT[i] * m_wokCosmetics.m_emissionCurve.Evaluate(_progress);
		}
		if (m_flameMaterial != null)
		{
			m_flameMaterial.SetFloat(m_materialParamID, _progress);
		}
	}

	public void EnterCookingRegion()
	{
		if (m_state != EffectState.On)
		{
			m_state = EffectState.TransitionOn;
			GameUtils.StartAudio(GameLoopingAudioTag.DLC_04_Flames, this, base.gameObject.layer);
		}
	}

	public void ExitCookingRegion()
	{
		if (m_state != EffectState.Off)
		{
			m_state = EffectState.TransitionOff;
			GameUtils.StopAudio(GameLoopingAudioTag.DLC_04_Flames, this);
		}
	}
}
