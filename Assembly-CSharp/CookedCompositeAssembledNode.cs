using BitStream;

public class CookedCompositeAssembledNode : CompositeAssembledNode
{
	public CookingStepData m_cookingStep;

	public CookedCompositeOrderNode.CookingProgress m_progress = CookedCompositeOrderNode.CookingProgress.Cooked;

	public float? m_recordedProgress = 0f;

	public override void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_cookingStep.m_uID, 32);
		writer.Write((uint)m_progress, 4);
		writer.Write(m_recordedProgress.HasValue);
		if (m_recordedProgress.HasValue)
		{
			writer.Write(m_recordedProgress.Value);
		}
		base.Serialise(writer);
	}

	public override bool Deserialise(BitStreamReader reader)
	{
		m_cookingStep = GameUtils.GetCookingStepData((int)reader.ReadUInt32(32));
		m_progress = (CookedCompositeOrderNode.CookingProgress)reader.ReadUInt32(4);
		if (reader.ReadBit())
		{
			m_recordedProgress = reader.ReadFloat32();
		}
		else
		{
			m_recordedProgress = null;
		}
		return base.Deserialise(reader);
	}

	protected override bool IsMatch(AssembledDefinitionNode _subject)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _subject as CookedCompositeAssembledNode;
		if (cookedCompositeAssembledNode != null && AssumeTypeMatch(cookedCompositeAssembledNode))
		{
			return m_cookingStep.m_uID == cookedCompositeAssembledNode.m_cookingStep.m_uID && m_progress == cookedCompositeAssembledNode.m_progress;
		}
		return false;
	}

	public override void ReplaceData(AssembledDefinitionNode _node)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _node as CookedCompositeAssembledNode;
		m_cookingStep = cookedCompositeAssembledNode.m_cookingStep;
		m_progress = cookedCompositeAssembledNode.m_progress;
		m_recordedProgress = cookedCompositeAssembledNode.m_recordedProgress;
		base.ReplaceData(_node);
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		bool flag = m_progress != CookedCompositeOrderNode.CookingProgress.Raw;
		CompositeAssembledNode compositeAssembledNode;
		if (flag)
		{
			CookedCompositeAssembledNode cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
			cookedCompositeAssembledNode.m_cookingStep = m_cookingStep;
			cookedCompositeAssembledNode.m_progress = m_progress;
			compositeAssembledNode = cookedCompositeAssembledNode;
		}
		else
		{
			compositeAssembledNode = new CompositeAssembledNode();
		}
		for (int i = 0; i < m_composition.Length; i++)
		{
			AssembledDefinitionNode assembledDefinitionNode = m_composition[i].Simpilfy();
			if (assembledDefinitionNode != AssembledDefinitionNode.NullNode)
			{
				ArrayUtils.PushBack(ref compositeAssembledNode.m_composition, assembledDefinitionNode);
			}
		}
		for (int j = 0; j < m_optional.Length; j++)
		{
			AssembledDefinitionNode assembledDefinitionNode2 = m_optional[j].Simpilfy();
			if (assembledDefinitionNode2 != AssembledDefinitionNode.NullNode)
			{
				ArrayUtils.PushBack(ref compositeAssembledNode.m_optional, assembledDefinitionNode2);
			}
		}
		compositeAssembledNode.m_permittedEntries = m_permittedEntries;
		if (compositeAssembledNode.m_optional.Length == 0)
		{
			if (compositeAssembledNode.m_composition.Length == 1 && !flag)
			{
				return compositeAssembledNode.m_composition[0];
			}
			if (compositeAssembledNode.m_composition.Length == 1 && compositeAssembledNode.m_composition[0] is CompositeAssembledNode && !(compositeAssembledNode.m_composition[0] is MixedCompositeAssembledNode))
			{
				CompositeAssembledNode compositeAssembledNode2 = compositeAssembledNode.m_composition[0] as CompositeAssembledNode;
				compositeAssembledNode.m_composition = compositeAssembledNode2.m_composition;
				compositeAssembledNode.m_optional = compositeAssembledNode.m_optional.Union(compositeAssembledNode2.m_optional);
				return compositeAssembledNode;
			}
			if (compositeAssembledNode.m_composition.Length == 0)
			{
				return AssembledDefinitionNode.NullNode;
			}
		}
		return compositeAssembledNode;
	}
}
