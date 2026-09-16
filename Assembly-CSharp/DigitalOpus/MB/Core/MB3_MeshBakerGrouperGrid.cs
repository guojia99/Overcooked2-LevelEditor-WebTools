using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperGrid : MB3_MeshBakerGrouperCore
	{
		public MB3_MeshBakerGrouperGrid(GrouperData d)
		{
			base.d = d;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			if (d.cellSize.x <= 0f || d.cellSize.y <= 0f || d.cellSize.z <= 0f)
			{
				Debug.LogError("cellSize x,y,z must all be greater than zero.");
				return dictionary;
			}
			Debug.Log("Collecting renderers in each cell");
			foreach (GameObject item in selection)
			{
				if (item == null)
				{
					continue;
				}
				GameObject gameObject = item;
				Renderer component = gameObject.GetComponent<Renderer>();
				if (component is MeshRenderer || component is SkinnedMeshRenderer)
				{
					Vector3 center = component.bounds.center;
					center.x = Mathf.Floor((center.x - d.origin.x) / d.cellSize.x) * d.cellSize.x;
					center.y = Mathf.Floor((center.y - d.origin.y) / d.cellSize.y) * d.cellSize.y;
					center.z = Mathf.Floor((center.z - d.origin.z) / d.cellSize.z) * d.cellSize.z;
					List<Renderer> list = null;
					string key = center.ToString();
					if (dictionary.ContainsKey(key))
					{
						list = dictionary[key];
					}
					else
					{
						list = new List<Renderer>();
						dictionary.Add(key, list);
					}
					if (!list.Contains(component))
					{
						list.Add(component);
					}
				}
			}
			return dictionary;
		}

		public override void DrawGizmos(Bounds sourceObjectBounds)
		{
			Vector3 cellSize = d.cellSize;
			if (cellSize.x <= 1E-05f || cellSize.y <= 1E-05f || cellSize.z <= 1E-05f)
			{
				return;
			}
			Vector3 vector = sourceObjectBounds.center - sourceObjectBounds.extents;
			Vector3 origin = d.origin;
			origin.x %= cellSize.x;
			origin.y %= cellSize.y;
			origin.z %= cellSize.z;
			vector.x = Mathf.Round(vector.x / cellSize.x) * cellSize.x + origin.x;
			vector.y = Mathf.Round(vector.y / cellSize.y) * cellSize.y + origin.y;
			vector.z = Mathf.Round(vector.z / cellSize.z) * cellSize.z + origin.z;
			if (vector.x > sourceObjectBounds.center.x - sourceObjectBounds.extents.x)
			{
				vector.x -= cellSize.x;
			}
			if (vector.y > sourceObjectBounds.center.y - sourceObjectBounds.extents.y)
			{
				vector.y -= cellSize.y;
			}
			if (vector.z > sourceObjectBounds.center.z - sourceObjectBounds.extents.z)
			{
				vector.z -= cellSize.z;
			}
			Vector3 vector2 = vector;
			int num = Mathf.CeilToInt(sourceObjectBounds.size.x / cellSize.x + sourceObjectBounds.size.y / cellSize.y + sourceObjectBounds.size.z / cellSize.z);
			if (num > 200)
			{
				Gizmos.DrawWireCube(d.origin + cellSize / 2f, cellSize);
				return;
			}
			while (vector.x < sourceObjectBounds.center.x + sourceObjectBounds.extents.x)
			{
				vector.y = vector2.y;
				while (vector.y < sourceObjectBounds.center.y + sourceObjectBounds.extents.y)
				{
					vector.z = vector2.z;
					while (vector.z < sourceObjectBounds.center.z + sourceObjectBounds.extents.z)
					{
						Gizmos.DrawWireCube(vector + cellSize / 2f, cellSize);
						vector.z += cellSize.z;
					}
					vector.y += cellSize.y;
				}
				vector.x += cellSize.x;
			}
		}
	}
}
