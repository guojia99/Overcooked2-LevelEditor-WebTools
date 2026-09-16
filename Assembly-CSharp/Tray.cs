using System.Collections.Generic;
using UnityEngine;

public class Tray : Plate
{
	[SerializeField]
	public OrderToPrefabLookup[] m_slots;

	public void GetIngredientContents(AssembledDefinitionNode _node, PlacementContext _context, List<int> _availableSlots)
	{
		_availableSlots.Clear();
		for (int i = 0; i < m_slots.Length; i++)
		{
			List<OrderContentRestriction> contentRestrictions = m_slots[i].GetContentRestrictions();
			for (int j = 0; j < contentRestrictions.Count; j++)
			{
				if (AssembledDefinitionNode.MatchingAlreadySimple(contentRestrictions[j].m_content.Simpilfy(), _node.Simpilfy()))
				{
					_availableSlots.Add(i);
				}
			}
		}
	}

	public bool AreContentsDifferent(AssembledDefinitionNode[] _contents1, AssembledDefinitionNode[] _contents2)
	{
		if (_contents1 == null || _contents2 == null)
		{
			return true;
		}
		if (_contents1.Length == _contents2.Length)
		{
			for (int i = 0; i < _contents1.Length; i++)
			{
				if (!AssembledDefinitionNode.Matching(_contents1[i], _contents2[i]))
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}
}
