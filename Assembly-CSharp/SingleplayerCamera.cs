using UnityEngine;

[ExecutionDependency(typeof(PlayerControls))]
[ExecutionDependency(typeof(PlayerSwitchingManager))]
public class SingleplayerCamera : MonoBehaviour
{
	private enum Mode
	{
		Normal = 0,
		Switching = 1
	}

	[SerializeField]
	private float m_idealDistance = 5f;

	[SerializeField]
	private Vector3 m_targetOffset = new Vector3(0f, 1f, 0f);

	[SerializeField]
	private float m_normalAccelTime = 1f;

	[SerializeField]
	private float m_normalFollowSpeed = 5f;

	[SerializeField]
	private float m_switchAccelTime = 1f;

	[SerializeField]
	private float m_switchFollowSpeed = 10f;

	[SerializeField]
	private float m_switchCompleteDistance = 0.1f;

	private Mode m_mode;

	private FollowCamera m_followCamera;

	private PlayerSwitchingManager m_playerSwitchingManager;

	private void Awake()
	{
		m_followCamera = base.gameObject.AddComponent<FollowCamera>();
	}

	private void Start()
	{
		m_playerSwitchingManager = GameUtils.RequireManager<PlayerSwitchingManager>();
		m_playerSwitchingManager.AvatarSelectChangeCallback += OnAvatarSelectionChanged;
		m_followCamera.Target = m_playerSwitchingManager.SelectedAvatar(PlayerInputLookup.Player.One).gameObject;
		Vector3 vector = base.transform.rotation * ((0f - m_idealDistance) * Vector3.forward);
		m_followCamera.IdealOffset = m_targetOffset + vector;
		SetMode(Mode.Normal);
	}

	private void OnAvatarSelectionChanged(PlayerInputLookup.Player _player, PlayerControls _controls)
	{
		m_followCamera.Target = _controls.gameObject;
		SetMode(Mode.Switching);
	}

	private void SetMode(Mode _mode)
	{
		switch (_mode)
		{
		case Mode.Normal:
			UpdateNormalMode();
			break;
		case Mode.Switching:
			m_followCamera.GradientLimit = m_switchFollowSpeed;
			m_followCamera.TimeToMax = m_switchAccelTime;
			break;
		}
		m_mode = _mode;
	}

	private void UpdateNormalMode()
	{
		PlayerControls playerControls = m_followCamera.Target.RequireComponent<PlayerControls>();
		float num = Mathf.Max(1f, playerControls.GetUnclampedMovementSpeed());
		m_followCamera.GradientLimit = num * m_normalFollowSpeed;
		m_followCamera.TimeToMax = m_normalAccelTime / num;
	}

	private void Update()
	{
		if (m_mode == Mode.Switching)
		{
			float sqrMagnitude = (m_followCamera.GetIdealLocation() - base.transform.position).sqrMagnitude;
			float num = m_switchCompleteDistance * m_switchCompleteDistance;
			if (sqrMagnitude < num)
			{
				SetMode(Mode.Normal);
			}
		}
		else
		{
			UpdateNormalMode();
		}
	}
}
