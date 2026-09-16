using System;
using UnityEngine;

[Serializable]
public class EmoteWheelOption
{
	[Serializable]
	public enum EmoteType
	{
		Dialog = 0,
		Animation = 1,
		Both = 2
	}

	[Serializable]
	public class Connection
	{
		public enum Direction
		{
			Right = 0,
			DownRight = 1,
			Down = 2,
			DownLeft = 3,
			Left = 4,
			UpLeft = 5,
			Up = 6,
			UpRight = 7,
			COUNT = 8
		}

		public const int c_originConnection = -1;

		public const int c_noConnection = -2;

		[SerializeField]
		public int m_connectedTo = -2;
	}

	[SerializeField]
	public EmoteType m_type;

	[SerializeField]
	public string m_emoteId;

	[SerializeField]
	public Connection[] m_connections = new Connection[8];

	[SerializeField]
	public GameObject m_wheelButtonPrefab;

	[SerializeField]
	public GameObject m_wheelButtonHighlightPrefab;

	[SerializeField]
	public GameObject m_dialogPrefab;

	[SerializeField]
	public float m_duration = 3f;

	[SerializeField]
	public Vector2 m_anchorOffset = new Vector2(0f, 0f);

	[SerializeField]
	public string m_animTrigger;

	[SerializeField]
	public bool m_triggerForCode;

	[NonSerialized]
	public int m_animTriggerHash;
}
