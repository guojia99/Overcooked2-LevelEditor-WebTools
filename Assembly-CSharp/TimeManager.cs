using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimeManager : Manager
{
	public enum PauseLayer
	{
		Main = 0,
		UI = 1,
		Camera = 2,
		System = 3,
		Network = 4,
		Count = 5
	}

	private class FrozenPhysicsData
	{
		private int m_layer;

		private Rigidbody m_frozenBody;

		private Vector3 m_linearVelocity;

		private Vector3 m_angularVelocity;

		private bool m_isKinematic;

		private bool m_useGravity;

		public int Layer
		{
			get
			{
				return m_layer;
			}
		}

		public FrozenPhysicsData(Rigidbody _rigidBody, int _layer)
		{
			m_frozenBody = _rigidBody;
			m_linearVelocity = _rigidBody.velocity;
			m_angularVelocity = _rigidBody.angularVelocity;
			m_isKinematic = _rigidBody.isKinematic;
			m_useGravity = _rigidBody.useGravity;
			m_layer = _layer;
			m_frozenBody.velocity = Vector3.zero;
			m_frozenBody.angularVelocity = Vector3.zero;
			m_frozenBody.isKinematic = true;
			m_frozenBody.useGravity = false;
		}

		public void Unfreeze()
		{
			if (m_frozenBody != null)
			{
				m_frozenBody.velocity = m_linearVelocity;
				m_frozenBody.angularVelocity = m_angularVelocity;
				m_frozenBody.isKinematic = m_isKinematic;
				m_frozenBody.useGravity = m_useGravity;
				m_frozenBody = null;
			}
		}
	}

	[SerializeField]
	private LayerMask m_mainLayer;

	[SerializeField]
	private LayerMask m_uiLayer;

	[SerializeField]
	private LayerMask m_cameraLayer;

	[SerializeField]
	private LayerMask m_networkLayer;

	[SerializeField]
	private LayerMask m_systemLayer = -1;

	[SerializeField]
	private float m_debugtimeScale = 1f;

	private List<object>[] m_arbitrationSupressors = new List<object>[5];

	private List<FrozenPhysicsData> m_frozenPhysics = new List<FrozenPhysicsData>();

	private bool[] m_paused = new bool[5];

	private const int c_layerCount = 32;

	private float[] m_timeMultiplier = new float[0];

	private LayerMask m_pauseMask = 0;

	private static TimeManager s_timeManager;

	private void Awake()
	{
		for (int i = 0; i < 5; i++)
		{
			m_paused[i] = false;
			m_arbitrationSupressors[i] = new List<object>();
		}
		m_timeMultiplier = new float[32];
		for (int j = 0; j < m_timeMultiplier.Length; j++)
		{
			m_timeMultiplier[j] = 1f;
		}
		s_timeManager = this;
	}

	private void Update()
	{
		Time.timeScale = m_debugtimeScale;
	}

	private void OnDestroy()
	{
		s_timeManager = null;
	}

	private bool UpdateArbitratedPause(PauseLayer _layer, bool _pause, object _arbitrationKey)
	{
		List<object> list = m_arbitrationSupressors[(int)_layer];
		if (_pause)
		{
			list.Add(_arbitrationKey);
		}
		else
		{
			list.RemoveAll(_arbitrationKey.Equals);
		}
		return list.Count != 0;
	}

	public static bool IsPaused(PauseLayer _layer)
	{
		if (s_timeManager != null)
		{
			return s_timeManager.IsLayerPaused(_layer);
		}
		return false;
	}

	protected bool IsLayerPaused(PauseLayer _layer)
	{
		return m_arbitrationSupressors[(int)_layer].Count != 0;
	}

	public void SetPaused(PauseLayer _layer, bool _pause, object _arbitrationKey)
	{
		bool flag = UpdateArbitratedPause(_layer, _pause, _arbitrationKey);
		bool flag2 = m_paused[(int)_layer];
		if (flag2 != flag)
		{
			OnPauseStatusChange(_layer, flag);
		}
	}

	private LayerMask GetLayerMask(PauseLayer _layer)
	{
		switch (_layer)
		{
		case PauseLayer.Main:
			return m_mainLayer;
		case PauseLayer.UI:
			return m_uiLayer;
		case PauseLayer.Camera:
			return m_cameraLayer;
		case PauseLayer.System:
			return m_systemLayer;
		case PauseLayer.Network:
			return m_networkLayer;
		default:
			return default(LayerMask);
		}
	}

	private void OnPauseStatusChange(PauseLayer _layer, bool _pause)
	{
		LayerMask pauseMask = m_pauseMask;
		m_paused[(int)_layer] = _pause;
		LayerMask layerMask = 0;
		for (int i = 0; i < m_paused.Length; i++)
		{
			if (m_paused[i])
			{
				layerMask = (int)layerMask | (int)GetLayerMask((PauseLayer)i);
			}
		}
		m_pauseMask = layerMask;
		if (_layer != PauseLayer.Network)
		{
			if (_pause)
			{
				LayerMask mask = (int)layerMask & ~(int)pauseMask;
				OnPauseChange(mask, true);
			}
			else
			{
				LayerMask mask2 = ~(int)layerMask & (int)pauseMask;
				OnPauseChange(mask2, false);
			}
		}
	}

	private void OnPauseChange(LayerMask _mask, bool _pause)
	{
		float num = ((!_pause) ? 1f : 0f);
		for (int i = 0; i < m_timeMultiplier.Length; i++)
		{
			if (InMask(_mask, i))
			{
				m_timeMultiplier[i] = num;
			}
		}
		Animator[] array = UnityEngine.Object.FindObjectsOfType<Animator>();
		for (int j = 0; j < array.Length; j++)
		{
			if (InMask(_mask, array[j].gameObject.layer))
			{
				array[j].speed = num;
			}
		}
		Animation[] array2 = UnityEngine.Object.FindObjectsOfType<Animation>();
		for (int k = 0; k < array2.Length; k++)
		{
			if (!InMask(_mask, array2[k].gameObject.layer))
			{
				continue;
			}
			IEnumerator enumerator = array2[k].GetEnumerator();
			while (enumerator.MoveNext())
			{
				AnimationState animationState = enumerator.Current as AnimationState;
				if (animationState != null)
				{
					animationState.speed = num;
				}
			}
		}
		ParticleSystem[] array3 = GameObjectUtils.FindComponentsOfTypeInScene<ParticleSystem>();
		for (int l = 0; l < array3.Length; l++)
		{
			if (InMask(_mask, array3[l].gameObject.layer))
			{
				ParticleSystem.MainModule main = array3[l].main;
				main.simulationSpeed = num;
			}
		}
		AudioSource[] array4 = UnityEngine.Object.FindObjectsOfType<AudioSource>();
		for (int m = 0; m < array4.Length; m++)
		{
			if (InMask(_mask, array4[m].gameObject.layer))
			{
				if (_pause)
				{
					array4[m].Pause();
				}
				else
				{
					array4[m].UnPause();
				}
			}
		}
		if (_pause)
		{
			Rigidbody[] array5 = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
			array5 = array5.AllRemoved_Predicate((Rigidbody x) => !InMask(_mask, x.gameObject.layer));
			for (int num2 = 0; num2 < array5.Length; num2++)
			{
				m_frozenPhysics.Add(new FrozenPhysicsData(array5[num2], array5[num2].gameObject.layer));
			}
		}
		else
		{
			Predicate<FrozenPhysicsData> match = delegate(FrozenPhysicsData _fpd)
			{
				if (InMask(_mask, _fpd.Layer))
				{
					_fpd.Unfreeze();
					return true;
				}
				return false;
			};
			m_frozenPhysics.RemoveAll(match);
		}
		PlayableDirector[] array6 = UnityEngine.Object.FindObjectsOfType<PlayableDirector>();
		for (int num3 = 0; num3 < array6.Length; num3++)
		{
			PlayableGraph playableGraph = array6[num3].playableGraph;
			if (playableGraph.IsValid())
			{
				int rootPlayableCount = playableGraph.GetRootPlayableCount();
				for (int num4 = 0; num4 < rootPlayableCount; num4++)
				{
					playableGraph.GetRootPlayable(num4).SetSpeed((!_pause) ? 1 : 0);
				}
			}
		}
	}

	public static bool IsPaused(int _layer)
	{
		if (s_timeManager != null)
		{
			for (int i = 0; i < 5; i++)
			{
				LayerMask layerMask = s_timeManager.GetLayerMask((PauseLayer)i);
				if (InMask(layerMask, _layer))
				{
					return s_timeManager.m_paused[i];
				}
			}
		}
		return false;
	}

	public static bool IsPaused(GameObject _object)
	{
		return IsPaused(_object.layer);
	}

	public static float GetDeltaTime(int _layer)
	{
		if (s_timeManager != null)
		{
			return s_timeManager.m_timeMultiplier[_layer] * Time.deltaTime;
		}
		return Time.deltaTime;
	}

	public static float GetFixedDeltaTime(int _layer)
	{
		if (s_timeManager != null)
		{
			return s_timeManager.m_timeMultiplier[_layer] * Time.fixedDeltaTime;
		}
		return Time.fixedDeltaTime;
	}

	public static float GetDeltaTime(GameObject _object)
	{
		return GetDeltaTime(_object.layer);
	}

	public static float GetFixedDeltaTime(GameObject _object)
	{
		return GetFixedDeltaTime(_object.layer);
	}

	private static bool InMask(LayerMask _layerMask, int _layerId)
	{
		return (_layerMask.value & (1 << _layerId)) != 0;
	}
}
