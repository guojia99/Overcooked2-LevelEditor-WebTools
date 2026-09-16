using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWorldMapSwitch : ServerSynchroniserBase, IServerMapSelectable
{
	private WorldMapSwitch m_baseObject;

	private WorldMapFlipperBase m_flipper;

	private ServerSwitchMapNode m_ServerSwitchMapNode;

	private bool m_bVisualsPressed;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (WorldMapSwitch)synchronisedObject;
		if (null != m_baseObject.m_switchOwnerData.m_switchMapNode)
		{
			m_ServerSwitchMapNode = m_baseObject.m_switchOwnerData.m_switchMapNode.gameObject.RequestComponent<ServerSwitchMapNode>();
		}
		m_flipper = base.gameObject.RequireComponent<WorldMapFlipperBase>();
	}

	private bool IsIdle()
	{
		return m_flipper == null || (m_flipper.IsFlipped() && m_flipper.IsFinishedFlipping());
	}

	public void AvatarEnteringSelectable(MapAvatarControls _avatar)
	{
		if (IsIdle() && m_baseObject.CanBePressed() && m_ServerSwitchMapNode != null)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.WorldMapRampButton, base.gameObject.layer);
			m_ServerSwitchMapNode.OnSwitchPressed(_avatar, m_baseObject);
		}
	}

	public void AvatarLeavingSelectable(MapAvatarControls _avatar)
	{
	}

	public void OnSelected(MapAvatarControls _avatar)
	{
	}
}
