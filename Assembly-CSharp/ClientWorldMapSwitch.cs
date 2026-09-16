using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWorldMapSwitch : ClientSynchroniserBase, IClientMapSelectable
{
	private WorldMapSwitch m_baseObject;

	private bool m_bVisualsPressed;

	private WorldMapSwitchOptimizer m_MapOptimizer;

	protected override void OnDestroy()
	{
		if (m_MapOptimizer != null)
		{
			m_MapOptimizer.UnRegisterOnSwitchFlipBegin(UpdateVisuals);
			m_MapOptimizer.UnRegisterOnSwitchFlipEnd(UpdateVisuals);
			m_MapOptimizer.UnRegisterOnSwitchFlipEnd(RevealSwitch);
		}
		base.OnDestroy();
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (WorldMapSwitch)synchronisedObject;
		m_MapOptimizer = base.gameObject.RequireComponentRecursive<WorldMapSwitchOptimizer>();
		if (m_MapOptimizer != null)
		{
			m_MapOptimizer.RegisterOnSwitchFlipBegin(UpdateVisuals);
			m_MapOptimizer.RegisterOnSwitchFlipEnd(UpdateVisuals);
			m_MapOptimizer.RegisterOnSwitchFlipEnd(RevealSwitch);
		}
		UpdateVisuals();
	}

	public void AvatarEnteringSelectable(MapAvatarControls _avatar)
	{
	}

	public void AvatarLeavingSelectable(MapAvatarControls _avatar)
	{
	}

	public void SetVisualsToUnPressed()
	{
		if (m_bVisualsPressed)
		{
			m_bVisualsPressed = false;
			if (m_baseObject.m_PadMesh != null)
			{
				Vector3 position = m_baseObject.m_PadMesh.transform.position;
				position.y += m_baseObject.m_PadPressDistance;
				m_baseObject.m_PadMesh.transform.position = position;
			}
			if (m_baseObject.m_PadGlowMesh != null && m_baseObject.m_PadGlowMesh.material != null)
			{
				m_baseObject.m_PadGlowMesh.material.SetFloat("_Multiplier", 0f);
			}
		}
	}

	public void SetVisualsToPressed()
	{
		if (!m_bVisualsPressed)
		{
			m_bVisualsPressed = true;
			if (m_baseObject.m_PadMesh != null)
			{
				Vector3 position = m_baseObject.m_PadMesh.transform.position;
				position.y -= m_baseObject.m_PadPressDistance;
				m_baseObject.m_PadMesh.transform.position = position;
			}
			if (m_baseObject.m_PadGlowMesh != null && m_baseObject.m_PadGlowMesh.material != null)
			{
				m_baseObject.m_PadGlowMesh.material.SetFloat("_Multiplier", 1f);
			}
		}
	}

	private void UpdateVisuals()
	{
		if (m_baseObject.IsSwitchActivated())
		{
			SetVisualsToPressed();
		}
		else
		{
			SetVisualsToUnPressed();
		}
	}

	private void RevealSwitch()
	{
		if (m_baseObject.IsFlipped())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			int switchID = m_baseObject.m_switchOwnerData.m_switchMapNode.SwitchID;
			GameProgress.GameProgressData.SwitchState switchState = gameSession.Progress.GetSwitchState(switchID);
			if (switchState.SwitchId == -1)
			{
				gameSession.Progress.RecordSwitchRevealed(switchID);
			}
		}
	}
}
