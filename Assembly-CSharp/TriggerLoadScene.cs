using UnityEngine;

public class TriggerLoadScene : MonoBehaviour
{
	[SerializeField]
	private string m_changeSceneMessage = "change scene";

	[SerializeField]
	private string m_sceneName = "default";

	private void OnTrigger(string msg)
	{
		if (m_changeSceneMessage == msg)
		{
			GameUtils.LoadScene(m_sceneName);
		}
	}
}
