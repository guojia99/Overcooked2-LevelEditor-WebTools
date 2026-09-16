using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class EmbeddedTextImageLookup : EmbeddedTextImageLookupBase
{
	[SerializeField]
	private Sprite[] m_sprites = new Sprite[0];

	protected override Sprite GetIcon(int _materialNum)
	{
		return m_sprites.TryAtIndex(_materialNum);
	}
}
