using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWorldMapInfoPopup : ServerSynchroniserBase
{
	private WorldMapInfoPopup m_popupInfo;

	private WorldMapInfoPopupMessage m_message = new WorldMapInfoPopupMessage();

	private ILogicalButton m_continueButton;

	public override EntityType GetEntityType()
	{
		return EntityType.WorldPopup;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_popupInfo = synchronisedObject as WorldMapInfoPopup;
		m_continueButton = PlayerInputLookup.GetButton(m_popupInfo.m_button, PlayerInputLookup.Player.One);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		StartCoroutine(PopupRoutine());
	}

	public IEnumerator PopupRoutine()
	{
		IEnumerator wait = null;
		if (m_popupInfo.m_autoCancel)
		{
			wait = CoroutineUtils.TimerRoutine(m_popupInfo.m_autoCancelTime, base.gameObject.layer);
		}
		while ((wait == null || wait.MoveNext()) && !m_continueButton.JustPressed())
		{
			yield return null;
		}
		SendServerEvent(m_message);
	}
}
