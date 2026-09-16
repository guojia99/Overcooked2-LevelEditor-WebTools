using Team17.Online.Multiplayer.Messaging;

public class ClientCoalScuttleCosmeticDecisions : ClientSynchroniserBase
{
	public void OnPickupItem()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Coal_Collect, base.gameObject.layer);
	}
}
