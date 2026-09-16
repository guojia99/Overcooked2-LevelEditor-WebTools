using System;
using System.Collections.Generic;
using UnityEngine;

public class EditorIDStore : MonoBehaviour
{
	[Serializable]
	private class IDMap
	{
		public uint id;

		public GameObject gameObject;

		public IDMap(GameObject _object, uint _id)
		{
			gameObject = _object;
			id = _id;
		}
	}

	[SerializeField]
	private List<IDMap> m_allAssignedIDs = new List<IDMap>();

	[SerializeField]
	private uint m_maxAssignedID;

	public uint GetIDForObject(GameObject _object)
	{
		uint result = 0u;
		IDMap iDMap = m_allAssignedIDs.Find((IDMap x) => x.gameObject == _object);
		if (iDMap != null)
		{
			result = iDMap.id;
		}
		return result;
	}
}
