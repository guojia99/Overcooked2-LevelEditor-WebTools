using System.Collections.Generic;
using BitStream;

public class ItemAssembledNode : AssembledDefinitionNode
{
	public ItemOrderNode m_itemOrderNode;

	public ItemAssembledNode()
	{
	}

	public ItemAssembledNode(ItemOrderNode _orderNode)
	{
		m_itemOrderNode = _orderNode;
	}

	public override void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_itemOrderNode.m_uID, 32);
	}

	public override bool Deserialise(BitStreamReader reader)
	{
		OrderDefinitionNode orderDefinitionNode = GameUtils.GetOrderDefinitionNode((int)reader.ReadUInt32(32));
		ItemOrderNode itemOrderNode = orderDefinitionNode as ItemOrderNode;
		m_itemOrderNode = itemOrderNode;
		return true;
	}

	public override IEnumerator<AssembledDefinitionNode> GetEnumerator()
	{
		yield return this;
	}

	protected override bool IsMatch(AssembledDefinitionNode _subject)
	{
		ItemAssembledNode itemAssembledNode = _subject as ItemAssembledNode;
		if (itemAssembledNode != null)
		{
			return m_itemOrderNode.m_uID == itemAssembledNode.m_itemOrderNode.m_uID;
		}
		return false;
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		return new ItemAssembledNode(m_itemOrderNode);
	}

	public override void ReplaceData(AssembledDefinitionNode _node)
	{
		ItemAssembledNode itemAssembledNode = _node as ItemAssembledNode;
		m_itemOrderNode = itemAssembledNode.m_itemOrderNode;
		base.ReplaceData(_node);
	}

	public override int GetNodeCount()
	{
		return 1;
	}
}
