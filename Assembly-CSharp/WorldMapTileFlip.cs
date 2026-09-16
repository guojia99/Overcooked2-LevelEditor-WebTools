using System;
using UnityEngine;

public class WorldMapTileFlip : WorldMapFlipperBase
{
	[Serializable]
	public class FlipOwnerData
	{
		[SerializeField]
		public MapNode m_levelMapNode;
	}

	[SerializeField]
	public FlipOwnerData m_flipOwnerData = new FlipOwnerData();

	protected override void Awake()
	{
		base.Awake();
		if (m_flipOwnerData == null)
		{
			return;
		}
		if (base.gameObject.GetComponent<GridAutoParenting>() == null)
		{
			if (m_flipOwnerData.m_levelMapNode != null)
			{
				m_startFlipDelay = 0f;
				m_flipOwnerData.m_levelMapNode.AddToFlipSet(GetOrder(), this);
			}
		}
		else
		{
			m_startFlipDelay += 0.2f * (float)base.transform.GetSiblingIndex();
		}
	}

	private int GetOrder()
	{
		GridManager gridManager = GameUtils.GetGridManager(base.transform);
		GridIndex unclampedGridLocationFromPos = gridManager.GetUnclampedGridLocationFromPos(m_flipOwnerData.m_levelMapNode.transform.position);
		GridIndex unclampedGridLocationFromPos2 = gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
		return HexGridManager.ComputeDistanceHexGrid(unclampedGridLocationFromPos, unclampedGridLocationFromPos2);
	}

	public MapNode GetMapNode()
	{
		if (m_flipOwnerData != null)
		{
			return m_flipOwnerData.m_levelMapNode;
		}
		return null;
	}

	public void DrawBoundingHex()
	{
	}
}
