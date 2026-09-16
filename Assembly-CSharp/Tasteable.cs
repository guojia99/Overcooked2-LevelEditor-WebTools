using UnityEngine;

[RequireComponent(typeof(IngredientContentGUI))]
public class Tasteable : MonoBehaviour
{
	[SerializeField]
	private float m_tasteUITime = 5f;

	private IngredientContentGUI m_gui;

	private float m_timer;

	public void Taste()
	{
		m_timer = m_tasteUITime;
		m_gui.enabled = true;
	}

	private void Awake()
	{
		m_gui = base.gameObject.RequireComponent<IngredientContentGUI>();
		m_gui.enabled = false;
	}

	private void Update()
	{
		if (m_timer > 0f)
		{
			m_timer -= TimeManager.GetDeltaTime(base.gameObject);
			m_gui.enabled = m_timer > 0f;
		}
		else
		{
			m_gui.enabled = false;
		}
	}
}
