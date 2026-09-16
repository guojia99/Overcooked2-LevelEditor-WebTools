using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public interface INetworkEntitySpawner
	{
		GameObject SpawnEntity(int _id, Vector3 _position, Quaternion _rotation, ref List<VoidGeneric<GameObject>> callbacks);

		int GetSpawnableID(GameObject _object);

		GameObject AccessGameObject();
	}
}
