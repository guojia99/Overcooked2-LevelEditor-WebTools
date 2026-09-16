using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMixingStation : ClientSynchroniserBase
{
	private MixingStation m_MixingStation;

	private ClientAttachStation m_AttachStation;

	private IClientMixable m_mixable;

	private bool m_isTurnedOn;

	private Animator m_Animator;

	private const string c_AnimatorMixingVar = "On";

	private static int s_AnimatorMixingHash;

	private GameLoopingAudioTag? m_activeLoopingAudio;

	public override EntityType GetEntityType()
	{
		return EntityType.MixingStation;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		ApplyStateSyncMessage(serialisable);
	}

	protected virtual void ApplyStateSyncMessage(Serialisable serialisable)
	{
		MixingStationMessage mixingStationMessage = (MixingStationMessage)serialisable;
		m_isTurnedOn = mixingStationMessage.m_isTurnedOn;
		UpdateAnimations();
	}

	private void Awake()
	{
		m_MixingStation = GetComponent<MixingStation>();
		m_AttachStation = GetComponent<ClientAttachStation>();
		m_AttachStation.RegisterAllowItemPlacement(CanAddItem);
		m_AttachStation.RegisterOnItemAdded(OnItemAdded);
		m_AttachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_Animator = GetComponentInChildren<Animator>();
		if (s_AnimatorMixingHash == 0)
		{
			s_AnimatorMixingHash = Animator.StringToHash("On");
		}
	}

	protected override void OnDestroy()
	{
		m_AttachStation.UnregisterAllowItemPlacement(CanAddItem);
		m_AttachStation.UnregisterOnItemAdded(OnItemAdded);
		m_AttachStation.UnregisterOnItemRemoved(OnItemRemoved);
		base.OnDestroy();
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		switch (_context.m_source)
		{
		case PlacementContext.Source.Game:
			return true;
		case PlacementContext.Source.Player:
		{
			if (!m_MixingStation.m_bAttachRestrictions)
			{
				return true;
			}
			IClientMixable clientMixable = _object.RequestInterface<IClientMixable>();
			if (clientMixable == null)
			{
				return false;
			}
			IClientCookable clientCookable = _object.RequestInterface<IClientCookable>();
			if (clientCookable != null)
			{
				IIngredientContents ingredientContents = _object.RequestInterface<IIngredientContents>();
				if (ingredientContents != null && ingredientContents.HasContents() && (clientCookable.GetCookedOrderState() != CookedCompositeOrderNode.CookingProgress.Raw || clientCookable.AccessCookingTime > 0f))
				{
					return false;
				}
			}
			return true;
		}
		default:
			return false;
		}
	}

	private void OnItemAdded(IClientAttachment _iHoldable)
	{
		m_mixable = _iHoldable.AccessGameObject().RequestInterface<IClientMixable>();
		UpdateAnimations();
	}

	private void OnItemRemoved(IClientAttachment _iHoldable)
	{
		m_isTurnedOn = false;
		m_mixable = null;
		UpdateAnimations();
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_isTurnedOn)
		{
			if (!m_activeLoopingAudio.HasValue && m_mixable != null)
			{
				m_activeLoopingAudio = m_mixable.GetMixingSoundTag();
				GameUtils.StartAudio(m_activeLoopingAudio.Value, this, base.gameObject.layer);
			}
		}
		else if (m_activeLoopingAudio.HasValue)
		{
			GameUtils.StopAudio(m_activeLoopingAudio.Value, this);
			m_activeLoopingAudio = null;
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_activeLoopingAudio.HasValue)
		{
			GameUtils.StopAudio(m_activeLoopingAudio.Value, this);
			m_activeLoopingAudio = null;
		}
	}

	private void UpdateAnimations()
	{
		if (m_Animator != null)
		{
			m_Animator.SetBool(s_AnimatorMixingHash, m_isTurnedOn);
		}
	}
}
