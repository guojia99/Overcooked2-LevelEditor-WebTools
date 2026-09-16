using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BeachAnimatedDynamicTransition : AnimatedDynamicTransition
{
	[Space]
	[SerializeField]
	private Transform m_leavingCounterLayoutRoot;

	[Space]
	[Header("Tide References")]
	[SerializeField]
	private Animator m_tideAnimator;

	[SerializeField]
	private Collider m_tideCollider;

	private CallbackVoid m_tideInCallback = delegate
	{
	};

	private static int m_TideInScene = Animator.StringToHash("TideInScene");

	private static int m_IsTideTransitioning = Animator.StringToHash("IsTideTransitioning");

	public override void Setup(CallbackVoid _endTransitionCallback)
	{
		base.Setup(_endTransitionCallback);
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_tideInCallback = (CallbackVoid)Delegate.Combine(m_tideInCallback, (CallbackVoid)delegate
			{
				DestroyAndRespawnObjects(m_leavingCounterLayoutRoot, m_tideCollider);
			});
		}
	}

	private void DestroyAndRespawnObjects(Transform _attachmentRoot, Collider _looseDestroyCollider)
	{
		Bounds bounds = _looseDestroyCollider.bounds;
		GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			IAttachment[] componentsInChildren = gameObject.GetComponentsInChildren<IAttachment>();
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (componentsInChildren[j] != null && !(componentsInChildren[j].AccessGameObject() == null) && !componentsInChildren[j].IsAttached())
				{
					GameObject gameObject2 = componentsInChildren[j].AccessGameObject();
					if (!(gameObject2 == null) && bounds.Contains(gameObject2.transform.position))
					{
						ServerPlayerRespawnManager.KillOrRespawn(componentsInChildren[j].AccessGameObject(), null);
					}
				}
			}
		}
		ServerAttachStation[] array = _attachmentRoot.gameObject.RequestComponentsRecursive<ServerAttachStation>();
		for (int k = 0; k < array.Length; k++)
		{
			if (array[k].HasItem())
			{
				GameObject gameObject3 = array[k].TakeItem();
				if (gameObject3 != null)
				{
					ServerPlayerRespawnManager.KillOrRespawn(gameObject3, null);
				}
			}
		}
	}

	public override IEnumerator Run()
	{
		m_tideAnimator.SetBool(m_TideInScene, true);
		yield return null;
		while (m_tideAnimator.GetBool(m_IsTideTransitioning))
		{
			yield return null;
		}
		m_tideInCallback();
		IEnumerator transitionRoutine = _003CRun_003E__BaseCallProxy0();
		while (transitionRoutine.MoveNext())
		{
			yield return transitionRoutine.Current;
		}
		m_tideAnimator.SetBool(m_TideInScene, false);
		yield return null;
		while (m_tideAnimator.GetBool(m_IsTideTransitioning))
		{
			yield return null;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
