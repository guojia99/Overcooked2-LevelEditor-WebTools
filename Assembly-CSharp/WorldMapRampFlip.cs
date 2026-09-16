using System;
using UnityEngine;

public class WorldMapRampFlip : WorldMapFlipperBase
{
	[Serializable]
	public class SwitchOwnerData
	{
		public SwitchMapNode m_switchMapNode;
	}

	[SerializeField]
	private SwitchOwnerData m_switchOwnerData = new SwitchOwnerData();

	protected override void Awake()
	{
		base.Awake();
		if (m_switchOwnerData.m_switchMapNode != null)
		{
			m_switchOwnerData.m_switchMapNode.AddToFlipSet(GetOrder(), this);
		}
		if (base.gameObject.RequestComponent<GridAutoParenting>() != null)
		{
			Transform parent = base.gameObject.transform.parent;
			WorldMapTileOptimizer component = parent.GetComponent<WorldMapTileOptimizer>();
			GameObject tile = component.Tile;
			if (tile != null)
			{
				base.transform.SetParent(tile.transform, true);
			}
			WorldMapTileFlip worldMapTileFlip = parent.gameObject.RequireComponent<WorldMapTileFlip>();
			MapNode mapNode = worldMapTileFlip.GetMapNode();
			if (mapNode != null)
			{
				mapNode.RegisterPreFlipCallback(OnPreFlipCallback);
			}
		}
	}

	private int GetOrder()
	{
		return 0;
	}

	public bool ShouldBeUnfolded()
	{
		SwitchMapNode switchMapNode = m_switchOwnerData.m_switchMapNode;
		return m_startFlipped || switchMapNode == null || switchMapNode.Unfolding || switchMapNode.Unfolded || switchMapNode.IsSwitchedDueToCompletion();
	}

	public override void StartUnfoldFlow()
	{
		if (ShouldBeUnfolded())
		{
			base.StartUnfoldFlow();
		}
	}

	public override void StartInstantUnfold()
	{
		if (ShouldBeUnfolded())
		{
			base.StartInstantUnfold();
		}
	}

	private void OnPreFlipCallback(FlipDirection _direction, FlipType _type)
	{
		if (IsFlipped() && _type == FlipType.Normal)
		{
			StartFoldFlow();
		}
	}
}
