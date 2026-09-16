using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperPie : MB3_MeshBakerGrouperCore
	{
		public MB3_MeshBakerGrouperPie(GrouperData data)
		{
			d = data;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			if (d.pieNumSegments == 0)
			{
				Debug.LogError("pieNumSegments must be greater than zero.");
				return dictionary;
			}
			if (d.pieAxis.magnitude <= 1E-06f)
			{
				Debug.LogError("Pie axis must have length greater than zero.");
				return dictionary;
			}
			d.pieAxis.Normalize();
			Quaternion quaternion = Quaternion.FromToRotation(d.pieAxis, Vector3.up);
			Debug.Log("Collecting renderers in each cell");
			foreach (GameObject item in selection)
			{
				if (item == null)
				{
					continue;
				}
				GameObject gameObject = item;
				Renderer component = gameObject.GetComponent<Renderer>();
				if (!(component is MeshRenderer) && !(component is SkinnedMeshRenderer))
				{
					continue;
				}
				Vector3 vector = component.bounds.center - d.origin;
				vector.Normalize();
				vector = quaternion * vector;
				float num = 0f;
				if (Mathf.Abs(vector.x) < 0.0001f && Mathf.Abs(vector.z) < 0.0001f)
				{
					num = 0f;
				}
				else
				{
					num = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
					if (num < 0f)
					{
						num = 360f + num;
					}
				}
				int num2 = Mathf.FloorToInt(num / 360f * (float)d.pieNumSegments);
				List<Renderer> list = null;
				string key = "seg_" + num2;
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
			return dictionary;
		}

		public override void DrawGizmos(Bounds sourceObjectBounds)
		{
			if (!(d.pieAxis.magnitude < 0.1f) && d.pieNumSegments >= 1)
			{
				float magnitude = sourceObjectBounds.extents.magnitude;
				DrawCircle(d.pieAxis, d.origin, magnitude, 24);
				Quaternion quaternion = Quaternion.FromToRotation(Vector3.up, d.pieAxis);
				Quaternion quaternion2 = Quaternion.AngleAxis(180f / (float)d.pieNumSegments, Vector3.up);
				Vector3 vector = Vector3.forward;
				for (int i = 0; i < d.pieNumSegments; i++)
				{
					Vector3 vector2 = quaternion * vector;
					Gizmos.DrawLine(d.origin, d.origin + vector2 * magnitude);
					vector = quaternion2 * vector;
					vector = quaternion2 * vector;
				}
			}
		}

		public static void DrawCircle(Vector3 axis, Vector3 center, float radius, int subdiv)
		{
			Quaternion quaternion = Quaternion.AngleAxis(360 / subdiv, axis);
			Vector3 vector = new Vector3(axis.y, 0f - axis.x, axis.z);
			vector.Normalize();
			vector *= radius;
			for (int i = 0; i < subdiv + 1; i++)
			{
				Vector3 vector2 = quaternion * vector;
				Gizmos.DrawLine(center + vector, center + vector2);
				vector = vector2;
			}
		}
	}
}
