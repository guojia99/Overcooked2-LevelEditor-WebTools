using System.Collections.Generic;
using UnityEngine;

public class ConveyorPrediction : IClientSidePredicted
{
	public struct Destination
	{
		public uint targetEntityID;

		public Transform targetTransform;

		public float arriveTime;

		public float attachTime;
	}

	public FastList<Destination> m_Destinations = new FastList<Destination>();

	public Transform m_Transform;

	public static GenericVoid<Destination, ConveyorPrediction> OnStartedMovingToDestination = delegate
	{
	};

	public static GenericVoid<Destination, ConveyorPrediction> OnDestinationReached = delegate
	{
	};

	private float m_RemainingMove;

	private bool m_bCalculatedDistance;

	public void EnqueueDestination(Destination dest)
	{
		m_Destinations.Add(dest);
		if (m_Destinations.Count == 1)
		{
			OnStartedMovingToDestination(m_Destinations._items[0], this);
		}
	}

	public void Clear()
	{
		m_Destinations.Clear();
	}

	public void Update()
	{
		if (TimeManager.IsPaused(m_Transform.gameObject))
		{
			for (int i = 0; i < m_Destinations.Count; i++)
			{
				m_Destinations._items[i].arriveTime += ClientTime.DeltaTime();
			}
		}
		else
		{
			if (m_Destinations.Count <= 0)
			{
				return;
			}
			Vector3 position = m_Destinations._items[0].targetTransform.position;
			float num = ClientTime.Time();
			float num2 = m_Destinations._items[0].arriveTime - num;
			if (num2 <= Time.deltaTime)
			{
				m_Transform.position = position;
				OnDestinationReached(m_Destinations._items[0], this);
				m_Destinations.RemoveAt(0);
				if (m_Destinations.Count > 0)
				{
					OnStartedMovingToDestination(m_Destinations._items[0], this);
				}
			}
			else
			{
				Vector3 vector = position - m_Transform.position;
				float num3 = vector.magnitude / num2;
				vector.Normalize();
				Vector3 vector2 = vector * num3 * TimeManager.GetDeltaTime(m_Transform.gameObject);
				m_Transform.position += vector2;
			}
		}
	}
}
