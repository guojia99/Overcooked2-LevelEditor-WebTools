using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientChefSynchroniser : ClientWorldObjectSynchroniser
	{
		private Vector3 m_ServerVelocity = Vector3.zero;

		private Rigidbody m_Rigidbody;

		private PositionRecorder m_PositionRecorder;

		private RemoteChefPositionRecorder m_RemoteChefPositionRecorder;

		private bool m_bLocallyControlled;

		private PlayerControls m_PlayerControls;

		private PlayerControls.ControlSchemeData m_ControlScheme;

		private ChefLerp m_ChefLerp;

		private Vector3 m_ServerLocalPosition = default(Vector3);

		private Vector3 m_ServerLastLocalPosition = default(Vector3);

		private Vector3 m_ServerLastGlobalPosition = default(Vector3);

		private Vector3 m_ServerTrueLastGlobalPosition = default(Vector3);

		private Vector3 m_ServerGlobalPosition = default(Vector3);

		private Vector3 m_QueuedServerGlobalPosition = default(Vector3);

		private Vector3 m_QueuedServerLocalPosition = default(Vector3);

		private Transform m_ServerParent;

		private Vector3 m_ParentPreviousPosition = default(Vector3);

		private Quaternion m_ParentPreviousRotation = default(Quaternion);

		private Transform m_PreviousServerParent;

		private float m_ServerTime;

		private float m_ReceivedTimeStamp;

		private float m_QueuedReceivedTimeStamp;

		private float m_LastReceivedTimeStamp;

		private float m_TrueLastReceivedTimeStamp;

		private bool m_Go;

		public CapsuleCollisionHelper m_CapsuleCollision;

		public static float CorrectionForce = 4f;

		private Vector3 m_DesiredPosition = default(Vector3);

		private Vector3 m_DesiredLocalPosition = default(Vector3);

		private bool m_bLerpRigidBody;

		private MultiplayerController m_MultiPlayerController;

		private bool m_DisableDynamicReparenting;

		private float m_ResumeTime;

		private float m_RemoteResumeTime;

		public float m_RemoteServerTime;

		private Transform m_ServerLastParent;

		private bool m_bParentHasMoved;

		private bool m_bDoCorrection = true;

		private Transform m_LocalCurrentParent;

		public static string DEBUGSTRING = string.Empty;

		public Vector3 GetDesiredPosition()
		{
			return m_DesiredPosition;
		}

		public Vector3 GetServerGlobalPosition()
		{
			if (m_DisableDynamicReparenting)
			{
				if (m_Transform.parent != null)
				{
					return m_Transform.parent.position + m_Transform.parent.rotation * m_ServerLocalPosition;
				}
			}
			else if (m_ServerParent != null)
			{
				return m_ServerParent.transform.position + m_ServerParent.transform.rotation * m_ServerLocalPosition;
			}
			return m_ServerLocalPosition;
		}

		public Vector3 GetServerVelocity()
		{
			return m_ServerVelocity;
		}

		protected Vector3 GetLastServerGlobalPosition()
		{
			if (m_DisableDynamicReparenting)
			{
				if (m_Transform.parent != null)
				{
					return MathUtils.MultiplyByMatrix(m_Transform.parent.localToWorldMatrix, m_ServerLastLocalPosition);
				}
			}
			else if (m_ServerLastParent != null)
			{
				return MathUtils.MultiplyByMatrix(m_ServerLastParent.localToWorldMatrix, m_ServerLastLocalPosition);
			}
			return m_ServerLastLocalPosition;
		}

		public override void Awake()
		{
			base.Awake();
			m_DisableDynamicReparenting = GameUtils.GetLevelConfig().m_disableDynamicParenting;
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
			m_Rigidbody = GetComponent<Rigidbody>();
			m_MultiPlayerController = GameUtils.RequireManager<MultiplayerController>();
		}

		protected override void OnDestroy()
		{
			Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
			base.OnDestroy();
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartEntities)
			{
				ChefSetup();
				if (m_bLocallyControlled)
				{
					m_ChefLerp = m_Transform.Find("Chef").gameObject.AddComponent<ChefLerp>();
					m_ChefLerp.Setup(m_Rigidbody);
				}
				else
				{
					m_Lerper = base.gameObject.AddComponent<BasicLerp>();
					m_Lerper.StartSynchronising(GetSynchronisedComponent());
				}
			}
			if (gameStateMessage.m_State == GameState.InLevel)
			{
				m_Go = true;
			}
		}

		private void ChefSetup()
		{
			PlayerIDProvider playerIDProvider = base.gameObject.RequireComponent<PlayerIDProvider>();
			m_bLocallyControlled = playerIDProvider.IsLocallyControlled();
			if (m_bLocallyControlled)
			{
				m_CapsuleCollision = base.gameObject.AddComponent<CapsuleCollisionHelper>();
				m_PositionRecorder = base.gameObject.RequireComponent<PositionRecorder>();
				m_PlayerControls = GetComponent<PlayerControls>();
				m_ControlScheme = m_PlayerControls.ControlScheme;
				m_PositionRecorder.Setup(GetLocalCurrentParent());
			}
			else
			{
				m_PositionRecorder = null;
				m_RemoteChefPositionRecorder = base.gameObject.AddComponent<RemoteChefPositionRecorder>();
				m_Rigidbody.isKinematic = true;
			}
		}

		private Transform GetLocalCurrentParent()
		{
			if (m_DisableDynamicReparenting)
			{
				return m_Transform.parent;
			}
			return m_LocalCurrentParent;
		}

		private void OnGroundChanged(Collider _collider)
		{
			if (_collider != null)
			{
				IParentable parentable = _collider.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
				if (parentable != null)
				{
					m_LocalCurrentParent = parentable.GetAttachPoint(base.gameObject);
				}
				else
				{
					m_LocalCurrentParent = null;
				}
			}
			else
			{
				m_LocalCurrentParent = null;
			}
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			BuildDebugString();
			if (!m_DisableDynamicReparenting)
			{
				GroundCast component = GetComponent<GroundCast>();
				component.RegisterGroundChangedCallback(OnGroundChanged);
				OnGroundChanged(component.GetGroundCollider());
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.Chef;
		}

		public override void ApplyServerUpdate(Serialisable serialisable)
		{
			HandleMessage((ChefPositionMessage)serialisable);
			m_bHasEverReceived = true;
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			HandleMessage((ChefPositionMessage)serialisable, true);
			m_bHasEverReceived = true;
		}

		private void HandleMessage(ChefPositionMessage dataReceived, bool bSetStationary = false)
		{
			if (m_bLocallyControlled)
			{
				LocalChefReceiveData(dataReceived);
			}
			else
			{
				if (m_bPaused)
				{
					return;
				}
				m_RemoteServerTime = dataReceived.NetworkTime;
				if (!(m_RemoteResumeTime <= m_RemoteServerTime))
				{
					return;
				}
				if (bSetStationary)
				{
					m_ServerVelocity.Set(0f, 0f, 0f);
				}
				else
				{
					m_ServerVelocity = dataReceived.Velocity;
				}
				base.ApplyServerUpdate((Serialisable)dataReceived.WorldObject);
				if (m_RemoteChefPositionRecorder != null)
				{
					Vector3 localPosition = dataReceived.WorldObject.LocalPosition;
					if (m_Transform.parent != null)
					{
						localPosition += m_Transform.parent.position;
					}
					m_RemoteChefPositionRecorder.RecordData(Time.time, localPosition, m_Rigidbody.velocity);
				}
			}
		}

		private void LocalChefReceiveData(ChefPositionMessage dataReceived)
		{
			if (!(m_PositionRecorder != null) || m_bPaused)
			{
				return;
			}
			WorldObjectMessage worldObject = dataReceived.WorldObject;
			m_ServerLastLocalPosition = m_ServerLocalPosition;
			m_ServerLocalPosition = worldObject.LocalPosition;
			m_ServerLastParent = m_ServerParent;
			if (!m_DisableDynamicReparenting)
			{
				ParentingLogic(worldObject);
				m_ServerParent = m_Transform.parent;
			}
			if (m_ServerParent != m_PreviousServerParent)
			{
				Matrix4x4 matrix4x = Matrix4x4.identity;
				Matrix4x4 matrix4x2 = Matrix4x4.identity;
				if (m_PreviousServerParent != null)
				{
					matrix4x = m_PreviousServerParent.localToWorldMatrix;
				}
				if (m_ServerParent != null)
				{
					matrix4x2 = m_ServerParent.worldToLocalMatrix;
				}
				m_QueuedServerLocalPosition = MathUtils.MultiplyByMatrix(matrix4x2 * matrix4x, m_QueuedServerLocalPosition);
				m_PreviousServerParent = m_ServerParent;
			}
			if (m_ServerParent != null)
			{
				m_ParentPreviousPosition = m_ServerParent.position;
				m_ParentPreviousRotation = m_ServerParent.rotation;
				m_QueuedServerGlobalPosition = MathUtils.MultiplyByMatrix(m_ServerParent.localToWorldMatrix, m_QueuedServerLocalPosition);
			}
			m_ServerLastGlobalPosition = m_QueuedServerGlobalPosition;
			m_ServerGlobalPosition = GetServerGlobalPosition();
			m_ServerTime = dataReceived.NetworkTime;
			m_LastReceivedTimeStamp = m_QueuedReceivedTimeStamp;
			m_ReceivedTimeStamp = dataReceived.ClientTimeStamp;
			m_bParentHasMoved = false;
		}

		public override void OnResumeDataReceived(Serialisable _data)
		{
			ChefPositionMessage other = (ChefPositionMessage)_data;
			ChefPositionMessage chefPositionMessage = new ChefPositionMessage();
			chefPositionMessage.Copy(other);
			m_PendingResumeData = chefPositionMessage;
			m_bDoCorrection = true;
		}

		protected override void ApplyResumeData(Serialisable _data)
		{
			ChefPositionMessage chefPositionMessage = (ChefPositionMessage)_data;
			m_bPaused = false;
			if (m_bLocallyControlled)
			{
				LocalChefReceiveData(chefPositionMessage);
				if (m_PositionRecorder != null)
				{
					m_PositionRecorder.Clear(GetLocalCurrentParent());
					m_QueuedServerGlobalPosition = GetServerGlobalPosition();
					m_QueuedReceivedTimeStamp = chefPositionMessage.ClientTimeStamp;
					if (!m_CapsuleCollision.CheckCapsule(m_QueuedServerGlobalPosition))
					{
					}
				}
			}
			else
			{
				m_RemoteResumeTime = chefPositionMessage.NetworkTime;
				m_RemoteServerTime = chefPositionMessage.NetworkTime;
				m_ServerVelocity.Set(0f, 0f, 0f);
				m_Rigidbody.velocity = m_ServerVelocity;
				base.ApplyResumeData(chefPositionMessage.WorldObject);
			}
			m_bDoCorrection = true;
			m_ResumeTime = Time.time;
		}

		public virtual void FixedUpdate()
		{
			if (m_bLocallyControlled)
			{
				RunCorrection();
			}
		}

		private bool ControlsMovingPlayer()
		{
			return m_ControlScheme != null && m_PlayerControls != null && (m_ControlScheme.m_moveX.GetValue() != 0f || m_ControlScheme.m_moveY.GetValue() != 0f || m_PlayerControls.IsDashing());
		}

		private void RunCorrection()
		{
			Transform localCurrentParent = GetLocalCurrentParent();
			if (!m_bDoCorrection)
			{
				if (ControlsMovingPlayer())
				{
					m_bDoCorrection = true;
				}
				else if (!m_bPaused)
				{
					if (!m_DisableDynamicReparenting)
					{
						m_Transform.parent = m_ServerParent;
					}
					m_Transform.localPosition = m_ServerLocalPosition;
					m_PositionRecorder.TakeSample(TimeManager.GetFixedDeltaTime(base.gameObject), localCurrentParent, m_ReceivedTimeStamp);
					return;
				}
			}
			if (!m_Go || m_bPaused || !m_bLocallyControlled || !(m_PositionRecorder != null) || !(m_ChefLerp != null) || !(m_Transform != null) || !m_bHasEverReceived)
			{
				return;
			}
			m_PositionRecorder.TakeSample(TimeManager.GetFixedDeltaTime(base.gameObject), localCurrentParent, m_ReceivedTimeStamp);
			if (!(m_ResumeTime <= m_ReceivedTimeStamp))
			{
				return;
			}
			Vector3 serverGlobalPosition = GetServerGlobalPosition();
			if (m_ServerParent != null && ((m_ServerParent.position - m_ParentPreviousPosition).magnitude > float.Epsilon || m_ServerParent.rotation != m_ParentPreviousRotation))
			{
				m_ParentPreviousPosition = m_ServerParent.position;
				m_ParentPreviousRotation = m_ServerParent.rotation;
				m_QueuedServerGlobalPosition = MathUtils.MultiplyByMatrix(m_ServerParent.localToWorldMatrix, m_QueuedServerLocalPosition);
				m_bParentHasMoved = true;
			}
			bool flag = m_CapsuleCollision.CheckCapsule(serverGlobalPosition);
			Vector3 vector = serverGlobalPosition;
			Vector3 vector2 = m_PositionRecorder.CalculateLocalPosition(m_Transform.position, localCurrentParent);
			float num = m_ReceivedTimeStamp;
			Vector3 lastServerGlobalPosition = GetLastServerGlobalPosition();
			bool flag2 = false;
			if (m_bParentHasMoved)
			{
				flag2 = m_CapsuleCollision.CheckCapsule(lastServerGlobalPosition);
				if (flag2)
				{
					vector = lastServerGlobalPosition;
					num = m_ReceivedTimeStamp;
				}
			}
			if (!flag2)
			{
				float magnitude = (serverGlobalPosition - lastServerGlobalPosition).magnitude;
				float b = 0.03f / magnitude;
				float num2 = Mathf.Max(0.05f, b);
				float num3 = Mathf.Max(1f - num2, 0.1f);
				while (flag && num3 >= 0f)
				{
					vector = lastServerGlobalPosition + num3 * (serverGlobalPosition - lastServerGlobalPosition);
					flag = m_CapsuleCollision.CheckCapsule(vector);
					num = m_LastReceivedTimeStamp + num3 * (m_ReceivedTimeStamp - m_LastReceivedTimeStamp);
					num2 *= 2f;
					num3 -= num2;
				}
				if (num3 < 0f)
				{
					num3 = 0f;
					vector = lastServerGlobalPosition + num3 * (serverGlobalPosition - lastServerGlobalPosition);
					flag = m_CapsuleCollision.CheckCapsule(vector);
					num = m_LastReceivedTimeStamp + num3 * (m_ReceivedTimeStamp - m_LastReceivedTimeStamp);
				}
				if (m_CapsuleCollision.CheckCapsule(vector))
				{
					vector = lastServerGlobalPosition + 1.05f * (serverGlobalPosition - lastServerGlobalPosition);
					num = m_LastReceivedTimeStamp + 1.05f * (m_ReceivedTimeStamp - m_LastReceivedTimeStamp);
					if (m_CapsuleCollision.CheckCapsule(vector))
					{
						vector = serverGlobalPosition;
						num = m_ReceivedTimeStamp;
					}
				}
			}
			m_QueuedServerGlobalPosition = vector;
			if (m_ServerParent != null)
			{
				m_QueuedServerLocalPosition = MathUtils.MultiplyByMatrix(m_ServerParent.worldToLocalMatrix, vector);
			}
			m_QueuedReceivedTimeStamp = num;
			if (flag)
			{
			}
			Vector3 lagCompensatedPositionDeltaParents = m_PositionRecorder.GetLagCompensatedPositionDeltaParents(num, vector, ref m_DesiredPosition);
			m_DesiredLocalPosition = m_PositionRecorder.CalculateLocalPosition(m_DesiredPosition, localCurrentParent);
			Quaternion quaternion = Quaternion.identity;
			if (localCurrentParent != null)
			{
				quaternion = localCurrentParent.localToWorldMatrix.rotation;
			}
			bool flag3 = m_CapsuleCollision.CheckCapsule(m_DesiredPosition);
			if (flag3)
			{
			}
			Vector3 vector3 = m_DesiredLocalPosition - vector2;
			if (!m_bLerpRigidBody)
			{
				float y = vector3.y;
				float num4 = y * y;
				float x = vector3.x;
				float z = vector3.z;
				float num5 = x * x + z * z;
				RaycastHit[] array = Physics.SphereCastAll(m_Transform.position, 0.1f, m_Transform.up * -1f, 2f, (1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("SlopedGround")));
				if (num4 > num5 + 0.1f && array.Length == 0)
				{
					m_bLerpRigidBody = true;
					m_Rigidbody.velocity = Vector3.zero;
					Transform transform = null;
					if (m_DisableDynamicReparenting)
					{
						transform = m_Transform.parent;
					}
					else
					{
						m_Transform.parent = m_ServerParent;
						transform = m_ServerParent;
					}
					if (transform != null)
					{
						m_DesiredPosition = MathUtils.MultiplyByMatrix(transform.localToWorldMatrix, m_ServerLocalPosition);
					}
					else
					{
						m_DesiredPosition = m_ServerLocalPosition;
					}
				}
				else if (vector3.sqrMagnitude > 6.25f && !flag3)
				{
					m_bLerpRigidBody = true;
					m_Rigidbody.velocity = Vector3.zero;
				}
				else
				{
					if (vector3.magnitude <= 0.1f && !ControlsMovingPlayer())
					{
						m_Rigidbody.velocity = Vector3.zero;
						m_bLerpRigidBody = true;
					}
					else
					{
						float num6 = Mathf.Lerp(1f, 2f, (vector3.magnitude - 1.25f) / 1.25f);
						Vector3 vector4 = CorrectionForce * num6 * vector3;
						m_Rigidbody.velocity += quaternion * vector4;
						m_PositionRecorder.AddAjustedVelocity(vector4);
					}
					m_bLerpRigidBody = false;
					if (Vector3.Dot(m_Transform.forward, vector3.normalized) > -0.950002f && m_CapsuleCollision.CheckPathToPoint(m_Transform.position, m_DesiredPosition) && !flag3)
					{
						m_bLerpRigidBody = true;
					}
				}
			}
			if (m_bLerpRigidBody)
			{
				m_PositionRecorder.Teleport(m_DesiredPosition, localCurrentParent);
				m_bDoCorrection = false;
				m_bLerpRigidBody = false;
			}
		}

		public static void BuildDebugString()
		{
			DEBUGSTRING = ClientPhysicsObjectSynchroniser.GetDebugString();
		}
	}
}
