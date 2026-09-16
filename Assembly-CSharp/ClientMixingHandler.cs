using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMixingHandler : ClientSynchroniserBase, IClientMixable
{
	public StateChanged m_stateChangedCallback = delegate
	{
	};

	private MixingHandler m_mixingHandler;

	private IClientMixingNotifed[] m_IMixingNotified;

	private CookingUIController m_progressUI;

	private float m_hideAfterTime = float.MinValue;

	private CookingUIController.State m_cachedMixingState;

	private float m_cachedMixingProgress = -1f;

	public float AccessMixingTime
	{
		get
		{
			return GetMixingHandler().m_mixingTime;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		GameObject gameObject = GameUtils.InstantiateUIController(GetMixingHandler().m_progressUIPrefab.gameObject, "HoverIconCanvas");
		if (gameObject != null)
		{
			m_progressUI = gameObject.GetComponent<CookingUIController>();
			m_progressUI.SetFollowTransform(NetworkUtils.FindVisualRoot(base.gameObject), Vector3.zero);
		}
		m_IMixingNotified = base.gameObject.RequestInterfacesRecursive<IClientMixingNotifed>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.MixingState;
	}

	public override void ApplyServerUpdate(Serialisable serialisable)
	{
		MixingHandler mixingHandler = GetMixingHandler();
		MixingStateMessage mixingStateMessage = (MixingStateMessage)serialisable;
		bool flag = false;
		if (m_cachedMixingProgress != mixingStateMessage.m_mixingProgress)
		{
			m_cachedMixingProgress = mixingStateMessage.m_mixingProgress;
			flag = true;
		}
		if (m_cachedMixingState != mixingStateMessage.m_mixingState)
		{
			m_cachedMixingState = mixingStateMessage.m_mixingState;
			flag = true;
			m_stateChangedCallback(m_cachedMixingState);
		}
		if (!flag)
		{
			return;
		}
		m_IMixingNotified = base.gameObject.RequestInterfacesRecursive<IClientMixingNotifed>();
		float newProp = mixingStateMessage.m_mixingProgress / mixingHandler.m_mixingTime;
		for (int i = 0; i < m_IMixingNotified.Length; i++)
		{
			IClientMixingNotifed clientMixingNotifed = m_IMixingNotified[i];
			clientMixingNotifed.OnMixingPropChanged(newProp);
		}
		if (mixingStateMessage.m_mixingProgress > 0.001f)
		{
			if (m_progressUI != null)
			{
				m_progressUI.gameObject.SetActive(true);
				m_progressUI.SetProgress(mixingStateMessage.m_mixingProgress / mixingHandler.m_mixingTime);
				float b = Mathf.Clamp(2f * mixingHandler.m_mixingTime - 0.5f, mixingHandler.m_mixingTime, 2f * mixingHandler.m_mixingTime);
				m_progressUI.SetOverDoingAmount(MathUtils.ClampedRemap(mixingStateMessage.m_mixingProgress, mixingHandler.m_mixingTime, b, 0f, 1f));
				m_progressUI.SetState(mixingStateMessage.m_mixingState);
			}
			MixableContainer mixableContainer = base.gameObject.RequestComponent<MixableContainer>();
			ClientHandlePlacementReferral clientHandlePlacementReferral = base.gameObject.RequestComponent<ClientHandlePlacementReferral>();
			ClientAttachStation clientAttachStation = clientHandlePlacementReferral.GetHandlePlacementReferree() as ClientAttachStation;
			bool flag2 = false;
			if (clientAttachStation != null && mixableContainer != null && clientAttachStation.gameObject.RequestComponent<MixingStation>() != null)
			{
				flag2 = true;
			}
			if (flag2)
			{
				m_progressUI.SetState(mixingStateMessage.m_mixingState);
				for (int j = 0; j < m_IMixingNotified.Length; j++)
				{
					IClientMixingNotifed clientMixingNotifed2 = m_IMixingNotified[j];
					clientMixingNotifed2.OnMixingStarted();
				}
				m_hideAfterTime = 1f;
			}
			else
			{
				m_hideAfterTime = 0f;
				if (m_cachedMixingState != CookingUIController.State.Progressing && m_progressUI != null)
				{
					m_progressUI.SetState(CookingUIController.State.Idle);
				}
			}
		}
		else
		{
			if (m_progressUI != null)
			{
				m_progressUI.gameObject.SetActive(false);
			}
			m_hideAfterTime = float.MinValue;
			for (int k = 0; k < m_IMixingNotified.Length; k++)
			{
				IClientMixingNotifed clientMixingNotifed3 = m_IMixingNotified[k];
				clientMixingNotifed3.OnMixingFinished();
			}
		}
	}

	public GameLoopingAudioTag GetMixingSoundTag()
	{
		return GetMixingHandler().m_mixingType.m_sizzleSound;
	}

	public MixedCompositeOrderNode.MixingProgress GetMixedOrderState()
	{
		return GetMixingHandler().GetMixedOrderState(m_cachedMixingProgress);
	}

	public float GetMixingProgress()
	{
		return m_cachedMixingProgress;
	}

	public MixingHandler GetMixingHandler()
	{
		if (m_mixingHandler == null)
		{
			m_mixingHandler = base.gameObject.RequireComponent<MixingHandler>();
		}
		return m_mixingHandler;
	}

	private void UpdateCosmetics()
	{
	}

	public override void UpdateSynchronising()
	{
		float hideAfterTime = m_hideAfterTime;
		m_hideAfterTime -= TimeManager.GetDeltaTime(base.gameObject);
		if (hideAfterTime >= 0f && m_hideAfterTime < 0f)
		{
			if (m_cachedMixingState == CookingUIController.State.OverDoing && m_progressUI != null)
			{
				m_progressUI.SetState(CookingUIController.State.Idle);
			}
			for (int i = 0; i < m_IMixingNotified.Length; i++)
			{
				IClientMixingNotifed clientMixingNotifed = m_IMixingNotified[i];
				clientMixingNotifed.OnMixingFinished();
			}
		}
		UpdateCosmetics();
	}

	public bool IsMixed()
	{
		return m_cachedMixingProgress >= AccessMixingTime;
	}

	public bool IsOverMixed()
	{
		return m_cachedMixingProgress > 2f * AccessMixingTime;
	}
}
