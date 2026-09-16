using System.Collections.Generic;
using BitStream;

public class IngredientAssembledNode : AssembledDefinitionNode
{
	public IngredientOrderNode m_ingriedientOrderNode;

	public IngredientAssembledNode()
	{
	}

	public IngredientAssembledNode(IngredientOrderNode _orderNode)
	{
		m_ingriedientOrderNode = _orderNode;
	}

	public override void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_ingriedientOrderNode.m_uID, 32);
	}

	public override bool Deserialise(BitStreamReader reader)
	{
		OrderDefinitionNode orderDefinitionNode = GameUtils.GetOrderDefinitionNode((int)reader.ReadUInt32(32));
		IngredientOrderNode ingriedientOrderNode = orderDefinitionNode as IngredientOrderNode;
		m_ingriedientOrderNode = ingriedientOrderNode;
		return true;
	}

	public override IEnumerator<AssembledDefinitionNode> GetEnumerator()
	{
		yield return this;
	}

	protected override bool IsMatch(AssembledDefinitionNode _subject)
	{
		IngredientAssembledNode ingredientAssembledNode = _subject as IngredientAssembledNode;
		if (ingredientAssembledNode != null)
		{
			return m_ingriedientOrderNode.m_uID == ingredientAssembledNode.m_ingriedientOrderNode.m_uID;
		}
		return false;
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		return new IngredientAssembledNode(m_ingriedientOrderNode);
	}

	public override void ReplaceData(AssembledDefinitionNode _node)
	{
		IngredientAssembledNode ingredientAssembledNode = _node as IngredientAssembledNode;
		m_ingriedientOrderNode = ingredientAssembledNode.m_ingriedientOrderNode;
		base.ReplaceData(_node);
	}

	public override int GetNodeCount()
	{
		return 1;
	}
}
