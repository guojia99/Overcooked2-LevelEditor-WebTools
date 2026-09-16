using System;
using UnityEngine;

[Serializable]
public class DLCSerializedData<T>
{
	[SerializeField]
	private T m_Data = default(T);

	[SerializeField]
	private T m_DLC02Data = default(T);

	[SerializeField]
	private T m_DLC03Data = default(T);

	[SerializeField]
	private T m_DLC04Data = default(T);

	[SerializeField]
	private T m_DLC05Data = default(T);

	[SerializeField]
	private T m_DLC06Data = default(T);

	[SerializeField]
	private T m_DLC07Data = default(T);

	[SerializeField]
	private T m_DLC08Data = default(T);

	[SerializeField]
	private T m_DLC09Data = default(T);

	[SerializeField]
	private T m_DLC10Data = default(T);

	[SerializeField]
	private T m_DLC11Data = default(T);

	[SerializeField]
	private T m_DLC13Data = default(T);

	public T[] AllData
	{
		get
		{
			return new T[12]
			{
				m_Data, m_DLC02Data, m_DLC03Data, m_DLC04Data, m_DLC05Data, m_DLC06Data, m_DLC07Data, m_DLC08Data, m_DLC09Data, m_DLC10Data,
				m_DLC11Data, m_DLC13Data
			};
		}
	}
}
