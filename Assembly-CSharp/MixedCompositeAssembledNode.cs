using BitStream;

public class MixedCompositeAssembledNode : CompositeAssembledNode
{
	public MixedCompositeOrderNode.MixingProgress m_progress = MixedCompositeOrderNode.MixingProgress.Mixed;

	public float? m_recordedProgress = 0f;

	public override void Serialise(BitStreamWriter writer)
	{
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
		m_progress = (MixedCompositeOrderNode.MixingProgress)reader.ReadUInt32(4);
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
		MixedCompositeAssembledNode mixedCompositeAssembledNode = _subject as MixedCompositeAssembledNode;
		if (mixedCompositeAssembledNode != null && AssumeTypeMatch(mixedCompositeAssembledNode))
		{
			return m_progress == mixedCompositeAssembledNode.m_progress;
		}
		return false;
	}

	public override void ReplaceData(AssembledDefinitionNode _node)
	{
		MixedCompositeAssembledNode mixedCompositeAssembledNode = _node as MixedCompositeAssembledNode;
		m_progress = mixedCompositeAssembledNode.m_progress;
		m_recordedProgress = mixedCompositeAssembledNode.m_recordedProgress;
		base.ReplaceData(_node);
	}

	public override AssembledDefinitionNode Simpilfy()
	{
		bool flag = m_progress != MixedCompositeOrderNode.MixingProgress.Unmixed;
		CompositeAssembledNode compositeAssembledNode;
		if (flag)
		{
			MixedCompositeAssembledNode mixedCompositeAssembledNode = new MixedCompositeAssembledNode();
			mixedCompositeAssembledNode.m_progress = m_progress;
			mixedCompositeAssembledNode.m_recordedProgress = m_recordedProgress;
			compositeAssembledNode = mixedCompositeAssembledNode;
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
			if (compositeAssembledNode.m_composition.Length == 1 && compositeAssembledNode.m_composition[0] is CompositeAssembledNode)
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
