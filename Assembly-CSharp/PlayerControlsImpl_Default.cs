using UnityEngine;

public class PlayerControlsImpl_Default : MonoBehaviour, IPlayerControlsImpl
{
	public ServerPlayerControlsImpl_Default m_serverImpl;

	public ClientPlayerControlsImpl_Default m_clientImpl;

	public void RegisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_clientImpl.RegisterForInteractTrigger(_callback);
	}

	public void UnregisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_clientImpl.UnregisterForInteractTrigger(_callback);
	}

	public void RegisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_clientImpl.RegisterForThrowTrigger(_callback);
	}

	public void UnregisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_clientImpl.UnregisterForThrowTrigger(_callback);
	}

	public void RegisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_clientImpl.RegisterForFallingTrigger(_callback);
	}

	public void UnregisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_clientImpl.UnregisterForFallingTrigger(_callback);
	}

	public void NotifySessionInteractionStarted(ClientSessionInteractable _interaction)
	{
		m_clientImpl.NotifySessionInteractionStarted(_interaction);
	}

	public void NotifySessionInteractionEnded(ClientSessionInteractable _interaction)
	{
		m_clientImpl.NotifySessionInteractionEnded(_interaction);
	}

	public void OnCollisionEnter(Collision _collision)
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.OnCollisionEnter(_collision);
		}
	}

	public ClientInteractable GetCurrentlyInteracting()
	{
		if (m_clientImpl != null)
		{
			return m_clientImpl.GetCurrentlyInteracting();
		}
		return null;
	}

	public void Enable()
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.Enable();
		}
		if (m_serverImpl != null)
		{
			m_serverImpl.Enable();
		}
	}

	public void Disable()
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.Disable();
		}
		if (m_serverImpl != null)
		{
			m_serverImpl.Disable();
		}
	}

	public void Update_Impl()
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.Update_Impl();
		}
		if (m_serverImpl != null)
		{
			m_serverImpl.Update_Impl();
		}
	}

	public void Init(PlayerControls _controls)
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.Init(_controls);
		}
		if (m_serverImpl != null)
		{
			m_serverImpl.Init(_controls);
		}
	}

	public void SetPlayerControlSchemeData(PlayerControls.ControlSchemeData _controlScheme)
	{
		if (m_clientImpl != null)
		{
			m_clientImpl.SetPlayerControlSchemeData(_controlScheme);
		}
		if (m_serverImpl != null)
		{
			m_serverImpl.SetPlayerControlSchemeData(_controlScheme);
		}
	}
}
