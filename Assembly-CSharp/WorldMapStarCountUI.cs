using UnityEngine;
using UnityEngine.UI;

public class WorldMapStarCountUI : UIControllerBase
{
	[SerializeField]
	private Text m_text;

	private void Start()
	{
		int starTotal = GameUtils.GetGameSession().Progress.GetStarTotal();
		m_text.text = starTotal.ToString().PadLeft(3, '0');
	}
}
