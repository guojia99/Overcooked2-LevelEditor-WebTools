using System;
using System.Collections.Generic;
using BitStream;

public class CompositeAssembledNode : AssembledDefinitionNode
{
	public AssembledDefinitionNode[] m_composition = new AssembledDefinitionNode[0];

	public AssembledDefinitionNode[] m_optional = new AssembledDefinitionNode[0];

	public List<OrderContentRestriction> m_permittedEntries = new List<OrderContentRestriction>();

	public override void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_composition.Length, 5);
		for (int i = 0; i < m_composition.Length; i++)
		{
			writer.Write((uint)AssembledDefinitionNodeFactory.GetNodeType(m_composition[i]), 4);
			m_composition[i].Serialise(writer);
		}
	}

	public override bool Deserialise(BitStreamReader reader)
	{
		bool flag = true;
		if (m_composition.Length != 0)
		{
			m_composition = new AssembledDefinitionNode[0];
		}
		int num = (int)reader.ReadUInt32(5);
		for (int i = 0; i < num; i++)
		{
			AssembledDefinitionNode assembledDefinitionNode = AssembledDefinitionNodeFactory.CreateNode((int)reader.ReadUInt32(4));
			flag &= assembledDefinitionNode.Deserialise(reader);
			Array.Resize(ref m_composition, m_composition.Length + 1);
			m_composition[m_composition.Length - 1] = assembledDefinitionNode;
		}
		return flag;
	}

	public IEnumerator<AssembledDefinitionNode> GetEnumeratorExhaustive()
	{
		for (int i = 0; i < m_composition.Length; i++)
		{
			yield return m_composition[i];
			foreach (AssembledDefinitionNode item in m_composition[i])
			{
				yield return item;
			}
		}
	}

	public override IEnumerator<AssembledDefinitionNode> GetEnumerator()
	{
		for (int i = 0; i < m_composition.Length; i++)
		{
			foreach (AssembledDefinitionNode item in m_composition[i])
			{
				yield return item;
			}
		}
	}

	protected override bool IsMatch(AssembledDefinitionNode _subject)
	{
		if (_subject.GetType() == typeof(CompositeAssembledNode))
		{
			CompositeAssembledNode subject = _subject as CompositeAssembledNode;
			return AssumeTypeMatch(subject);
		}
		CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
		compositeAssembledNode.m_composition = new AssembledDefinitionNode[1] { _subject };
		return IsMatch(compositeAssembledNode);
	}

	protected bool AssumeTypeMatch(CompositeAssembledNode _subject)
	{
		return Contains(m_composition.Union(m_optional), _subject.m_composition) && Contains(_subject.m_composition.Union(_subject.m_optional), m_composition);
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
		compositeAssembledNode.m_permittedEntries = m_permittedEntries;
		for (int i = 0; i < m_composition.Length; i++)
		{
			if (m_composition[i] != AssembledDefinitionNode.NullNode)
			{
				AssembledDefinitionNode assembledDefinitionNode = m_composition[i].Simpilfy();
				if (assembledDefinitionNode != AssembledDefinitionNode.NullNode)
				{
					ArrayUtils.PushBack(ref compositeAssembledNode.m_composition, assembledDefinitionNode);
				}
			}
		}
		for (int j = 0; j < m_optional.Length; j++)
		{
			if (m_optional[j] != AssembledDefinitionNode.NullNode)
			{
				AssembledDefinitionNode assembledDefinitionNode2 = m_optional[j].Simpilfy();
				if (assembledDefinitionNode2 != AssembledDefinitionNode.NullNode)
				{
					ArrayUtils.PushBack(ref compositeAssembledNode.m_optional, assembledDefinitionNode2);
				}
			}
		}
		if (compositeAssembledNode.m_optional.Length == 0)
		{
			if (compositeAssembledNode.m_composition.Length == 1)
			{
				return compositeAssembledNode.m_composition[0];
			}
			if (compositeAssembledNode.m_composition.Length == 0)
			{
				return AssembledDefinitionNode.NullNode;
			}
		}
		return compositeAssembledNode;
	}

	public static bool Contains(AssembledDefinitionNode[] _superSet, AssembledDefinitionNode[] _subSet)
	{
		bool[] array = new bool[_superSet.Length];
		for (int i = 0; i < _subSet.Length; i++)
		{
			bool flag = false;
			for (int j = 0; j < _superSet.Length; j++)
			{
				if (!array[j] && (_subSet[i] == null || AssembledDefinitionNode.MatchingAlreadySimple(_subSet[i], _superSet[j])))
				{
					array[j] = true;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
		}
		return true;
	}

	public override void ReplaceData(AssembledDefinitionNode _node)
	{
		CompositeAssembledNode compositeAssembledNode = _node as CompositeAssembledNode;
		m_composition = compositeAssembledNode.m_composition;
		m_optional = compositeAssembledNode.m_optional;
		m_permittedEntries = compositeAssembledNode.m_permittedEntries;
		base.ReplaceData(_node);
	}

	public void AddOrderNode(AssembledDefinitionNode _toAdd, bool _dontUpdate)
	{
		if (m_freeObject != null && m_freeObject.RequestInterface<IHandleOrderModification>() != null && !_dontUpdate)
		{
			IHandleOrderModification handleOrderModification = m_freeObject.RequestInterface<IHandleOrderModification>();
			handleOrderModification.AddOrderContents(new AssembledDefinitionNode[1] { _toAdd });
			IOrderDefinition orderDefinition = m_freeObject.RequireInterface<IOrderDefinition>();
			CompositeAssembledNode node = orderDefinition.GetOrderComposition() as CompositeAssembledNode;
			ReplaceData(node);
		}
		else
		{
			Array.Resize(ref m_composition, m_composition.Length + 1);
			m_composition[m_composition.Length - 1] = _toAdd;
		}
	}

	public bool CanAddOrderNode(AssembledDefinitionNode _toAdd, bool _raw = false)
	{
		if (!_raw && m_freeObject != null && m_freeObject.RequestInterface<IHandleOrderModification>() != null)
		{
			IHandleOrderModification handleOrderModification = m_freeObject.RequestInterface<IHandleOrderModification>();
			return handleOrderModification.CanAddOrderContents(new AssembledDefinitionNode[1] { _toAdd });
		}
		int num = 0;
		AssembledDefinitionNode[] composition = m_composition;
		foreach (AssembledDefinitionNode node in composition)
		{
			if (AssembledDefinitionNode.Matching(node, _toAdd))
			{
				num++;
			}
		}
		Predicate<OrderContentRestriction> match = (OrderContentRestriction _specifier) => AssembledDefinitionNode.Matching(_specifier.m_content, _toAdd);
		OrderContentRestriction orderContentRestriction = m_permittedEntries.Find(match);
		if (orderContentRestriction == null)
		{
			return false;
		}
		return num < orderContentRestriction.m_amountAllowed && !CompositionContainsRestrictedNode(_toAdd, orderContentRestriction);
	}

	private bool CompositionContainsRestrictedNode(AssembledDefinitionNode _toAdd, OrderContentRestriction _specifier)
	{
		for (int i = 0; i < _specifier.m_restrictedContent.Length; i++)
		{
			OrderDefinitionNode node = _specifier.m_restrictedContent[i];
			for (int j = 0; j < m_composition.Length; j++)
			{
				AssembledDefinitionNode node2 = m_composition[j];
				if (AssembledDefinitionNode.Matching(node, node2))
				{
					return true;
				}
			}
		}
		return false;
	}

	public override int GetNodeCount()
	{
		int num = 1;
		for (int i = 0; i < m_composition.Length; i++)
		{
			num += m_composition[i].GetNodeCount();
		}
		return num;
	}
}
