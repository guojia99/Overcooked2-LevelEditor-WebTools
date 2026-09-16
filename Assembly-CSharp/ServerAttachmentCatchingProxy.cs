using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAttachmentCatchingProxy : ServerSynchroniserBase
{
	private TriggerRecorder m_triggerRecorder;

	private IHandleCatch[] m_iHandleCatches = new IHandleCatch[0];

	private IHandleCatch m_iHandleCatchReferree;

	private GenericVoid<GameObject, Vector2> m_uncatchableItemCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_iHandleCatches = base.gameObject.RequestInterfaces<IHandleCatch>();
		m_triggerRecorder = base.gameObject.RequestComponent<TriggerRecorder>();
		if (m_triggerRecorder == null)
		{
			m_triggerRecorder = base.gameObject.AddComponent<TriggerRecorder>();
		}
	}

	public void RegisterUncatchableItemCallback(GenericVoid<GameObject, Vector2> _callback)
	{
		m_uncatchableItemCallback = (GenericVoid<GameObject, Vector2>)Delegate.Combine(m_uncatchableItemCallback, _callback);
	}

	public void UnRegisterUncatchableItemCallback(GenericVoid<GameObject, Vector2> _callback)
	{
		m_uncatchableItemCallback = (GenericVoid<GameObject, Vector2>)Delegate.Remove(m_uncatchableItemCallback, _callback);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (!(m_triggerRecorder != null))
		{
			return;
		}
		List<Collider> recentCollisions = m_triggerRecorder.GetRecentCollisions();
		for (int i = 0; i < recentCollisions.Count; i++)
		{
			if (!(recentCollisions[i] == null) && recentCollisions[i].enabled && !(recentCollisions[i].gameObject == null) && recentCollisions[i].gameObject.activeSelf)
			{
				IAttachment component = recentCollisions[i].gameObject.GetComponent<IAttachment>();
				if (component != null && !component.IsAttached())
				{
					AttemptToCatch(component.AccessGameObject());
				}
			}
		}
	}

	private void AttemptToCatch(GameObject _object)
	{
		ICatchable catchable = _object.RequestInterface<ICatchable>();
		if (catchable != null)
		{
			IHandleCatch controllingCatchingHandler = GetControllingCatchingHandler();
			if (controllingCatchingHandler != null)
			{
				Vector2 directionXZ = (_object.transform.position - base.transform.position).SafeNormalised(base.transform.forward).XZ();
				if (controllingCatchingHandler.CanHandleCatch(catchable, directionXZ))
				{
					controllingCatchingHandler.HandleCatch(catchable, directionXZ);
				}
			}
		}
		else
		{
			Vector2 param = (_object.transform.position - base.transform.position).SafeNormalised(base.transform.forward).XZ();
			if (m_uncatchableItemCallback != null)
			{
				m_uncatchableItemCallback(_object, param);
			}
		}
	}

	public void SetHandleCatchingReferree(IHandleCatch _iHandleCatch)
	{
		m_iHandleCatchReferree = _iHandleCatch;
	}

	public IHandleCatch GetHandleCatchingReferree()
	{
		return m_iHandleCatchReferree;
	}

	public IHandleCatch GetControllingCatchingHandler()
	{
		if (m_iHandleCatchReferree != null)
		{
			return m_iHandleCatchReferree;
		}
		IHandleCatch handleCatch = null;
		for (int i = 0; i < m_iHandleCatches.Length; i++)
		{
			IHandleCatch handleCatch2 = m_iHandleCatches[i];
			if ((!(handleCatch2 is MonoBehaviour) || (handleCatch2 as MonoBehaviour).enabled) && (handleCatch == null || handleCatch2.GetCatchingPriority() > handleCatch.GetCatchingPriority()))
			{
				handleCatch = handleCatch2;
			}
		}
		return handleCatch;
	}
}
