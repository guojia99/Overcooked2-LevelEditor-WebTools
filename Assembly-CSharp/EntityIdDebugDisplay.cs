using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

internal class EntityIdDebugDisplay : DebugDisplay
{
	private struct EntityInfo
	{
		public uint m_id;

		public Vector3 m_worldPos;

		public Rect m_rect;

		public string m_name;
	}

	private Camera m_camera;

	private Camera[] m_cameras;

	private int m_resolutionWidth;

	private int m_resolutionHeight;

	private FastList<EntityInfo> m_entityInfos = new FastList<EntityInfo>(256);

	private int m_smallLabelFontSize = 10;

	private GUIStyle m_smallLabel;

	private FastList<int> m_reflowList = new FastList<int>(256);

	public override void OnSetUp()
	{
		m_resolutionWidth = Screen.width;
		m_resolutionHeight = Screen.height;
		EntitySerialisationRegistry.OnEntryAdded = (GenericVoid<EntitySerialisationEntry>)Delegate.Combine(EntitySerialisationRegistry.OnEntryAdded, new GenericVoid<EntitySerialisationEntry>(OnEntityEntryAdded));
		EntitySerialisationRegistry.OnEntryRemoved = (GenericVoid<EntitySerialisationEntry>)Delegate.Combine(EntitySerialisationRegistry.OnEntryRemoved, new GenericVoid<EntitySerialisationEntry>(OnEntityEntryRemoved));
	}

	public override void OnDestroy()
	{
		EntitySerialisationRegistry.OnEntryAdded = (GenericVoid<EntitySerialisationEntry>)Delegate.Remove(EntitySerialisationRegistry.OnEntryAdded, new GenericVoid<EntitySerialisationEntry>(OnEntityEntryAdded));
		EntitySerialisationRegistry.OnEntryRemoved = (GenericVoid<EntitySerialisationEntry>)Delegate.Remove(EntitySerialisationRegistry.OnEntryRemoved, new GenericVoid<EntitySerialisationEntry>(OnEntityEntryRemoved));
	}

	private Camera GetCamera()
	{
		if (m_camera == null)
		{
			Camera[] allCameras = Camera.allCameras;
			for (int i = 0; i < allCameras.Length; i++)
			{
				if (allCameras[i] != null)
				{
					m_camera = allCameras[i];
					return m_camera;
				}
			}
		}
		return m_camera;
	}

	private void OnEntityEntryAdded(EntitySerialisationEntry entry)
	{
		if (entry != null)
		{
			EntityInfo item = new EntityInfo
			{
				m_id = ((entry.m_Header != null) ? entry.m_Header.m_uEntityID : 0u),
				m_worldPos = ((!(entry.m_GameObject == null)) ? entry.m_GameObject.transform.position : Vector3.zero),
				m_name = ((!(entry.m_GameObject == null)) ? entry.m_GameObject.name : "<unknown>")
			};
			Camera camera = GetCamera();
			if (camera != null)
			{
				string text = item.m_id + " - " + item.m_name;
				Vector2 position = camera.WorldToScreenPoint(item.m_worldPos);
				position.y = (float)m_resolutionHeight - position.y;
				Rect rect = new Rect(position, new Vector2(text.Length * m_smallLabelFontSize, m_smallLabelFontSize));
				item.m_rect = LayoutRect(rect);
				m_entityInfos.Add(item);
			}
		}
	}

	private void OnEntityEntryRemoved(EntitySerialisationEntry entry)
	{
		if (entry == null || entry.m_Header == null)
		{
			return;
		}
		for (int num = m_entityInfos.Count - 1; num >= 0; num--)
		{
			if (m_entityInfos._items[num].m_id == entry.m_Header.m_uEntityID)
			{
				m_entityInfos.RemoveAt(num);
				break;
			}
		}
	}

	public override void OnUpdate()
	{
		if (m_resolutionWidth != Screen.width || m_resolutionHeight != Screen.height)
		{
			m_resolutionWidth = Screen.width;
			m_resolutionHeight = Screen.height;
			for (int i = 0; i < m_entityInfos.Count; i++)
			{
				EntityInfo entityInfo = m_entityInfos._items[i];
				m_entityInfos._items[i].m_rect = LayoutRect(m_entityInfos._items[i].m_rect);
				m_entityInfos._items[i] = entityInfo;
			}
		}
	}

	private Rect LayoutRect(Rect rect)
	{
		Rect result = rect;
		bool flag = false;
		do
		{
			flag = false;
			for (int i = 0; i < m_entityInfos.Count; i++)
			{
				Rect rect2 = m_entityInfos._items[i].m_rect;
				if (result.Overlaps(rect2))
				{
					result.y += rect2.height + 0.5f;
					flag = true;
				}
			}
		}
		while (flag);
		return result;
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		if (m_smallLabel == null)
		{
			m_smallLabel = new GUIStyle("Label")
			{
				fontSize = m_smallLabelFontSize,
				alignment = TextAnchor.UpperLeft,
				clipping = TextClipping.Overflow
			};
		}
		m_reflowList.Clear();
		for (int i = 0; i < m_entityInfos.Count; i++)
		{
			EntityInfo entityInfo = m_entityInfos._items[i];
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entityInfo.m_id);
			if (entry != null && entry.m_GameObject != null)
			{
				Rect rect2 = entityInfo.m_rect;
				float num = VectorUtils.Hmax(VectorUtils.Abs(entry.m_GameObject.transform.position - entityInfo.m_worldPos));
				if (num > 1f)
				{
					m_reflowList.Add(i);
				}
				GUIUtils.ShadowedLabel(rect2, entityInfo.m_id + " - " + entityInfo.m_name, m_smallLabel);
			}
		}
		Camera camera = GetCamera();
		if (!(camera != null))
		{
			return;
		}
		for (int j = 0; j < m_reflowList.Count; j++)
		{
			int num2 = m_reflowList._items[j];
			EntityInfo entityInfo2 = m_entityInfos._items[num2];
			EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(entityInfo2.m_id);
			if (entry2 != null && entry2.m_GameObject != null)
			{
				entityInfo2.m_worldPos = entry2.m_GameObject.transform.position;
				Vector2 vector = camera.WorldToScreenPoint(entityInfo2.m_worldPos);
				vector.y = (float)m_resolutionHeight - vector.y;
				Rect rect3 = entityInfo2.m_rect;
				rect3.x = vector.x;
				rect3.y = vector.y;
				rect3 = LayoutRect(rect3);
				entityInfo2.m_rect = rect3;
				m_entityInfos._items[num2] = entityInfo2;
			}
		}
	}
}
