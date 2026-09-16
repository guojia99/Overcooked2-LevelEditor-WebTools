using System.Collections.Generic;
using UnityEngine;

public class LivesGUI : MonoBehaviour
{
	[SerializeField]
	private GUIRect m_iconDisplayRect;

	[SerializeField]
	private SubTexture2D m_texture;

	[SerializeField]
	private int m_startingLives;

	private List<SpriteGUI> m_lifeSprites = new List<SpriteGUI>();

	public int GetLives()
	{
		return m_lifeSprites.Count;
	}

	public void SetLives(int _numLives)
	{
		m_startingLives = _numLives;
		if (m_lifeSprites.Count < _numLives)
		{
			while (m_lifeSprites.Count < _numLives)
			{
				AddLife();
			}
			return;
		}
		for (int num = m_lifeSprites.Count - 1; num >= _numLives; num--)
		{
			SpriteGUI obj = m_lifeSprites[num];
			Object.Destroy(obj);
			m_lifeSprites.RemoveAt(num);
		}
	}

	private void Start()
	{
		SetLives(m_startingLives);
	}

	private void AddLife()
	{
		GameObject gameObject = GameObjectUtils.CreateOnParent<SpriteGUI>(base.gameObject, "Life");
		SpriteGUI component = gameObject.GetComponent<SpriteGUI>();
		GUIRect gUIRect = m_iconDisplayRect.DeepCopy();
		gUIRect.m_rect.x += (float)m_lifeSprites.Count * gUIRect.m_rect.width;
		component.SetData(gUIRect, m_texture, true);
		m_lifeSprites.Add(component);
	}
}
