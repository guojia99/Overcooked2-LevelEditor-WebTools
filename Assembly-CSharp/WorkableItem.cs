using System;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Items/WorkableItem")]
public class WorkableItem : MonoBehaviour
{
	[SerializeField]
	public ProgressUIController m_progressUIPrefab;

	[SerializeField]
	public GameObject m_nextPrefab;

	[SerializeField]
	public int m_stages = 8;

	[SerializeField]
	public string m_animationVariable = "Progress";

	[SerializeField]
	public string m_gatherVariable = "Gather";

	[SerializeField]
	public string m_dropVariable = "Drop";

	[NonSerialized]
	public int m_iAnimationVariable;

	[NonSerialized]
	public int m_iGatherVariable;

	[NonSerialized]
	public int m_iDropVariable;

	protected virtual void Awake()
	{
		m_iAnimationVariable = Animator.StringToHash(m_animationVariable);
		m_iGatherVariable = Animator.StringToHash(m_gatherVariable);
		m_iDropVariable = Animator.StringToHash(m_dropVariable);
	}

	public GameObject GetNextPrefab()
	{
		return m_nextPrefab;
	}

	public int GetChopTimeMultiplier(int _playerCount)
	{
		GameConfig gameConfig = GameUtils.GetGameConfig();
		if (gameConfig != null)
		{
			int result = 1;
			GameSession gameSession = GameUtils.GetGameSession();
			switch (gameSession.TypeSettings.Type)
			{
			case GameSession.GameType.Cooperative:
				result = ((_playerCount != 1) ? 1 : gameConfig.SingleplayerChopTimeMultiplier);
				break;
			case GameSession.GameType.Competitive:
				result = ((_playerCount >= 4) ? 1 : gameConfig.SingleplayerChopTimeMultiplier);
				break;
			}
			return result;
		}
		return 1;
	}
}
