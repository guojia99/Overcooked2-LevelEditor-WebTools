using UnityEngine;

public class ScoreBoundaryStar : MonoBehaviour
{
	[SerializeField]
	[StarNumber]
	public int m_star;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.Editable)]
	private T17Text m_scoreText;

	[SerializeField]
	[AssignChildRecursive("Completed_Star", Editorbility.Editable)]
	public GameObject m_completedStar;

	public int Score
	{
		set
		{
			if (m_scoreText != null)
			{
				m_scoreText.SetNonLocalizedText(value.ToString());
			}
		}
	}

	public void SetUnlocked(bool _isUnlocked)
	{
		if (m_completedStar != null)
		{
			m_completedStar.SetActive(_isUnlocked);
		}
	}
}
