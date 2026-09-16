using UnityEngine;
using UnityEngine.PostProcessing;

public class WorldMapCamera : MonoBehaviour
{
	[SerializeField]
	private GameObject m_avatar;

	[SerializeField]
	private float m_gradientLimit = 0.5f;

	[SerializeField]
	private float m_timeToMax = 0.5f;

	[SerializeField]
	[AssignChild("FocalPoint", Editorbility.Editable)]
	private Transform m_focalPoint;

	[SerializeField]
	private Vector2 m_thresholdDistanceFromIdeal = new Vector2(7f, 3f);

	[SerializeField]
	[AssignComponent(Editorbility.Editable)]
	private PostProcessingBehaviour m_postProcessingBehaviour;

	private MapAvatarControls m_mapControls;

	private FollowCamera m_followCamera;

	public Vector3 AccessIdealOffset
	{
		get
		{
			return m_followCamera.IdealOffset;
		}
	}

	public GameObject AccessAvatar
	{
		get
		{
			return m_avatar;
		}
	}

	public Vector3 GetIdealLocation()
	{
		return m_followCamera.GetIdealLocation();
	}

	public void Awake()
	{
		m_mapControls = m_avatar.RequestComponent<MapAvatarControls>();
		m_followCamera = base.gameObject.AddComponent<FollowCamera>();
		m_followCamera.Target = m_avatar;
		m_followCamera.IdealOffset = base.transform.position - m_avatar.transform.position;
		m_followCamera.GradientLimit = m_gradientLimit;
		m_followCamera.TimeToMax = m_timeToMax;
		m_followCamera.enabled = base.enabled;
		if (m_postProcessingBehaviour != null)
		{
			m_postProcessingBehaviour.profile = Object.Instantiate(m_postProcessingBehaviour.profile);
		}
	}

	public void Initialise()
	{
		base.transform.position = GetIdealLocation();
	}

	private void OnEnable()
	{
		m_followCamera.enabled = true;
	}

	private void OnDisable()
	{
		m_followCamera.enabled = false;
	}

	private void Update()
	{
		Vector3 idealLocation = m_followCamera.GetIdealLocation();
		Vector2 a = idealLocation.XZ() - base.transform.position.XZ();
		float num = Mathf.Max(1f, a.DividedBy(m_thresholdDistanceFromIdeal).magnitude);
		float num2 = Mathf.Max(1f, m_mapControls.GetUnclampedMovementSpeed());
		m_followCamera.GradientLimit = Mathf.Max(num2 * m_gradientLimit, m_mapControls.GetSpeed()) * num;
		m_followCamera.TimeToMax = m_timeToMax / num2;
		if (m_focalPoint != null)
		{
			m_focalPoint.position = base.transform.position - m_followCamera.IdealOffset;
		}
	}
}
