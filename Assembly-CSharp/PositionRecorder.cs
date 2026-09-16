using System.Collections.Generic;
using UnityEngine;

public class PositionRecorder : MonoBehaviour
{
	private class MovementDelta
	{
		public Vector3 PositionDelta = default(Vector3);

		public Vector3 OriginalDelta = default(Vector3);

		public float FrameTime;

		public float NetworkTime = float.MinValue;

		public Transform Parent;

		public MovementDelta(MovementDelta delta)
		{
			PositionDelta = delta.PositionDelta;
			FrameTime = delta.FrameTime;
			NetworkTime = delta.NetworkTime;
			Parent = delta.Parent;
			OriginalDelta = delta.OriginalDelta;
		}

		public MovementDelta()
		{
			Reset();
		}

		public void CopyData(MovementDelta delta)
		{
			PositionDelta = delta.PositionDelta;
			FrameTime = delta.FrameTime;
			NetworkTime = delta.NetworkTime;
			Parent = delta.Parent;
			OriginalDelta = delta.OriginalDelta;
		}

		public void Reset()
		{
			NetworkTime = float.MinValue;
			FrameTime = 0f;
			PositionDelta.Set(0f, 0f, 0f);
			OriginalDelta.Set(0f, 0f, 0f);
			Parent = null;
		}
	}

	private class ParentSpace
	{
		public Transform Parent;

		public List<MovementDelta> Segments = new List<MovementDelta>();

		public Matrix4x4 exitTransform = Matrix4x4.identity;

		public Vector3 FromWorldToMe(Vector3 position)
		{
			if (Parent == null)
			{
				return position;
			}
			return Parent.worldToLocalMatrix * new Vector4(position.x, position.y, position.z, 1f);
		}

		public Vector3 MeToWorld(Vector3 position)
		{
			if (Parent == null)
			{
				return position;
			}
			return Parent.localToWorldMatrix * new Vector4(position.x, position.y, position.z, 1f);
		}

		public void Setup(Transform parent)
		{
			Parent = parent;
			Segments.Clear();
			exitTransform = Matrix4x4.identity;
		}
	}

	private class ParentSpaceCache
	{
		private Stack<ParentSpace> parentSpaces = new Stack<ParentSpace>();

		private Stack<MovementDelta> segments = new Stack<MovementDelta>();

		public ParentSpaceCache()
		{
			for (int i = 0; i < 100; i++)
			{
				segments.Push(new MovementDelta());
			}
			for (int j = 0; j < 5; j++)
			{
				parentSpaces.Push(new ParentSpace());
			}
		}

		public ParentSpace GetSpace()
		{
			if (parentSpaces.Count == 0)
			{
				parentSpaces.Push(new ParentSpace());
			}
			return parentSpaces.Pop();
		}

		public void ReturnSpace(ParentSpace space)
		{
			parentSpaces.Push(space);
		}

		public MovementDelta GetSegment()
		{
			if (segments.Count == 0)
			{
				return null;
			}
			return segments.Pop();
		}

		public void ReturnSegment(MovementDelta delta)
		{
			segments.Push(delta);
		}
	}

	private List<ParentSpace> mySpaces = new List<ParentSpace>();

	private ParentSpaceCache parentCache = new ParentSpaceCache();

	private bool m_bActive;

	private const int kDeltaHistory = 100;

	private MovementDelta[] m_MovementHistory = new MovementDelta[100];

	private int m_CurrentMovementPosition;

	private Vector3 m_PositionLastFrame = Vector3.zero;

	private Vector3 m_PositionGlobalLastFrame = Vector3.zero;

	private Transform m_ParentLastFrame;

	private Transform m_Transform;

	private Rigidbody m_RigidBody;

	private MovementDelta m_PreviousMovementDelta;

	private Vector3 m_AdjustedAditionalVelocity = Vector3.zero;

	private CapsuleCollisionHelper m_CapsuleColliderHelper;

	private void Awake()
	{
		m_Transform = base.transform;
		m_RigidBody = GetComponent<Rigidbody>();
	}

	public void TakeSample(float _deltaTime, Transform _currentParent, float _serverPositionTime)
	{
		MovementDelta movementDelta = m_MovementHistory[m_CurrentMovementPosition++];
		movementDelta.NetworkTime = Time.time;
		float num = 0f;
		if (m_PreviousMovementDelta != null)
		{
			num = m_PreviousMovementDelta.NetworkTime;
		}
		else
		{
			num = movementDelta.NetworkTime;
		}
		movementDelta.FrameTime = _deltaTime;
		Vector3 vector = CalculateLocalPosition(m_Transform.position, _currentParent);
		Vector3 position = m_Transform.position;
		if (_currentParent != m_ParentLastFrame)
		{
			m_PositionLastFrame = CalculateLocalPosition(m_PositionGlobalLastFrame, _currentParent);
		}
		movementDelta.PositionDelta = vector - m_PositionLastFrame - m_AdjustedAditionalVelocity * movementDelta.FrameTime;
		movementDelta.Parent = _currentParent;
		m_AdjustedAditionalVelocity.Set(0f, 0f, 0f);
		m_ParentLastFrame = _currentParent;
		m_PositionLastFrame = vector;
		m_PositionGlobalLastFrame = position;
		m_PreviousMovementDelta = movementDelta;
		if (m_CurrentMovementPosition == 100)
		{
			m_CurrentMovementPosition = 0;
		}
		movementDelta.OriginalDelta = movementDelta.PositionDelta;
		MovementDelta segment = parentCache.GetSegment();
		if (segment == null)
		{
			CleanSpaces(_serverPositionTime);
			segment = parentCache.GetSegment();
		}
		if (segment == null)
		{
			CleanOldest();
			segment = parentCache.GetSegment();
		}
		if (segment != null)
		{
			segment.CopyData(movementDelta);
			AddToQueue(segment);
		}
	}

	private bool NeedToAddNewSpace(Transform aTransform)
	{
		if (mySpaces.Count == 0)
		{
			return true;
		}
		if (mySpaces[mySpaces.Count - 1].Parent == aTransform)
		{
			return false;
		}
		return true;
	}

	private bool DoPotentialSpaceTransition(MovementDelta delta)
	{
		if (NeedToAddNewSpace(delta.Parent))
		{
			if (mySpaces.Count != 0)
			{
				if (delta.Parent != null)
				{
					if (mySpaces[mySpaces.Count - 1].Parent != null)
					{
						mySpaces[mySpaces.Count - 1].exitTransform = delta.Parent.worldToLocalMatrix * mySpaces[mySpaces.Count - 1].Parent.worldToLocalMatrix.inverse;
					}
					else
					{
						mySpaces[mySpaces.Count - 1].exitTransform = delta.Parent.worldToLocalMatrix;
					}
				}
				else if (mySpaces[mySpaces.Count - 1].Parent != null)
				{
					mySpaces[mySpaces.Count - 1].exitTransform = mySpaces[mySpaces.Count - 1].Parent.worldToLocalMatrix.inverse;
				}
				else
				{
					mySpaces[mySpaces.Count - 1].exitTransform = Matrix4x4.identity;
				}
			}
			ParentSpace space = parentCache.GetSpace();
			space.Setup(delta.Parent);
			mySpaces.Add(space);
			space.Segments.Add(delta);
			return true;
		}
		return false;
	}

	private void AddToQueue(MovementDelta delta)
	{
		if (!DoPotentialSpaceTransition(delta))
		{
			ParentSpace parentSpace = mySpaces[mySpaces.Count - 1];
			MovementDelta movementDelta = parentSpace.Segments[parentSpace.Segments.Count - 1];
			mySpaces[mySpaces.Count - 1].Segments.Add(delta);
		}
	}

	public void Setup(Transform _currentParent)
	{
		m_bActive = true;
		m_PositionLastFrame = CalculateLocalPosition(m_Transform.position, _currentParent);
		m_PositionGlobalLastFrame = m_Transform.position;
		m_ParentLastFrame = _currentParent;
		m_CapsuleColliderHelper = base.gameObject.RequireComponent<CapsuleCollisionHelper>();
		for (int i = 0; i < 100; i++)
		{
			m_MovementHistory[i] = new MovementDelta();
		}
	}

	public void Clear(Transform _currentParent)
	{
		m_PositionLastFrame = CalculateLocalPosition(m_Transform.position, _currentParent);
		m_PositionGlobalLastFrame = m_Transform.position;
		m_ParentLastFrame = _currentParent;
		m_CurrentMovementPosition = 0;
		m_PreviousMovementDelta = null;
		m_AdjustedAditionalVelocity.Set(0f, 0f, 0f);
		for (int i = 0; i < 100; i++)
		{
			m_MovementHistory[i].Reset();
		}
	}

	public Vector3 GetLagCompensatedPositionDelta(float serverPositionTime, Vector3 position)
	{
		Vector3 vector = position;
		if (m_bActive)
		{
			bool option = DebugManager.Instance.GetOption("Chef VS Chef Prediction");
			m_CapsuleColliderHelper.UpdateCollisionMask(option);
			Vector3 position2 = m_RigidBody.position;
			Vector3 velocity = m_RigidBody.velocity;
			if (option)
			{
				m_RigidBody.position += Vector3.one * 100f;
				RemoteChefPositionRecorder.SetChefRestorePoint();
			}
			int num = m_CurrentMovementPosition;
			if (num < 0)
			{
				num = 100;
			}
			bool flag = true;
			for (int i = m_CurrentMovementPosition; i != num || flag; i++)
			{
				flag = false;
				if (i == 100)
				{
					i = 0;
					if (i == num)
					{
						break;
					}
				}
				MovementDelta movementDelta = m_MovementHistory[i];
				if (serverPositionTime <= movementDelta.NetworkTime)
				{
					float num2 = 1f;
					if (serverPositionTime > movementDelta.NetworkTime - movementDelta.FrameTime)
					{
						num2 = (movementDelta.NetworkTime - serverPositionTime) / movementDelta.FrameTime;
					}
					if (option)
					{
						RemoteChefPositionRecorder.SetRemoteChefsToTime(movementDelta.NetworkTime);
					}
					m_CapsuleColliderHelper.CastPositionForward(ref position, movementDelta.PositionDelta * num2, option);
				}
			}
			if (option)
			{
				m_RigidBody.position = position2;
				m_RigidBody.velocity = velocity;
				RemoteChefPositionRecorder.RestoreChefsPositions();
			}
		}
		return position - vector;
	}

	public Vector3 GetLagCompensatedPositionDeltaParents(float serverPositionTime, Vector3 position, ref Vector3 globalPosition)
	{
		Vector3 vector = position;
		if (m_bActive)
		{
			bool option = DebugManager.Instance.GetOption("Chef VS Chef Prediction");
			m_CapsuleColliderHelper.UpdateCollisionMask(option);
			Vector3 position2 = m_RigidBody.position;
			Vector3 velocity = m_RigidBody.velocity;
			if (option)
			{
				m_RigidBody.position += Vector3.one * 100f;
				RemoteChefPositionRecorder.SetChefRestorePoint();
			}
			CleanSpaces(serverPositionTime);
			Vector3 vector2 = position;
			Vector3 position3 = Vector3.zero;
			for (int i = 0; i < mySpaces.Count; i++)
			{
				ParentSpace parentSpace = mySpaces[i];
				Vector3 vector3 = ((i != 0) ? parentSpace.MeToWorld(position3) : vector2);
				position = vector3;
				for (int j = 0; j < parentSpace.Segments.Count; j++)
				{
					MovementDelta movementDelta = parentSpace.Segments[j];
					if (serverPositionTime <= movementDelta.NetworkTime)
					{
						float num = 1f;
						if (serverPositionTime > movementDelta.NetworkTime - movementDelta.FrameTime)
						{
							num = (movementDelta.NetworkTime - serverPositionTime) / movementDelta.FrameTime;
						}
						if (option)
						{
							RemoteChefPositionRecorder.SetRemoteChefsToTime(movementDelta.NetworkTime);
						}
						if (movementDelta.Parent == null)
						{
							m_CapsuleColliderHelper.CastPositionForward(ref position, movementDelta.PositionDelta * num, option);
						}
						else
						{
							m_CapsuleColliderHelper.CastPositionForward(ref position, movementDelta.Parent.localToWorldMatrix.rotation * movementDelta.PositionDelta * num, option);
						}
					}
				}
				Vector3 vector4 = parentSpace.FromWorldToMe(position);
				position3 = parentSpace.exitTransform * new Vector4(vector4.x, vector4.y, vector4.z, 1f);
			}
			if (option)
			{
				m_RigidBody.position = position2;
				m_RigidBody.velocity = velocity;
				RemoteChefPositionRecorder.RestoreChefsPositions();
			}
		}
		globalPosition = position;
		return position - vector;
	}

	private void CleanSpaces(float currentServerTime)
	{
		for (int num = mySpaces.Count - 1; num >= 0; num--)
		{
			ParentSpace parentSpace = mySpaces[num];
			for (int num2 = parentSpace.Segments.Count - 1; num2 >= 0; num2--)
			{
				MovementDelta movementDelta = parentSpace.Segments[num2];
				if (currentServerTime > movementDelta.NetworkTime)
				{
					parentCache.ReturnSegment(parentSpace.Segments[num2]);
					parentSpace.Segments.RemoveAt(num2);
				}
			}
			if (parentSpace.Segments.Count == 0)
			{
				parentCache.ReturnSpace(mySpaces[num]);
				mySpaces.RemoveAt(num);
			}
		}
	}

	private void CleanOldest()
	{
		if (mySpaces.Count > 0 && mySpaces[0].Segments.Count > 0)
		{
			parentCache.ReturnSegment(mySpaces[0].Segments[0]);
			mySpaces[0].Segments.RemoveAt(0);
			if (mySpaces[0].Segments.Count == 0)
			{
				parentCache.ReturnSpace(mySpaces[0]);
				mySpaces.RemoveAt(0);
			}
		}
	}

	public void AddAjustedVelocity(Vector3 additionalVelocity)
	{
		m_AdjustedAditionalVelocity += additionalVelocity;
	}

	public void Teleport(Vector3 newGlobalPosition, Transform currentParent)
	{
		Vector3 velocity = m_RigidBody.velocity;
		m_Transform.position = newGlobalPosition;
		m_RigidBody.velocity = velocity;
		m_PositionGlobalLastFrame = m_Transform.position;
		m_PositionLastFrame = CalculateLocalPosition(m_Transform.position, currentParent);
	}

	public Vector3 CalculateLocalPosition(Vector3 _globalPosition, Transform _currentParent)
	{
		if (_currentParent != null)
		{
			return _currentParent.worldToLocalMatrix * new Vector4(_globalPosition.x, _globalPosition.y, _globalPosition.z, 1f);
		}
		return _globalPosition;
	}
}
