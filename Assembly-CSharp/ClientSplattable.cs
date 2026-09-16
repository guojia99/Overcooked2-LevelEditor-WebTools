using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientSplattable : ClientSynchroniserBase
{
	private Splattable m_splattable;

	private void Awake()
	{
		m_splattable = base.gameObject.RequireComponent<Splattable>();
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_splattable.m_splatPrefab[m_splattable.m_prefabIndex], OnHazardSpawned);
	}

	private void OnHazardSpawned(GameObject _object)
	{
		_object.AddComponent<StaticGridLocation>();
		GameUtils.TriggerAudio(GameOneShotAudioTag.Splat, base.gameObject.layer);
	}
}
