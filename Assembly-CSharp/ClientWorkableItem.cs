using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWorkableItem : ClientSynchroniserBase, IClientSurfacePlacementNotified
{
	private WorkableItem m_workable;

	private ClientIngredientMeshVisibility m_ingredientMeshVisibility;

	private Animator m_animator;

	private ProgressUIController m_guibar;

	private bool m_onWorkstation;

	private int m_progress;

	private int m_subProgress;

	private int m_chopsPerSlice = 1;

	private IngredientMeshVisibility.VisState? m_prevVisState;

	public override EntityType GetEntityType()
	{
		return EntityType.Workable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_workable = (WorkableItem)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_workable.GetNextPrefab());
		m_ingredientMeshVisibility = base.gameObject.RequestComponent<ClientIngredientMeshVisibility>();
		GameObject gameObject = GameUtils.InstantiateUIController(m_workable.m_progressUIPrefab.gameObject, "HoverIconCanvas");
		m_guibar = gameObject.GetComponent<ProgressUIController>();
		m_guibar.AutoHide = false;
		m_guibar.SetFollowTransform(base.transform, Vector3.zero);
		m_guibar.gameObject.SetActive(false);
		m_chopsPerSlice = m_workable.GetChopTimeMultiplier(ClientUserSystem.m_Users.Count);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_chopsPerSlice = m_workable.GetChopTimeMultiplier(ClientUserSystem.m_Users.Count);
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		WorkableMessage workableMessage = (WorkableMessage)serialisable;
		m_onWorkstation = workableMessage.m_onWorkstation;
		m_progress = workableMessage.m_progress;
		m_subProgress = workableMessage.m_subProgress;
		UpdateProgressVisuals();
	}

	private void Awake()
	{
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
		if (m_animator != null)
		{
			m_animator.enabled = false;
		}
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected override void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (m_guibar != null)
		{
			Object.Destroy(m_guibar.gameObject);
		}
		base.OnDestroy();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_subProgress > 0 || m_progress > 0)
		{
			m_guibar.gameObject.SetActive(true);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_guibar != null)
		{
			m_guibar.gameObject.SetActive(false);
		}
	}

	public void OnSurfacePlacement(ClientAttachStation _station)
	{
		if (_station.gameObject.RequestComponent<Workstation>() != null && m_animator != null)
		{
			m_animator.enabled = true;
		}
	}

	public void OnSurfaceDeplacement(ClientAttachStation _station)
	{
		if (GetProgress() <= 0f && m_animator != null)
		{
			m_animator.enabled = false;
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_workable == null)
		{
			return;
		}
		if (m_onWorkstation)
		{
			SetVisState(IngredientMeshVisibility.VisState.Working);
			return;
		}
		if (m_guibar != null)
		{
			m_guibar.gameObject.SetActive(false);
		}
		if ((float)m_progress > 0.5f * (float)(m_workable.m_stages - 1))
		{
			SetVisState(IngredientMeshVisibility.VisState.Working);
		}
		else
		{
			SetVisState(IngredientMeshVisibility.VisState.Whole);
		}
	}

	protected void SetVisState(IngredientMeshVisibility.VisState _state)
	{
		if (!m_prevVisState.HasValue || m_prevVisState.Value != _state)
		{
			if (m_ingredientMeshVisibility != null)
			{
				m_ingredientMeshVisibility.SetVisState(_state);
			}
			m_prevVisState = _state;
		}
	}

	public float GetProgress()
	{
		return (float)m_progress / (float)Mathf.Max(m_workable.m_stages - 1, 1);
	}

	public bool HasFinished()
	{
		return m_progress == m_workable.m_stages - 1;
	}

	public void DoWork(ClientAttachStation _station, GameObject _worker)
	{
		m_subProgress++;
		UpdateProgressVisuals();
		if (m_subProgress >= m_chopsPerSlice && !GameUtils.GetDebugConfig().m_infiniteChopping)
		{
			m_subProgress = 0;
			DoWork(_station, _worker, 1);
		}
	}

	public void DoWork(ClientAttachStation _station, GameObject _worker, int _progress)
	{
		if (!HasFinished())
		{
			m_subProgress = 0;
			m_progress = Mathf.Min(m_progress + _progress, m_workable.m_stages - 1);
			if (!HasFinished())
			{
				UpdateProgressVisuals();
			}
		}
	}

	private void UpdateProgressVisuals()
	{
		if (m_guibar != null)
		{
			m_guibar.SetProgress((float)(m_progress * m_chopsPerSlice + m_subProgress) / (float)(m_chopsPerSlice * m_workable.m_stages - 1));
			m_guibar.gameObject.SetActive(m_subProgress > 0 || m_progress > 0);
		}
		if (m_animator != null)
		{
			m_animator.SetInteger(m_workable.m_iAnimationVariable, m_progress);
		}
	}
}
