using UnityEngine;

public class PopupGUI : MonoBehaviour
{
	[SerializeField]
	private Rect m_textureRect = new Rect(0f, 0f, 100f, 100f);

	[SerializeField]
	private SubTexture2D m_texture;

	[SerializeField]
	[Multiline]
	private string m_message;

	[SerializeField]
	private Rect m_messageRect = new Rect(0f, 0f, 100f, 20f);

	[SerializeField]
	private float m_fadeInTime;

	[SerializeField]
	private Font m_font;

	private Camera m_mainCamera;

	private float m_alphaProp;

	private void Awake()
	{
		m_mainCamera = Camera.main;
	}

	private void Update()
	{
		if (m_fadeInTime > 0f)
		{
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			m_alphaProp = Mathf.Min(m_alphaProp + deltaTime / m_fadeInTime, 1f);
		}
		else
		{
			m_alphaProp = 1f;
		}
	}

	private void OnGUI()
	{
		GUI.color = new Color(1f, 1f, 1f, m_alphaProp);
		Rect rect = m_textureRect.Added(0.5f * (float)m_mainCamera.pixelWidth, 0.5f * (float)m_mainCamera.pixelHeight);
		Rect position = m_messageRect.Added(0.5f * (float)m_mainCamera.pixelWidth, 0.5f * (float)m_mainCamera.pixelHeight);
		m_texture.Draw(rect);
		GUIStyle gUIStyle = new GUIStyle("label");
		gUIStyle.font = m_font;
		gUIStyle.alignment = TextAnchor.MiddleCenter;
		gUIStyle.clipping = TextClipping.Overflow;
		gUIStyle.fontSize = (int)position.height / m_message.Split('\n').Length;
		GUI.Label(position, m_message, gUIStyle);
	}
}
