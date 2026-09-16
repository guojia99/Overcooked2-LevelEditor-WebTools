using UnityEngine;
using UnityEngine.UI;

public class AwardSceneUIController : MonoBehaviour
{
	[SerializeField]
	private Image m_sceneImage;

	[SerializeField]
	private Text m_name;

	public void SetData(SceneDirectoryData.SceneDirectoryEntry _scene)
	{
		m_sceneImage.sprite = _scene.SceneVarients[0].Screenshot;
		m_name.text = _scene.Label;
	}
}
