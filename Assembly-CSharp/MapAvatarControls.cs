using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MapAvatarGroundCast), typeof(MapAvatarTransformer))]
public class MapAvatarControls : MonoBehaviour
{
	[Serializable]
	public class MapAvatarRevTags
	{
		[SerializeField]
		public GameOneShotAudioTag m_landTag = GameOneShotAudioTag.VanRev;

		[SerializeField]
		public GameOneShotAudioTag m_waterTag = GameOneShotAudioTag.WorldMapBoatRev;

		[SerializeField]
		public GameOneShotAudioTag m_flyingTag = GameOneShotAudioTag.WorldMapPlaneRev;
	}

	[SerializeField]
	[AssignChild("Thomas", Editorbility.NonEditable)]
	public GameObject m_vanMesh;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	public Animator m_animator;

	[SerializeField]
	public float m_movementSpeed = 4f;

	[SerializeField]
	public float m_gravityStrength = 1f;

	[SerializeField]
	public float m_rotateSmoothing = 1f;

	[SerializeField]
	public float m_turningCircle = 0.5f;

	[SerializeField]
	public float m_dashTime = 0.3f;

	[SerializeField]
	public float m_dashCooldown = 0.3f;

	[SerializeField]
	public float m_dashSpeed = 12f;

	[SerializeField]
	public GameObject m_dashPFXPrefab;

	[SerializeField]
	public MapAvatarRevTags m_dashTags;

	[SerializeField]
	public float m_selectionRadius = 4f;

	[SerializeField]
	public LayerMask m_selectionLayerMask = -1;

	[SerializeField]
	public bool m_bStartOnFirstNode = true;

	private GridManager m_gridManager;

	private Transform m_previousParent;

	private Transform m_VanMeshTransform;

	private Transform m_Transform;

	private Quaternion m_originalVanRotation = default(Quaternion);

	private Vector3 m_PreviousPosition = default(Vector3);

	private Vector3 m_Velocity = default(Vector3);

	private float m_UnclampedMovementSpeed;

	private float m_DashTimer;

	private float m_Speed;

	public GridManager GridManager
	{
		get
		{
			return m_gridManager;
		}
	}

	private void Start()
	{
		m_gridManager = GameUtils.GetGridManager(base.transform);
		m_VanMeshTransform = m_vanMesh.transform;
		m_Transform = base.transform;
		m_PreviousPosition = m_Transform.position;
		m_previousParent = m_Transform.parent;
	}

	public float GetUnclampedMovementSpeed()
	{
		return m_UnclampedMovementSpeed;
	}

	public bool CalculateCurrentSelectable<T>(GridManager gridManager, MapAvatarGroundCast groundCast, out T selectable) where T : class
	{
		if (groundCast.HasGroundContact())
		{
			GridIndex gridLocationFromPos = gridManager.GetGridLocationFromPos(base.transform.position);
			GameObject gridOccupant = gridManager.GetGridOccupant(gridLocationFromPos);
			if (gridOccupant != null)
			{
				GridIndex unclampedGridLocationFromPos = gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
				GridIndex unclampedGridLocationFromPos2 = gridManager.GetUnclampedGridLocationFromPos(gridOccupant.transform.position);
				if (unclampedGridLocationFromPos == unclampedGridLocationFromPos2)
				{
					selectable = gridOccupant.RequestInterfaceRecursive<T>();
					if (selectable == null)
					{
						SelectableRedirect selectableRedirect = gridOccupant.RequestComponentRecursive<SelectableRedirect>();
						if (selectableRedirect != null)
						{
							selectable = ((!(selectableRedirect.m_target != null)) ? ((T)null) : selectableRedirect.m_target.gameObject.RequestInterfaceRecursive<T>());
						}
					}
					return true;
				}
			}
		}
		selectable = (T)null;
		return false;
	}

	public void SetOriginalVanRotation(Quaternion _originalRotation)
	{
		m_originalVanRotation = _originalRotation;
	}

	public void OrientateVan(float _deltaTime, Vector3 _groundNormal)
	{
		Vector3 normalized = Vector3.Cross(_groundNormal, m_Transform.forward).normalized;
		Vector3 normalized2 = Vector3.Cross(normalized, _groundNormal).normalized;
		Quaternion to = Quaternion.LookRotation(normalized2, _groundNormal) * m_originalVanRotation;
		Quaternion rotation = Quaternion.RotateTowards(m_VanMeshTransform.rotation, to, m_rotateSmoothing * _deltaTime);
		m_VanMeshTransform.rotation = rotation;
	}

	public void UpdateMovement()
	{
		if (m_previousParent != m_Transform.parent)
		{
			m_gridManager = GameUtils.GetGridManager(m_Transform);
			m_previousParent = m_Transform.parent;
		}
		Vector3 position = m_Transform.position;
		m_Velocity = position - m_PreviousPosition;
		m_PreviousPosition = position;
		CalculateSpeed();
		m_Velocity = m_Velocity.normalized * m_Speed;
		Vector3 velocity = m_Velocity;
		m_UnclampedMovementSpeed = (velocity - Vector3.Dot(velocity, Vector3.up) * Vector3.up).magnitude / m_movementSpeed;
	}

	public Vector3 GetVelocity()
	{
		return m_Velocity;
	}

	public void DashStarted()
	{
		m_DashTimer = m_dashTime;
	}

	public float GetSpeed()
	{
		return m_Speed;
	}

	private void CalculateSpeed()
	{
		if (m_Velocity.magnitude > 0.001f)
		{
			m_Speed = m_movementSpeed;
		}
		else
		{
			m_Speed = 0f;
		}
		if (m_DashTimer > 0f)
		{
			float num = MathUtils.SinusoidalSCurve(m_DashTimer / m_dashTime);
			m_Speed = (1f - num) * m_Speed + num * m_dashSpeed;
		}
		m_DashTimer -= TimeManager.GetDeltaTime(base.gameObject);
	}

	public IEnumerator DebugAutoLoadRandomLevel()
	{
		int lastLevelIndex = GameUtils.GetGameSession().Progress.SaveData.LastLevelEntered;
		yield return new WaitForSecondsRealtime(2f);
		LevelPortalMapNode[] allNodes = GameObjectUtils.FindComponentsOfTypeInScene<LevelPortalMapNode>();
		LevelPortalMapNode[] newNodes = allNodes.AllRemoved_Predicate((LevelPortalMapNode x) => x.LevelIndex == lastLevelIndex);
		newNodes = newNodes.AllRemoved_Predicate((LevelPortalMapNode x) => !x.m_sceneDirectoryEntry.HasScoreBoundaries);
		LevelPortalMapNode node = null;
		if (newNodes.Length > 0)
		{
			node = newNodes[UnityEngine.Random.Range(0, newNodes.Length)];
		}
		else
		{
			int num = allNodes.FindIndex_Predicate((LevelPortalMapNode x) => x.LevelIndex == lastLevelIndex);
			node = allNodes[num];
		}
		if (node != null)
		{
			base.gameObject.transform.position = node.transform.position;
			yield return new WaitForSecondsRealtime(2f);
			ServerWorldMapFlowController serverFlow = node.m_worldMapFlowController.GetComponent<ServerWorldMapFlowController>();
			if (serverFlow != null)
			{
				serverFlow.OnSelectLevelPortal(this, node);
			}
		}
	}
}
