using System;
using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[Serializable]
public class RecipeList : ScriptableObject, ISerializationCallbackReceiver
{
	[Serializable]
	public class Entry : IWeight, Serialisable
	{
		public OrderDefinitionNode m_order;

		public float m_weight;

		public int m_scoreForMeal = 1;

		private const int kBitsPerOrderNode = 32;

		private const int kBitsPerScore = 10;

		public float Weight
		{
			get
			{
				return m_weight;
			}
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_order.m_uID, 32);
			writer.Write((uint)m_scoreForMeal, 10);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			int iD = (int)reader.ReadUInt32(32);
			m_order = GameUtils.GetOrderDefinitionNode(iD);
			m_scoreForMeal = (int)reader.ReadUInt32(10);
			return true;
		}

		public void Copy(Entry entry)
		{
			m_order = entry.m_order;
			m_weight = entry.m_weight;
			m_scoreForMeal = entry.m_scoreForMeal;
		}
	}

	[SerializeField]
	public Entry[] m_recipes;

	[SerializeField]
	public Entry[] m_freestyle;

	public void OnAfterDeserialize()
	{
		Validate();
	}

	public void OnBeforeSerialize()
	{
		Validate();
	}

	private void Validate()
	{
	}
}
