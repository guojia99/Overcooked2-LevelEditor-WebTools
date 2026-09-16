using System.Collections;
using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class AssembledDefinitionNode : IEnumerable<AssembledDefinitionNode>, Serialisable, IEnumerable
{
	public static AssembledDefinitionNode NullNode = new NullAssembledNode();

	public GameObject m_freeObject;

	public abstract void Serialise(BitStreamWriter writer);

	public abstract bool Deserialise(BitStreamReader reader);

	public static bool MatchingAlreadySimple(AssembledDefinitionNode _simpleNode1, AssembledDefinitionNode _simpleNode2)
	{
		if (_simpleNode1 == null || _simpleNode2 == null)
		{
			return _simpleNode1 == null && _simpleNode2 == null;
		}
		return _simpleNode1.IsMatch(_simpleNode2);
	}

	public static bool Matching(OrderDefinitionNode _node1, OrderDefinitionNode _node2)
	{
		if (_node1 == null || _node2 == null)
		{
			return _node1 == null && _node2 == null;
		}
		AssembledDefinitionNode assembledDefinitionNode = _node1.Simpilfy();
		AssembledDefinitionNode node = _node2.Simpilfy();
		return assembledDefinitionNode.IsMatch(node);
	}

	public static bool Matching(AssembledDefinitionNode _node1, AssembledDefinitionNode _node2)
	{
		if (_node1 == null || _node2 == null)
		{
			return _node1 == null && _node2 == null;
		}
		AssembledDefinitionNode assembledDefinitionNode = _node1.Simpilfy();
		AssembledDefinitionNode node = _node2.Simpilfy();
		return assembledDefinitionNode.IsMatch(node);
	}

	public static bool Matching(OrderDefinitionNode _node1, AssembledDefinitionNode _node2)
	{
		if (_node1 == null || _node2 == null)
		{
			return _node1 == null && _node2 == null;
		}
		AssembledDefinitionNode assembledDefinitionNode = _node1.Simpilfy();
		AssembledDefinitionNode node = _node2.Simpilfy();
		return assembledDefinitionNode.IsMatch(node);
	}

	public static bool Matching(AssembledDefinitionNode _node1, OrderDefinitionNode _node2)
	{
		if (_node1 == null || _node2 == null)
		{
			return _node1 == null && _node2 == null;
		}
		AssembledDefinitionNode assembledDefinitionNode = _node1.Simpilfy();
		AssembledDefinitionNode node = _node2.Simpilfy();
		return assembledDefinitionNode.IsMatch(node);
	}

	public abstract IEnumerator<AssembledDefinitionNode> GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public override bool Equals(object _node)
	{
		return Matching(this, _node as AssembledDefinitionNode);
	}

	protected abstract bool IsMatch(AssembledDefinitionNode _node);

	public abstract int GetNodeCount();

	public abstract AssembledDefinitionNode Simpilfy();

	public virtual void ReplaceData(AssembledDefinitionNode _node)
	{
		m_freeObject = _node.m_freeObject;
	}
}
