using LevelEditor;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientToastingForkCosmeticDecisions : ClientSynchroniserBase, IClientSurfacePlacementNotified
{
	private ToastingForkCosmeticDecisions m_cosmeticDecisions;

	private CookingHandler m_cookingHandler;

	private Dictionary<string, Vector3> m_originalPositions = new Dictionary<string, Vector3>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cosmeticDecisions = (ToastingForkCosmeticDecisions)synchronisedObject;
		m_cookingHandler = base.gameObject.RequireComponent<CookingHandler>();
	}

	public void OnSurfacePlacement(ClientAttachStation _station)
	{
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession() && _station != null && _station.gameObject != null)
		{
			CookingStation cookingStation = _station.gameObject.RequestComponent<CookingStation>();
			// patch
			//if (cookingStation != null && cookingStation.m_stationType == m_cookingHandler.m_stationType)
            MultiCookingStationTypes multiCookingStationTypes = m_cookingHandler.GetComponent<MultiCookingStationTypes>();
			if (cookingStation != null && (
				cookingStation.m_stationType == m_cookingHandler.m_stationType || 
				multiCookingStationTypes != null && multiCookingStationTypes.MatchType(cookingStation.m_stationType)))
            // patch
            {
				m_cosmeticDecisions.ApplyPositionOffsetToChilden(new Vector3(m_cosmeticDecisions.m_surfaceAttachedOffset, 0f, 0f), ref m_originalPositions);
			}
		}
	}

	public void OnSurfaceDeplacement(ClientAttachStation _station)
	{
		m_cosmeticDecisions.RestorePositionsToChildren(ref m_originalPositions);
		m_originalPositions.Clear();
	}
}
