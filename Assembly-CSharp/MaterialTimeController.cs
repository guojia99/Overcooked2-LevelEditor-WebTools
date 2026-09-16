using UnityEngine;

[ExecuteInEditMode]
public class MaterialTimeController : MonoBehaviour
{
	private int m_TimeID;

	private float m_controlledTime;

	private void Start()
	{
		m_TimeID = Shader.PropertyToID("_ControlledTime");
	}

	private void Update()
	{
		if (!TimeManager.IsPaused(TimeManager.PauseLayer.Main))
		{
			m_controlledTime += ClientTime.DeltaTime();
		}
		Shader.SetGlobalFloat(m_TimeID, m_controlledTime);
	}
}
