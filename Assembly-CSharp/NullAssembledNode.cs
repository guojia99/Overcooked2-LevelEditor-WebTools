using System.Collections.Generic;
using BitStream;

public class NullAssembledNode : AssembledDefinitionNode
{
	public override void Serialise(BitStreamWriter writer)
	{
	}

	public override bool Deserialise(BitStreamReader reader)
	{
		return true;
	}

	public override IEnumerator<AssembledDefinitionNode> GetEnumerator()
	{
		yield return this;
	}

	protected override bool IsMatch(AssembledDefinitionNode _node)
	{
		return _node.GetType() == typeof(NullAssembledNode);
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		return AssembledDefinitionNode.NullNode;
	}

	public override int GetNodeCount()
	{
		return 1;
	}
}
