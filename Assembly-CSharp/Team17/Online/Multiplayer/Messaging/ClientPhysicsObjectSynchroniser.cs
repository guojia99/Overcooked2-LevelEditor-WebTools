using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientPhysicsObjectSynchroniser : ClientWorldObjectSynchroniser
	{
		public class ContactingChef
		{
			public uint EntityID;

			public GameObject ChefGameObject;

			public ClientChefSynchroniser ChefSynchroniser;

			public PositionRecorder ChefPositionRecorder;

			public Vector3 RelativePosition;

			public Vector3 ContactVelocity;

			public bool LocallyControlled;

			public float ServerContactTime;
		}

		private enum State
		{
			ServerPosition = 0,
			PreLine = 1,
			Line = 2,
			PostLine = 3
		}

		private PhysicalAttachment m_PhysicalAttachment;

		private RigidbodyMotion m_RigidBodyMotion;

		private Vector3 m_ServerLocalPosition = Vector3.zero;

		private Vector3 m_ServerVelocity = Vector3.zero;

		private Vector3 m_ServerPreviousVelocity = Vector3.zero;

		private Quaternion m_ServerLocalRotation = Quaternion.identity;

		private int m_PlayerLayer;

		private Vector3 m_ChefLocalDirectionOnCollision = default(Vector3);

		private bool m_CollidingWithLocalChef;

		private bool m_PenetrationOccured;

		private Rigidbody m_LocalCollidingChefRigidbody;

		private bool m_WaitingOnRemoteCollisionInfo;

		private const float kRemoteCollisionWaitTime = 2f;

		private Collider[] m_Colliders;

		private List<Collider> m_ContactedChefColliders = new List<Collider>(2);

		private bool m_CollidersEnabled;

		private float m_RemoteCollisionConformationTimer;

		private bool m_RecentlyCollided;

		private Vector3 m_LinePosition = default(Vector3);

		public int m_ContactChefCount;

		public ContactingChef[] m_ContactingChefs = new ContactingChef[4];

		private MeshLerper m_meshLerper;

		private bool m_SnapRigidbodyOnDataReceive = true;

		private Transform m_ServerPositionTransform;

		private MultiplayerController m_MultiplayerController;

		private float m_TimeLastLocalCollision;

		private float m_TimeLastServerCollision;

		private float m_TimeStartedReceivingCollision;

		private float m_TimeEnteredPreLineState;

		private float m_TimeinLine;

		private float m_TimeLeftLineState;

		private float m_TimeEnteredLineState;

		private float m_TimeEnteredPostLineState;

		private float m_TimeAnyCollision;

		private float m_TimeLineStateEnd;

		private float m_TimeLastMessageReceived;

		private bool m_RemoteCollisionData;

		private bool m_bPreviousFrameCollision;

		private bool m_bFirstNetworkMessage = true;

		private float m_ChildAttachedTime;

		private bool m_PendingSetKinematicState;

		private State m_CurrentState;

		private Vector3 m_PreviousRelativeTarget = default(Vector3);

		private bool m_bNewLine = true;

		private bool m_bRenderLineSphere;

		private static NetworkPredictionTweekables Tweekables;

		public static float kCorrectiveForce = 0.2f;

		public override void Awake()
		{
			base.Awake();
			if (Tweekables == null)
			{
				Tweekables = GameUtils.RequireManager<MultiplayerController>().m_NetworkPredictionTweekables;
			}
			m_MultiplayerController = GameUtils.RequireManager<MultiplayerController>();
			m_PlayerLayer = LayerMask.NameToLayer("Players");
			for (int i = 0; i < m_ContactingChefs.Length; i++)
			{
				ContactingChef contactingChef = new ContactingChef();
				contactingChef.EntityID = 0u;
				contactingChef.ChefGameObject = null;
				contactingChef.LocallyControlled = false;
				m_ContactingChefs[i] = contactingChef;
			}
			m_RigidBodyMotion = GetComponent<RigidbodyMotion>();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_PhysicalAttachment = ((PhysicsObjectSynchroniser)synchronisedObject).GetPhysicalAttachment();
			m_Colliders = m_PhysicalAttachment.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < m_Colliders.Length; i++)
			{
				if (m_Colliders[i].isTrigger)
				{
					m_Colliders[i] = null;
				}
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.PhysicsObject;
		}

		public override void ApplyServerUpdate(Serialisable serialisable)
		{
			PhysicsObjectMessage physicsObjectMessage = (PhysicsObjectMessage)serialisable;
			base.ApplyServerUpdate((Serialisable)physicsObjectMessage.WorldObject);
			ReceiveServerUpdate(physicsObjectMessage);
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			PhysicsObjectMessage physicsObjectMessage = (PhysicsObjectMessage)serialisable;
			base.ApplyServerEvent((Serialisable)physicsObjectMessage.WorldObject);
			ReceiveServerUpdate(physicsObjectMessage);
		}

		private void ReceiveServerUpdate(PhysicsObjectMessage receivedData)
		{
			m_ServerLocalPosition = receivedData.WorldObject.LocalPosition;
			m_ServerLocalRotation = receivedData.WorldObject.LocalRotation;
			m_TimeLastMessageReceived = Time.time;
			if (m_bFirstNetworkMessage && m_meshLerper != null)
			{
				m_bFirstNetworkMessage = false;
				m_meshLerper.SnapToTarget(MeshLerper.Target.ServerPosition);
			}
			m_ContactChefCount = (int)receivedData.ContactCount;
			for (int i = 0; i < m_ContactChefCount; i++)
			{
				uint num = receivedData.Contacts[i];
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(num);
				ContactingChef contactingChef = m_ContactingChefs[i];
				if (entry != null)
				{
					contactingChef.EntityID = num;
					contactingChef.ChefGameObject = entry.m_GameObject;
					contactingChef.ChefPositionRecorder = entry.m_GameObject.GetComponent<PositionRecorder>();
					contactingChef.ChefSynchroniser = entry.m_GameObject.GetComponent<ClientChefSynchroniser>();
					contactingChef.ServerContactTime = receivedData.ContactTimes[i];
					contactingChef.RelativePosition = receivedData.RelativePositions[i];
					contactingChef.ContactVelocity = receivedData.ContactVelocitys[i];
					contactingChef.LocallyControlled = entry.m_GameObject.GetComponent<PlayerIDProvider>().IsLocallyControlled();
					if (contactingChef.LocallyControlled)
					{
						m_WaitingOnRemoteCollisionInfo = false;
					}
				}
			}
			m_ServerPreviousVelocity = m_ServerVelocity;
			m_ServerVelocity = receivedData.Velocity;
			if (!m_bPaused)
			{
				if (m_ContactChefCount > 0 && m_ChildAttachedTime + 0.1f >= Time.time)
				{
					m_SnapRigidbodyOnDataReceive = true;
					ChangeState(State.Line);
					m_TimeEnteredPreLineState = Time.time;
					m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.Line, 0.012f);
				}
				if (m_SnapRigidbodyOnDataReceive && m_meshLerper != null)
				{
					float num2 = 0.5f;
					Vector3 vector = GetGlobalServerPosition();
					for (int j = 0; j < m_ContactChefCount; j++)
					{
						ContactingChef contactingChef2 = m_ContactingChefs[j];
						Vector3 position = contactingChef2.ChefGameObject.transform.position;
						if (Vector3.Distance(position, vector) < num2)
						{
							vector = position + (vector - position).normalized * num2;
							break;
						}
					}
					if (m_PendingSetKinematicState && m_CurrentState == State.ServerPosition)
					{
						m_PendingSetKinematicState = false;
						m_RigidBodyMotion.SetKinematic(true);
					}
					m_Transform.position = vector;
					m_Transform.localRotation = m_ServerLocalRotation;
					m_RigidBodyMotion.SetVelocity(m_ServerVelocity);
				}
				ParentingLogic(receivedData.WorldObject);
			}
			if (m_meshLerper != null)
			{
				m_meshLerper.SetServerPosition(GetGlobalServerPosition(), GetServerRotation());
			}
		}

		public override void Pause()
		{
			m_bPaused = true;
			base.Pause();
			if (m_meshLerper != null)
			{
				m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.RigidBody, 0.2f);
			}
		}

		public override void OnResumeDataReceived(Serialisable _data)
		{
			PhysicsObjectMessage other = (PhysicsObjectMessage)_data;
			PhysicsObjectMessage physicsObjectMessage = new PhysicsObjectMessage();
			physicsObjectMessage.Copy(other);
			m_PendingResumeData = physicsObjectMessage;
		}

		protected override void ApplyResumeData(Serialisable _data)
		{
			PhysicsObjectMessage physicsObjectMessage = (PhysicsObjectMessage)_data;
			m_bPaused = false;
			ChangeState(State.ServerPosition);
			m_SnapRigidbodyOnDataReceive = true;
			base.ApplyResumeData(physicsObjectMessage.WorldObject);
			ReceiveServerUpdate(physicsObjectMessage);
			if (m_meshLerper != null)
			{
				m_meshLerper.SnapToTarget(MeshLerper.Target.ServerPosition);
			}
		}

		public virtual void FixedUpdate()
		{
			float fixedDeltaTime = Time.fixedDeltaTime;
			if (m_meshLerper == null && m_PhysicalAttachment != null)
			{
				m_meshLerper = m_PhysicalAttachment.m_meshLerper;
				if (m_meshLerper != null)
				{
					m_meshLerper.Initialise(this, GetComponent<Rigidbody>(), m_Transform, m_PhysicalAttachment);
					m_bFirstNetworkMessage = true;
				}
				if (m_ServerPositionTransform != null)
				{
					m_ServerPositionTransform.parent = m_Transform.parent;
				}
			}
			if (m_Transform != null && m_meshLerper != null)
			{
				RunChefRelativePrediction(fixedDeltaTime);
			}
			if (m_bPreviousFrameCollision && !m_CollidingWithLocalChef)
			{
				m_TimeLastLocalCollision = Time.time;
				m_TimeAnyCollision = m_TimeLastLocalCollision;
			}
			m_bPreviousFrameCollision = m_CollidingWithLocalChef;
			m_CollidingWithLocalChef = false;
		}

		private void RunChefRelativePrediction(float _delta)
		{
			if (m_bPaused)
			{
				return;
			}
			float latencyTime = GetLatencyTime();
			float time = Time.time;
			float roundTripTime = GetRoundTripTime();
			Vector3 globalServerPosition = GetGlobalServerPosition();
			Vector3 vector = GetGlobalServerPosition();
			bool flag = false;
			bool flag2 = false;
			Vector3 lhs = default(Vector3);
			float num = ((!m_CollidingWithLocalChef) ? 0f : m_LocalCollidingChefRigidbody.velocity.magnitude);
			Vector3 vector2 = default(Vector3);
			Vector3 vector3 = default(Vector3);
			m_bRenderLineSphere = false;
			for (int i = 0; i < m_ContactChefCount; i++)
			{
				ContactingChef contactingChef = m_ContactingChefs[i];
				if (contactingChef.LocallyControlled)
				{
					m_TimeLastServerCollision = time;
					flag2 = true;
					Vector3 desiredPosition = contactingChef.ChefSynchroniser.GetDesiredPosition();
					lhs = contactingChef.ChefGameObject.GetComponent<Rigidbody>().velocity.normalized;
					Vector3 position = m_Transform.position;
					Vector3 forward = contactingChef.ChefGameObject.transform.forward;
					Vector3 relativePosition = contactingChef.RelativePosition;
					vector2 = relativePosition;
					vector3 = contactingChef.ContactVelocity;
					Vector3 vector4 = default(Vector3);
					if (m_bNewLine)
					{
						vector4 = contactingChef.RelativePosition + desiredPosition;
						m_PreviousRelativeTarget = contactingChef.RelativePosition;
					}
					else
					{
						vector4 = Vector3.Lerp(m_PreviousRelativeTarget, contactingChef.RelativePosition, 0.5f) + desiredPosition;
						m_PreviousRelativeTarget = contactingChef.RelativePosition;
					}
					Vector2 vector5 = position.XZ();
					Vector2 vector6 = relativePosition.XZ();
					Vector2 vector7 = vector4.XZ();
					Vector2 normalized = vector6.normalized;
					Vector2 vector8 = vector7;
					Vector2 vector9 = vector5 - vector8;
					float num2 = Vector3.Dot(forward, relativePosition.normalized);
					float num3 = ((!(num2 > 0f)) ? 0f : (1f - num2));
					num3 *= latencyTime;
					float num4 = vector9.x * normalized.x + vector9.y * normalized.y;
					flag = num4 < 0f;
					num4 = Mathf.Max(0f, num4);
					globalServerPosition = new Vector3(vector8.x - num3 * normalized.x, position.y, vector8.y - num3 * normalized.y);
					vector = new Vector3(vector8.x + 0.1f * normalized.x, position.y, vector8.y + 0.1f * normalized.y);
					m_LinePosition = globalServerPosition;
					break;
				}
			}
			if (!flag2)
			{
				m_bNewLine = true;
				if (m_CurrentState == State.Line)
				{
					m_TimeAnyCollision = time;
				}
			}
			else if (!m_RemoteCollisionData)
			{
				m_TimeStartedReceivingCollision = time;
			}
			m_RemoteCollisionData = flag2;
			if (m_CurrentState == State.Line)
			{
				m_TimeinLine += _delta;
			}
			else
			{
				m_TimeinLine -= _delta;
			}
			m_TimeinLine = Mathf.Clamp(m_TimeinLine, 0f, 0.4f);
			m_RemoteCollisionConformationTimer += _delta;
			if (m_RemoteCollisionConformationTimer > 0.11f)
			{
				m_RecentlyCollided = false;
			}
			switch (m_CurrentState)
			{
			case State.ServerPosition:
				if (m_CollidersEnabled)
				{
					m_CollidersEnabled = false;
					for (int j = 0; j < m_ContactedChefColliders.Count; j++)
					{
						for (int k = 0; k < m_Colliders.Length; k++)
						{
							if (m_Colliders[k] != null)
							{
								Physics.IgnoreCollision(m_ContactedChefColliders[j], m_Colliders[k], false);
							}
						}
					}
					m_ContactedChefColliders.Clear();
				}
				m_SnapRigidbodyOnDataReceive = true;
				m_PenetrationOccured = false;
				if (m_CollidingWithLocalChef)
				{
					ChangeState(State.PreLine);
					m_TimeEnteredPreLineState = time;
					m_SnapRigidbodyOnDataReceive = false;
				}
				break;
			case State.PreLine:
			{
				Vector3 right = m_LocalCollidingChefRigidbody.transform.right;
				float num6 = Vector3.Dot(right, (m_meshLerper.transform.position - m_LocalCollidingChefRigidbody.position).normalized);
				float num7 = Vector3.Distance(m_LocalCollidingChefRigidbody.position, m_meshLerper.transform.position);
				bool flag4 = (num6 > Tweekables.PenetrationAngle || num6 < 0f - Tweekables.PenetrationAngle) && num7 < 1f * Tweekables.ChefRadius;
				bool flag5 = num7 < 0.7f * Tweekables.ChefRadius;
				bool flag6 = num > Tweekables.PenitrationMinSpeed;
				bool flag7 = flag4 || flag5 || flag6;
				if (!m_PenetrationOccured && flag7 && num > 2.5f)
				{
					m_PenetrationOccured = true;
					m_meshLerper.SetTargets(MeshLerper.Target.ServerPosition, MeshLerper.Target.RigidBody, latencyTime * 3.5f);
				}
				if (flag2)
				{
					if (Vector3.Dot(vector3.normalized, m_ServerVelocity.normalized) > 0.8f)
					{
						float num8 = Vector3.Dot(lhs, vector2.normalized);
						if (num8 >= 0.5f)
						{
							ChangeState(State.Line);
							m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.Line, 0.2f);
							m_TimeEnteredLineState = time;
							m_SnapRigidbodyOnDataReceive = false;
						}
						else
						{
							ChangeState(State.ServerPosition);
							m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.ServerPosition, 0.2f);
							m_SnapRigidbodyOnDataReceive = true;
						}
					}
					else
					{
						ChangeState(State.ServerPosition);
						m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.ServerPosition, 0.2f);
						m_SnapRigidbodyOnDataReceive = true;
					}
				}
				else if (m_TimeEnteredPreLineState + roundTripTime * 2f < time)
				{
					m_SnapRigidbodyOnDataReceive = true;
					m_WaitingOnRemoteCollisionInfo = false;
					ChangeState(State.ServerPosition);
					m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.ServerPosition, 0.2f);
					float num9 = Mathf.Min((m_Transform.localPosition - m_ServerLocalPosition).magnitude * 1f, 1f);
					m_Transform.localPosition = (m_ServerLocalPosition - m_Transform.localPosition) * num9 + m_Transform.localPosition;
					m_Transform.localRotation = m_ServerLocalRotation;
				}
				else
				{
					m_SnapRigidbodyOnDataReceive = false;
				}
				break;
			}
			case State.Line:
			{
				bool flag3 = false;
				if (!flag2)
				{
					flag3 = true;
				}
				else
				{
					m_SnapRigidbodyOnDataReceive = true;
					m_bRenderLineSphere = true;
					vector2.y = 0f;
					lhs.y = 0f;
					float num5 = Vector3.Dot(lhs, vector2.normalized);
					if (num5 < Tweekables.ChefMovingTowardsUsAngle)
					{
						flag3 = true;
					}
					if (!flag3)
					{
						if (flag)
						{
							Vector3 additionalVelocity = (vector - m_Transform.position) * kCorrectiveForce;
							m_Transform.localRotation = m_ServerLocalRotation;
							m_RigidBodyMotion.AddVelocity(additionalVelocity);
						}
						else
						{
							Vector3 additionalVelocity2 = (vector - m_Transform.position) * kCorrectiveForce;
							m_Transform.localRotation = m_ServerLocalRotation;
							m_RigidBodyMotion.AddVelocity(additionalVelocity2);
						}
					}
				}
				if (!flag3)
				{
					break;
				}
				m_TimeLeftLineState = time;
				if (m_TimeinLine < 0.2f)
				{
					ChangeState(State.ServerPosition);
					m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.ServerPosition, 0.1f);
					m_SnapRigidbodyOnDataReceive = true;
					break;
				}
				ChangeState(State.PostLine);
				m_CollidersEnabled = true;
				for (int l = 0; l < m_ContactedChefColliders.Count; l++)
				{
					for (int m = 0; m < m_Colliders.Length; m++)
					{
						if (m_Colliders[m] != null)
						{
							Physics.IgnoreCollision(m_ContactedChefColliders[l], m_Colliders[m], true);
						}
					}
				}
				m_TimeEnteredPostLineState = time;
				m_SnapRigidbodyOnDataReceive = true;
				m_meshLerper.SetTargets(MeshLerper.Target.CurrentPosition, MeshLerper.Target.ServerPosition, 0.1f);
				break;
			}
			case State.PostLine:
				m_SnapRigidbodyOnDataReceive = false;
				if (time > m_TimeEnteredPostLineState + 0.2f)
				{
					ChangeState(State.ServerPosition);
				}
				if (m_CollidingWithLocalChef)
				{
					ChangeState(State.PreLine);
					m_TimeEnteredPreLineState = time;
				}
				break;
			}
		}

		private void ChangeState(State _state)
		{
			m_CurrentState = _state;
		}

		public void ChildRemoved(ClientWorldObjectSynchroniser _synchroniser)
		{
			if (m_PhysicalAttachment != null)
			{
				m_PhysicalAttachment.UseStaticPositioning();
			}
			m_RigidBodyMotion.SetKinematic(true);
		}

		public void ChildAttached(ClientWorldObjectSynchroniser _synchroniser)
		{
			m_ChildAttachedTime = Time.time;
			if (m_PhysicalAttachment != null)
			{
				m_PhysicalAttachment.UseMeshLerp();
			}
			m_RigidBodyMotion.SetKinematic(true);
		}

		private void OnCollisionStay(Collision collision)
		{
			if (m_bPaused)
			{
				return;
			}
			GameObject gameObject = collision.collider.gameObject;
			PlayerIDProvider playerIDProvider = gameObject.RequestComponent<PlayerIDProvider>();
			if (gameObject.layer != m_PlayerLayer || !(playerIDProvider != null) || !playerIDProvider.IsLocallyControlled())
			{
				return;
			}
			m_LocalCollidingChefRigidbody = gameObject.GetComponent<Rigidbody>();
			if (!m_PendingSetKinematicState)
			{
				m_RigidBodyMotion.SetKinematic(false);
				m_PendingSetKinematicState = true;
			}
			if (!m_CollidingWithLocalChef && !m_bPreviousFrameCollision && m_LocalCollidingChefRigidbody.velocity.magnitude > 0.2f)
			{
				if (!m_WaitingOnRemoteCollisionInfo)
				{
					m_WaitingOnRemoteCollisionInfo = true;
				}
				m_ChefLocalDirectionOnCollision = m_RigidBodyMotion.GetVelocity().normalized;
			}
			if (m_RemoteCollisionConformationTimer < 0.1f)
			{
				m_RecentlyCollided = true;
			}
			m_RemoteCollisionConformationTimer = 0f;
			m_CollidingWithLocalChef = true;
			if (!m_ContactedChefColliders.Contains(collision.collider))
			{
				m_ContactedChefColliders.Add(collision.collider);
			}
		}

		public float GetRoundTripTime()
		{
			return m_MultiplayerController.GetClientConnectionStats(false).m_fLatency * 2f;
		}

		public float GetLatencyTime()
		{
			return m_MultiplayerController.GetClientConnectionStats(false).m_fLatency;
		}

		public Vector3 GetLinePosition()
		{
			return m_LinePosition;
		}

		public Vector3 GetServerVelocity()
		{
			return m_ServerVelocity;
		}

		public Vector3 ServerPreviousVelocity()
		{
			return m_ServerPreviousVelocity;
		}

		public Vector3 GetServerPosition()
		{
			if (m_CurrentState != State.Line && m_TimeinLine > 0.2f)
			{
				return GetGlobalServerPosition() + m_ServerVelocity * GetLatencyTime() * 0.75f * Mathf.Clamp((m_TimeinLine - 0.2f) / 0.2f, 0f, 1f);
			}
			return GetGlobalServerPosition();
		}

		public Vector3 GetExtrapolatedServerPosition()
		{
			return GetGlobalServerPosition() + m_ServerVelocity * (Time.time - m_TimeLastMessageReceived);
		}

		public Quaternion GetServerRotation()
		{
			return m_ServerLocalRotation;
		}

		public static string GetDebugString()
		{
			return string.Empty;
		}
	}
}
