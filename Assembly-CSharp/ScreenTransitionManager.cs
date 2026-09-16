using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundles;
using Team17.Online;
using UnityEngine;

public class ScreenTransitionManager : Manager
{
	private class UpDownTransition
	{
		public TransitionState state;

		public IEnumerator enumerator;

		public CallbackVoid callback;

		public UpDownTransition(TransitionState _state, IEnumerator _enumerator, CallbackVoid _callback)
		{
			state = _state;
			enumerator = _enumerator;
			if (_callback != null)
			{
				callback = (CallbackVoid)Delegate.Combine(callback, _callback);
			}
		}
	}

	private static class Uniforms
	{
		internal static readonly int _FillColour = Shader.PropertyToID("_FillColour");

		internal static readonly int _MaskTexture = Shader.PropertyToID("_MaskTexture");

		internal static readonly int _Fade = Shader.PropertyToID("_Fade");

		internal static readonly int _Cutoff = Shader.PropertyToID("_Cutoff");

		internal static readonly int _MaxScale = Shader.PropertyToID("_MaxScale");
	}

	private enum TransitionState
	{
		eDown = 0,
		eUp = 1,
		eTransitionUp = 2,
		eTransitionDown = 3
	}

	[SerializeField]
	private T17Image m_TransitionImage;

	[SerializeField]
	private float m_MaskCutoff = 0.4f;

	[SerializeField]
	private float m_MaxScaleDown = 50f;

	[SerializeField]
	private Color m_FillColour = Color.black;

	[SerializeField]
	private List<Texture2D> m_MaskTextures = new List<Texture2D>();

	[SerializeField]
	private AnimationCurve m_TransitionCurve;

	private Material m_TransitionMaterial;

	private Suppressor m_eventSystemSuppressor;

	private Queue<IEnumerator> m_TransitionLoads = new Queue<IEnumerator>();

	private List<UpDownTransition> m_UpDownTransitionInfos = new List<UpDownTransition>();

	private TransitionState m_CurrentState;

	public bool IsIdle
	{
		get
		{
			return m_UpDownTransitionInfos.Count == 0 && TransitionState.eDown == m_CurrentState;
		}
	}

	private UpDownTransition GetLastTransition()
	{
		if (m_UpDownTransitionInfos.Count != 0)
		{
			return m_UpDownTransitionInfos[m_UpDownTransitionInfos.Count - 1];
		}
		return null;
	}

	private UpDownTransition GetCurrentTransition()
	{
		if (m_UpDownTransitionInfos.Count != 0)
		{
			return m_UpDownTransitionInfos[0];
		}
		return null;
	}

	private void Awake()
	{
		if (m_TransitionImage == null)
		{
			m_TransitionImage = base.gameObject.RequireComponentRecursive<T17Image>();
		}
		m_TransitionMaterial = m_TransitionImage.material;
		m_TransitionImage.gameObject.SetActive(false);
	}

	public void Update()
	{
		UpDownTransition currentTransition = GetCurrentTransition();
		if (currentTransition != null && !currentTransition.enumerator.MoveNext())
		{
			m_UpDownTransitionInfos.RemoveAt(0);
		}
		if (m_TransitionLoads.Count > 0 && !m_TransitionLoads.Peek().MoveNext())
		{
			m_TransitionLoads.Dequeue();
		}
	}

	public bool StartTransitionUp(CallbackVoid OnTransitionUp = null)
	{
		UpDownTransition lastTransition = GetLastTransition();
		UpDownTransition currentTransition = GetCurrentTransition();
		if (m_CurrentState != TransitionState.eTransitionDown && m_CurrentState == TransitionState.eUp)
		{
			if (OnTransitionUp != null)
			{
				OnTransitionUp();
			}
			return true;
		}
		if (lastTransition != null && lastTransition.state == TransitionState.eUp)
		{
			lastTransition.callback = (CallbackVoid)Delegate.Combine(lastTransition.callback, OnTransitionUp);
			return true;
		}
		m_UpDownTransitionInfos.Add(new UpDownTransition(TransitionState.eUp, TransitionUp(), OnTransitionUp));
		return true;
	}

	public bool StartTransitionDown(CallbackVoid OnTransitionDown = null)
	{
		UpDownTransition lastTransition = GetLastTransition();
		UpDownTransition currentTransition = GetCurrentTransition();
		if (m_CurrentState != TransitionState.eTransitionUp && m_CurrentState == TransitionState.eDown)
		{
			if (OnTransitionDown != null)
			{
				OnTransitionDown();
			}
			return true;
		}
		if (lastTransition != null && lastTransition.state == TransitionState.eDown)
		{
			lastTransition.callback = (CallbackVoid)Delegate.Combine(lastTransition.callback, OnTransitionDown);
			return true;
		}
		m_UpDownTransitionInfos.Add(new UpDownTransition(TransitionState.eDown, TransitionDown(), OnTransitionDown));
		return true;
	}

	private IEnumerator TransitionUp()
	{
		m_CurrentState = TransitionState.eTransitionUp;
		FastList<User> userList = ClientUserSystem.m_Users;
		for (int i = 0; i < userList.Count; i++)
		{
			User user = userList._items[i];
			if (user.Engagement != EngagementSlot.One)
			{
				continue;
			}
			if (user != null && user.GamepadUser != null)
			{
				T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user.GamepadUser);
				if (eventSystemForGamepadUser != null && m_eventSystemSuppressor == null)
				{
					m_eventSystemSuppressor = eventSystemForGamepadUser.Disable(this);
				}
			}
			break;
		}
		m_TransitionImage.gameObject.SetActive(true);
		m_TransitionMaterial.SetColor(Uniforms._FillColour, m_FillColour);
		m_TransitionMaterial.SetTexture(Uniforms._MaskTexture, PickRandomMaskTexture());
		m_TransitionMaterial.SetFloat(Uniforms._Cutoff, m_MaskCutoff);
		m_TransitionMaterial.SetFloat(Uniforms._MaxScale, m_MaxScaleDown);
		m_TransitionMaterial.SetFloat(Uniforms._Fade, 0f);
		yield return null;
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIScreenOut, base.gameObject.layer);
		float fCurrentFade = 0f;
		float fTime = 0f;
		while (fTime <= 1f)
		{
			fTime += Time.deltaTime;
			fCurrentFade = m_TransitionCurve.Evaluate(fTime) * m_MaxScaleDown;
			m_TransitionMaterial.SetFloat(Uniforms._Fade, fCurrentFade);
			yield return null;
		}
		m_CurrentState = TransitionState.eUp;
		UpDownTransition currentTransition = GetCurrentTransition();
		if (currentTransition.callback != null)
		{
			currentTransition.callback();
		}
	}

	private IEnumerator TransitionDown()
	{
		m_CurrentState = TransitionState.eTransitionDown;
		FastList<User> userList = ClientUserSystem.m_Users;
		m_TransitionImage.gameObject.SetActive(true);
		m_TransitionMaterial.SetColor(Uniforms._FillColour, m_FillColour);
		m_TransitionMaterial.SetTexture(Uniforms._MaskTexture, PickRandomMaskTexture());
		m_TransitionMaterial.SetFloat(Uniforms._Cutoff, m_MaskCutoff);
		m_TransitionMaterial.SetFloat(Uniforms._MaxScale, m_MaxScaleDown);
		m_TransitionMaterial.SetFloat(Uniforms._Fade, m_MaxScaleDown);
		yield return null;
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIScreenIn, base.gameObject.layer);
		float fCurrentFade = m_MaxScaleDown;
		float fTime = 0f;
		while (fTime <= 1f)
		{
			fTime += Time.deltaTime;
			fCurrentFade = m_TransitionCurve.Evaluate(1f - fTime) * m_MaxScaleDown;
			m_TransitionMaterial.SetFloat(Uniforms._Fade, fCurrentFade);
			yield return null;
		}
		m_CurrentState = TransitionState.eDown;
		if (m_eventSystemSuppressor != null)
		{
			m_eventSystemSuppressor.Release();
			m_eventSystemSuppressor = null;
		}
		UpDownTransition currentTransition = GetCurrentTransition();
		if (currentTransition.callback != null)
		{
			currentTransition.callback();
		}
	}

	private Texture PickRandomMaskTexture()
	{
		int index = UnityEngine.Random.Range(0, m_MaskTextures.Count);
		return m_MaskTextures[index];
	}

	public void TransitionLoad(string _sceneName)
	{
		m_TransitionLoads.Enqueue(TransitionLoadRoutine(_sceneName));
	}

	private IEnumerator TransitionLoadRoutine(string _sceneName)
	{
		SpinnerIconManager spinnerMan = SpinnerIconManager.Instance;
		Suppressor suppressor = null;
		if (spinnerMan != null)
		{
			suppressor = spinnerMan.Show(SpinnerIconManager.SpinnerIconType.Load, this);
		}
		AssetBundleLoadLevelOperationBase loadOp = null;
		AsyncOperation async = null;
		StartTransitionUp(delegate
		{
			loadOp = AssetBundleManager.LoadLevelAsync(_sceneName.ToLowerInvariant(), _sceneName, false);
			loadOp.OnAsyncOperationStarted = delegate(AsyncOperation op)
			{
				async = op;
			};
		});
		while (loadOp == null || loadOp.MoveNext())
		{
			yield return null;
		}
		while (async == null || !async.isDone)
		{
			yield return null;
		}
		if (suppressor != null)
		{
			suppressor.Release();
		}
		bool bDown = false;
		StartTransitionDown(delegate
		{
			bDown = true;
		});
		while (!bDown)
		{
			yield return null;
		}
	}
}
