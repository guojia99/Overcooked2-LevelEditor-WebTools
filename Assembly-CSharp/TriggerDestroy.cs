using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class TriggerDestroy : MonoBehaviour, ITriggerReceiver
{
	[SerializeField]
	public string m_trigger;

	public void OnTrigger(string _name)
	{
		if (_name == m_trigger)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(base.gameObject);
			if (entry == null)
			{
				Object.Destroy(base.gameObject);
			}
		}
	}
}
