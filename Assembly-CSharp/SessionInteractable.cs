using UnityEngine;

public abstract class SessionInteractable : MonoBehaviour
{
	[SerializeField]
	public string m_onSessionBegun = string.Empty;

	[SerializeField]
	public string m_onSesionEnded = string.Empty;
}
