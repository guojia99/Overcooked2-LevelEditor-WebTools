using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraveyardCountersDynamicTransition : DynamicTransitionBase
{
	[SerializeField]
	private GameObject[] m_countersGoingOut;

	[SerializeField]
	private float m_waitTime;

	[SerializeField]
	private GameObject[] m_countersComingIn;

	[SerializeField]
	private bool m_respawnObjects = true;

	[Space]
	[SerializeField]
	private string m_goingOutTrigger = string.Empty;

	[SerializeField]
	private string m_comingInTrigger = string.Empty;

	private AnimatedDynamicTransition m_animatedTransition;

	private List<Animator> m_outAnimators = new List<Animator>();

	private List<Animator> m_inAnimators = new List<Animator>();

	private static List<GameObject> ms_counterAttachments = new List<GameObject>();

	private GenericVoid<List<Animator>> m_countersLeavingCallback = delegate
	{
	};

	private GenericVoid<List<Animator>> m_countersEnteringCallback = delegate
	{
	};

	private GenericVoid<List<Animator>> m_countersOffscreenCallback = delegate
	{
	};

	private CallbackVoid m_endTransitionCallback = delegate
	{
	};

	private static int m_InScene = Animator.StringToHash("InScene");

	private static int m_IsTransitioning = Animator.StringToHash("IsTransitioning");

	private void OnDestroy()
	{
		ms_counterAttachments.Clear();
	}

	public override void Setup(CallbackVoid _endTransitionCallback)
	{
		m_endTransitionCallback = _endTransitionCallback;
		m_animatedTransition = base.gameObject.RequestComponent<AnimatedDynamicTransition>();
		m_outAnimators.Clear();
		m_inAnimators.Clear();
		for (int i = 0; i < m_countersGoingOut.Length; i++)
		{
			Animator[] array = m_countersGoingOut[i].RequestComponentsRecursive<Animator>();
			foreach (Animator animator in array)
			{
				if (animator.transform.name.Equals("Counters"))
				{
					m_outAnimators.Add(animator);
				}
			}
		}
		for (int k = 0; k < m_countersComingIn.Length; k++)
		{
			Animator[] array2 = m_countersComingIn[k].RequestComponentsRecursive<Animator>();
			foreach (Animator animator2 in array2)
			{
				if (animator2.transform.name.Equals("Counters"))
				{
					m_inAnimators.Add(animator2);
				}
			}
		}
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			m_countersLeavingCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersLeavingCallback, (GenericVoid<List<Animator>>)delegate
			{
				base.gameObject.SendTrigger(m_goingOutTrigger);
			});
			m_countersEnteringCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersEnteringCallback, (GenericVoid<List<Animator>>)delegate
			{
				base.gameObject.SendTrigger(m_comingInTrigger);
			});
			m_countersOffscreenCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersOffscreenCallback, (GenericVoid<List<Animator>>)delegate(List<Animator> _animators)
			{
				for (int m = 0; m < _animators.Count; m++)
				{
					RespawnAllUtensils(_animators[m].gameObject);
				}
			});
			m_countersEnteringCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersEnteringCallback, (GenericVoid<List<Animator>>)delegate(List<Animator> _animators)
			{
				for (int m = 0; m < _animators.Count; m++)
				{
					SetupAllUtensils(_animators[m].gameObject);
				}
			});
			return;
		}
		m_countersEnteringCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersEnteringCallback, (GenericVoid<List<Animator>>)delegate(List<Animator> _animators)
		{
			for (int m = 0; m < _animators.Count; m++)
			{
				SetupAllUtensils(_animators[m].gameObject);
			}
		});
		m_countersOffscreenCallback = (GenericVoid<List<Animator>>)Delegate.Combine(m_countersOffscreenCallback, (GenericVoid<List<Animator>>)delegate(List<Animator> _animators)
		{
			for (int m = 0; m < _animators.Count; m++)
			{
				RespawnAllUtensils_Client(_animators[m].gameObject);
			}
		});
	}

	private void SetupAllUtensils(GameObject _root)
	{
		IRespawnBehaviour[] array = _root.RequestInterfacesRecursive<IRespawnBehaviour>();
		for (int i = 0; i < array.Length; i++)
		{
			ms_counterAttachments.Add(((MonoBehaviour)array[i]).gameObject);
		}
	}

	private void RespawnAllUtensils(GameObject _root)
	{
		IRespawnBehaviour[] array = _root.RequestInterfacesRecursive<IRespawnBehaviour>();
		for (int i = 0; i < array.Length; i++)
		{
			GameObject gameObject = ((MonoBehaviour)array[i]).gameObject;
			if (!m_respawnObjects || ms_counterAttachments.IndexOf(gameObject) >= 0)
			{
				gameObject.SetActive(false);
			}
			else
			{
				ServerPlayerRespawnManager.KillOrRespawn(gameObject, null);
			}
		}
	}

	private void RespawnAllUtensils_Client(GameObject _root)
	{
		IRespawnBehaviour[] array = _root.RequestInterfacesRecursive<IRespawnBehaviour>();
		for (int i = 0; i < array.Length; i++)
		{
			GameObject gameObject = ((MonoBehaviour)array[i]).gameObject;
			if (!m_respawnObjects || ms_counterAttachments.IndexOf(gameObject) >= 0)
			{
				gameObject.SetActive(false);
			}
		}
	}

	public override IEnumerator Run()
	{
		for (int i = 0; i < m_outAnimators.Count; i++)
		{
			m_outAnimators[i].SetBool(m_InScene, false);
		}
		m_countersLeavingCallback(m_outAnimators);
		yield return null;
		while (m_outAnimators.Exists((Animator x) => x.GetBool(m_IsTransitioning)))
		{
			yield return null;
		}
		m_countersOffscreenCallback(m_outAnimators);
		IEnumerator timerRoutine = CoroutineUtils.TimerRoutine(m_waitTime, base.gameObject.layer);
		while (timerRoutine.MoveNext())
		{
			yield return null;
		}
		if (m_animatedTransition != null)
		{
			m_animatedTransition.Setup(delegate
			{
			});
			IEnumerator subRoutine = m_animatedTransition.Run();
			while (subRoutine.MoveNext())
			{
				yield return subRoutine.Current;
			}
			timerRoutine = CoroutineUtils.TimerRoutine(m_waitTime, base.gameObject.layer);
			while (timerRoutine.MoveNext())
			{
				yield return null;
			}
		}
		for (int num = 0; num < m_inAnimators.Count; num++)
		{
			m_inAnimators[num].SetBool(m_InScene, true);
		}
		m_countersEnteringCallback(m_inAnimators);
		yield return null;
		while (m_inAnimators.Exists((Animator x) => !x.GetBool(m_IsTransitioning)))
		{
			yield return null;
		}
		Shutdown();
	}

	private void Shutdown()
	{
		m_endTransitionCallback();
	}
}
