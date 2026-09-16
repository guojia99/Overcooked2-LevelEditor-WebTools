using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Player/PlayerIDProvider")]
public class PlayerIDProvider : MonoBehaviour
{
	public static FastList<PlayerIDProvider> s_AllProviders = new FastList<PlayerIDProvider>();

	[SerializeField]
	private PlayerInputLookup.Player m_player;

	private TeamID m_teamID;

	public static GenericVoid OnPlayerIDProviderDestroyed;

	public void Awake()
	{
		s_AllProviders.Add(this);
	}

	public void OnDestroy()
	{
		s_AllProviders.Remove(this);
		if (OnPlayerIDProviderDestroyed != null)
		{
			OnPlayerIDProviderDestroyed();
		}
	}

	public void OverridePlayerId(PlayerInputLookup.Player _player)
	{
		m_player = _player;
	}

	public void AssignToTeam(TeamID _teamID)
	{
		m_teamID = _teamID;
	}

	public bool IsLocallyControlled()
	{
		return m_player != PlayerInputLookup.Player.Count;
	}

	public PlayerInputLookup.Player GetID()
	{
		return m_player;
	}

	public TeamID GetTeam()
	{
		return m_teamID;
	}
}
