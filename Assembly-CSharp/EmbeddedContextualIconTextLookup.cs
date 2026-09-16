using UnityEngine;

public class EmbeddedContextualIconTextLookup : EmbeddedTextImageLookupBase
{
	[SerializeField]
	private SemanticIconLookup.Semantic[] m_sprites = new SemanticIconLookup.Semantic[0];

	private SemanticIconLookup m_semanticIconLookup;

	protected void Awake()
	{
		m_semanticIconLookup = GameUtils.RequireManager<SemanticIconLookup>();
	}

	protected override Sprite GetIcon(int _materialNum)
	{
		SemanticIconLookup.Semantic semantic = m_sprites.TryAtIndex(_materialNum);
		return m_semanticIconLookup.GetIcon(semantic);
	}
}
