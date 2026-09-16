using System.Collections.Generic;
using UnityEngine;

public class TriggerIgniteArea : MonoBehaviour
{
	[SerializeField]
	private float m_radius;

	[SerializeField]
	private string m_ignitionTrigger;

	private void OnTrigger(string _trigger)
	{
		if (m_ignitionTrigger == _trigger)
		{
			IgniteArea();
		}
	}

	public void IgniteArea()
	{
		foreach (GameObject item in GridObjectsInRadius(m_radius))
		{
			if (item.RequestComponent<ServerFlammable>() != null)
			{
				ServerFlammable serverFlammable = item.RequireComponent<ServerFlammable>();
				serverFlammable.Ignite();
			}
		}
	}

	private IEnumerable<GameObject> GridObjectsInRadius(float _radius)
	{
		int gridCount = GridManager.GetActiveCount();
		for (int g = 0; g < gridCount; g++)
		{
			GridManager gridManager = GridManager.GetActive(g);
			GridIndex gridIndex = gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
			Vector3 referencePos = gridManager.GetPosFromGridLocation(gridIndex);
			int x = gridIndex.X;
			int y = gridIndex.Y;
			int z = gridIndex.Z;
			int total = 1;
			bool bContinue = false;
			do
			{
				bContinue = false;
				for (int i = -total; i <= total; i++)
				{
					int jPve = total - Mathf.Abs(i);
					GridIndex testIndex1 = new GridIndex(x + i, y, z + jPve);
					GridIndex testIndex2 = new GridIndex(x + i, y, z - jPve);
					GridIndex[] testIndices = new GridIndex[2] { testIndex1, testIndex2 };
					for (int k = 0; k < testIndices.Length; k++)
					{
						Vector3 position = gridManager.GetPosFromGridLocation(testIndices[k]);
						if ((referencePos - position).sqrMagnitude < m_radius * m_radius)
						{
							bContinue = true;
							GameObject occupier = gridManager.GetGridOccupant(testIndices[k]);
							if (occupier != null)
							{
								yield return occupier;
							}
						}
					}
				}
				total++;
			}
			while (bContinue);
		}
	}
}
