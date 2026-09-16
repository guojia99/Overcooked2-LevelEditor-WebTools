using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CampsiteAnimatedDynamicTransition : AnimatedDynamicTransition
{
	[Space]
	[Header("Raft References")]
	[SerializeField]
	private Transform m_leavingRaftLayoutRoot;

	[SerializeField]
	private Collider m_leavingRaftRespawnCollider;

	[Space]
	[SerializeField]
	private Transform m_enteringRaftLayoutRoot;

	[Space]
	[Header("Lake References")]
	[SerializeField]
	private Animator m_lakeAnimator;

	private ServerRespawnCollider m_respawnCollider;

	private CallbackVoid m_raftLeftCallback = delegate
	{
	};

	private static int m_RaftLeavingHash = Animator.StringToHash("RaftsAreChanging");

	private static int m_AreRaftsInMotionHash = Animator.StringToHash("IsArtInMotion");

	protected override void Awake()
	{
		base.Awake();
	}

	public override void Setup(CallbackVoid _endTransitionCallback)
	{
		base.Setup(_endTransitionCallback);
		m_respawnCollider = base.gameObject.RequestComponent<ServerRespawnCollider>();
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_raftLeftCallback = (CallbackVoid)Delegate.Combine(m_raftLeftCallback, (CallbackVoid)delegate
			{
				DestroyAndRespawnObjects(m_leavingRaftLayoutRoot, m_leavingRaftRespawnCollider);
			});
		}
	}

	private void DestroyAndRespawnObjects(Transform _attachmentRoot, Collider _looseDestroyCollider)
	{
		Bounds bounds = _looseDestroyCollider.bounds;
		GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			IRespawnBehaviour[] componentsInChildren = gameObject.GetComponentsInChildren<IRespawnBehaviour>(true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (componentsInChildren[j] == null)
				{
					continue;
				}
				MonoBehaviour monoBehaviour = componentsInChildren[j] as MonoBehaviour;
				if (monoBehaviour == null || monoBehaviour.gameObject == null)
				{
					continue;
				}
				GameObject gameObject2 = monoBehaviour.gameObject;
				if (!(gameObject2 == null) && bounds.Contains(gameObject2.transform.position))
				{
					IAttachment attachment = gameObject2.RequestInterface<IAttachment>();
					if (attachment == null || !attachment.IsAttached())
					{
						ServerPlayerRespawnManager.KillOrRespawn(gameObject2, m_respawnCollider);
					}
				}
			}
		}
		ServerWashingStation[] array = _attachmentRoot.gameObject.RequestComponentsRecursive<ServerWashingStation>();
		for (int k = 0; k < array.Length; k++)
		{
			array[k].WashAllPlates();
		}
		ServerAttachStation[] array2 = _attachmentRoot.gameObject.RequestComponentsRecursive<ServerAttachStation>();
		for (int l = 0; l < array2.Length; l++)
		{
			if (array2[l].HasItem())
			{
				GameObject gameObject3 = array2[l].TakeItem();
				if (gameObject3 != null)
				{
					ServerPlayerRespawnManager.KillOrRespawn(gameObject3, m_respawnCollider);
				}
			}
		}
	}

	public override IEnumerator Run()
	{
		if (m_lakeAnimator != null)
		{
			m_lakeAnimator.SetBool(m_RaftLeavingHash, true);
			yield return null;
			while (m_lakeAnimator.GetBool(m_AreRaftsInMotionHash))
			{
				yield return null;
			}
		}
		IEnumerator transitionRoutine = _003CRun_003E__BaseCallProxy0();
		while (transitionRoutine.MoveNext())
		{
			yield return transitionRoutine.Current;
		}
		m_raftLeftCallback();
		if (m_lakeAnimator != null)
		{
			m_lakeAnimator.SetBool(m_RaftLeavingHash, false);
			yield return null;
			while (m_lakeAnimator.GetBool(m_AreRaftsInMotionHash))
			{
				yield return null;
			}
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
