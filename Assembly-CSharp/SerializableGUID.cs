using System;
using UnityEngine;

[Serializable]
public struct SerializableGUID
{
	[SerializeField]
	private string Data;

	private SerializableGUID(string _data)
	{
		Data = _data;
	}

	public static explicit operator SerializableGUID(Guid _guid)
	{
		return new SerializableGUID(_guid.ToString());
	}

	public static explicit operator Guid(SerializableGUID _sGUID)
	{
		return new Guid(_sGUID.Data);
	}
}
