using UnityEngine;

public class ServerTerminal : ServerSessionInteractable
{
	private class UserSession : SessionBase
	{
		private ServerPilotMovement m_puppetObject;

		public UserSession(ServerTerminal _self, GameObject _avatar, PilotMovement _pilotableObject)
			: base(_self, _avatar)
		{
			ServerPilotMovement serverPilotMovement = _pilotableObject.gameObject.RequestComponent<ServerPilotMovement>();
			serverPilotMovement.AssignPlayer(base.PlayerControls.ControlScheme);
			m_puppetObject = serverPilotMovement;
		}

		public override void OnSessionEnded()
		{
			m_puppetObject.AssignPlayer(null);
			base.OnSessionEnded();
		}
	}

	private Terminal m_terminal;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_terminal = (Terminal)synchronisedObject;
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		return new UserSession(this, _interacter, m_terminal.m_pilotableObject);
	}
}
