using UnityEngine;
using UnityEngine.UI;

public class LivesUIController : UIControllerBase
{
	[SerializeField]
	private Image[] m_lifeImages = new Image[0];

	[SerializeField]
	private int m_lives;

	public int GetLives()
	{
		return m_lives;
	}

	public void SetLives(int _numLives)
	{
		m_lives = _numLives;
		for (int i = 0; i < m_lifeImages.Length; i++)
		{
			m_lifeImages[i].gameObject.SetActive(i < m_lives);
		}
	}

	private void Start()
	{
		SetLives(m_lives);
	}
}
