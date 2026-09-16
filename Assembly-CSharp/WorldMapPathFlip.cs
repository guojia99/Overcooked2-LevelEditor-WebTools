using System.Collections.Generic;
using UnityEngine;

public class WorldMapPathFlip : WorldMapFlipperBase
{
	[SerializeField]
	private MapNode[] m_levelPortalMapNode = new MapNode[0];

	private List<FlipType> m_collectedFlipTypes = new List<FlipType>();

	protected override void Awake()
	{
		base.Awake();
		for (int i = 0; i < m_levelPortalMapNode.Length; i++)
		{
			m_levelPortalMapNode[i].RegisterFlipCallback(OnFlipCallback);
		}
	}

	private void OnFlipCallback(FlipDirection _direction, FlipType _type)
	{
		m_collectedFlipTypes.Add(_type);
		if (m_collectedFlipTypes.Count == m_levelPortalMapNode.Length)
		{
			if (m_collectedFlipTypes.Contains(FlipType.Normal))
			{
				StartUnfoldFlow();
			}
			else
			{
				StartInstantUnfold();
			}
		}
	}
}
