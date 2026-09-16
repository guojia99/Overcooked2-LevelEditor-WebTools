using System;
using UnityEngine;

[Serializable]
public class ItemOrderNode : OrderDefinitionNode
{
	public Sprite m_iconSprite;

	public SubTexture2D m_crateLid;

	public Color m_colour = Color.white;

	[Range(0f, 1f)]
	public float m_heatValue;

	public override AssembledDefinitionNode Convert()
	{
		return new ItemAssembledNode(this);
	}
}
