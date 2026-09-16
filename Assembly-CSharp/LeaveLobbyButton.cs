using UnityEngine;

public class LeaveLobbyButton : MonoBehaviour
{
	[SerializeField]
	private GameObject flowGO;

	public void LeaveLobby()
	{
		ClientLobbyFlowController component = flowGO.GetComponent<ClientLobbyFlowController>();
		component.ShowLeaveDialog();
	}
}
