using UnityEngine;

public class CapsuleCollisionHelper : MonoBehaviour
{
	private CapsuleCollider m_CapsuleCollider;

	private RaycastHit m_RaycastHit = default(RaycastHit);

	private float m_fCapsuleRadius = 1f;

	private float m_fCapsuleDiameter = 2f;

	private Vector3 m_CapsuleTopOffset = Vector3.zero;

	private Vector3 m_CapsuleBottomOffset = Vector3.zero;

	private int m_RayCollisionMask;

	private int m_CapsuleCheckCollisionMask;

	private int m_CapsuleKillPlaneMask;

	private bool m_bShowSweepTests;

	private Material m_DebugMaterial;

	private Mesh m_DebugMesh;

	private int m_DebugSortLayer;

	private Vector3 m_DebugMeshCentre = Vector3.zero;

	private void Awake()
	{
		m_CapsuleCollider = base.gameObject.RequireComponent<CapsuleCollider>();
		m_fCapsuleRadius = m_CapsuleCollider.radius * 0.99f;
		m_fCapsuleDiameter = m_fCapsuleRadius * 2f;
		float num = m_CapsuleCollider.height * 0.5f - m_fCapsuleRadius;
		float num2 = m_CapsuleCollider.center.y - base.transform.position.y;
		m_CapsuleTopOffset.Set(0f, num2 + num, 0f);
		m_CapsuleBottomOffset.Set(0f, num2 - num, 0f);
		m_RayCollisionMask = -1 ^ (1 << LayerMask.NameToLayer("KillPlane")) ^ (1 << LayerMask.NameToLayer("Ground")) ^ (1 << LayerMask.NameToLayer("SlopedGround")) ^ (1 << LayerMask.NameToLayer("Ignore Raycast")) ^ (1 << LayerMask.NameToLayer("PlayerTriggerZone")) ^ (1 << LayerMask.NameToLayer("Attachments")) ^ (1 << LayerMask.NameToLayer("HeldAttachments")) ^ (1 << LayerMask.NameToLayer("PushedObjectBounds"));
		m_CapsuleCheckCollisionMask = -1 ^ (1 << LayerMask.NameToLayer("KillPlane")) ^ (1 << LayerMask.NameToLayer("Ground")) ^ (1 << LayerMask.NameToLayer("SlopedGround")) ^ (1 << LayerMask.NameToLayer("Ignore Raycast")) ^ (1 << LayerMask.NameToLayer("PlayerTriggerZone")) ^ (1 << LayerMask.NameToLayer("Players")) ^ (1 << LayerMask.NameToLayer("Attachments")) ^ (1 << LayerMask.NameToLayer("HeldAttachments")) ^ (1 << LayerMask.NameToLayer("PushedObjectBounds"));
		m_CapsuleKillPlaneMask = 1 << LayerMask.NameToLayer("KillPlane");
		Transform transform = base.transform.Find("Capsule");
		if (transform != null)
		{
			m_bShowSweepTests = true;
			MeshFilter component = transform.GetComponent<MeshFilter>();
			m_DebugMesh = component.mesh;
			Renderer component2 = transform.GetComponent<Renderer>();
			m_DebugMaterial = component2.material;
			Color color = m_DebugMaterial.color;
			color.a = 0.5f;
			m_DebugMaterial.color = color;
			m_DebugSortLayer = component2.sortingLayerID;
		}
	}

	public void RenderCapsule(Vector3 position)
	{
		Vector3 vector = new Vector3(m_fCapsuleDiameter, m_fCapsuleDiameter, m_fCapsuleDiameter);
		Matrix4x4 matrix = Matrix4x4.Translate(position) * Matrix4x4.Scale(vector);
		Graphics.DrawMesh(m_DebugMesh, matrix, m_DebugMaterial, m_DebugSortLayer);
	}

	public void UpdateCollisionMask(bool bChefVsChef)
	{
		int num = 1 << LayerMask.NameToLayer("Players");
		if (bChefVsChef)
		{
			m_RayCollisionMask |= num;
		}
		else
		{
			m_RayCollisionMask &= ~num;
		}
	}

	public bool CheckCapsule(Vector3 startPosition)
	{
		return Physics.CheckCapsule(m_CapsuleTopOffset + startPosition, m_CapsuleBottomOffset + startPosition, m_fCapsuleRadius, m_CapsuleCheckCollisionMask, QueryTriggerInteraction.Ignore);
	}

	public bool CheckCapsuleKillPlane(Vector3 startPosition)
	{
		return Physics.CheckCapsule(m_CapsuleTopOffset + startPosition, m_CapsuleBottomOffset + startPosition, m_fCapsuleRadius, m_CapsuleKillPlaneMask, QueryTriggerInteraction.Collide);
	}

	public void CastPositionForward(ref Vector3 startPosition, Vector3 direction, bool bChefVsChef)
	{
		float magnitude = direction.magnitude;
		Vector3 normalized = direction.normalized;
		float b = 0.015f;
		if ((double)magnitude <= 0.001)
		{
			return;
		}
		if (Physics.CapsuleCast(m_CapsuleTopOffset + startPosition, m_CapsuleBottomOffset + startPosition, m_fCapsuleRadius, normalized, out m_RaycastHit, magnitude, m_RayCollisionMask, QueryTriggerInteraction.Ignore))
		{
			Vector3 vector = startPosition;
			float num = magnitude;
			int num2 = 0;
			int num3 = 30;
			do
			{
				vector += normalized * (m_RaycastHit.distance - 0.01f);
				num -= m_RaycastHit.distance - 0.01f;
				Vector3 vector2 = new Vector3(0f - m_RaycastHit.normal.z, 0f, m_RaycastHit.normal.x);
				Vector3 normalized2 = vector2.normalized;
				float num4 = Vector3.Dot(normalized, normalized2);
				if (num4 == 0f)
				{
					return;
				}
				float num5 = Mathf.Min(num * Mathf.Abs(num4), b);
				Vector3 vector3 = num5 * Mathf.Sign(num4) * normalized2;
				vector += vector3;
				num = Mathf.Max(0f, num - num5 / Mathf.Max(Mathf.Abs(num4), 1E-06f));
				num2++;
			}
			while (num2 < num3 && num > 0f && Physics.CapsuleCast(m_CapsuleTopOffset + vector, m_CapsuleBottomOffset + vector, m_fCapsuleRadius, normalized, out m_RaycastHit, num, m_RayCollisionMask, QueryTriggerInteraction.Ignore));
			Vector3 vector4 = vector + normalized * Mathf.Min(0f, num);
			vector4.y = startPosition.y;
			startPosition = vector4;
		}
		else if (magnitude > 0f)
		{
			startPosition += normalized * magnitude;
		}
	}

	public bool CheckPathToPoint(Vector3 start, Vector3 end)
	{
		Vector3 vector = end - start;
		float magnitude = vector.magnitude;
		vector.Normalize();
		if (Physics.CapsuleCast(start + m_CapsuleTopOffset, start + m_CapsuleBottomOffset, m_fCapsuleRadius, vector, out m_RaycastHit, magnitude, m_RayCollisionMask, QueryTriggerInteraction.Ignore))
		{
			float f = Vector3.Dot(vector, m_RaycastHit.normal);
			f = Mathf.Acos(f) * 57.29578f;
			if (f > 95f)
			{
				Vector3 vector2 = vector * -1f;
				if (Physics.CapsuleCast(end + m_CapsuleTopOffset, end + m_CapsuleBottomOffset, m_fCapsuleRadius, vector2, out m_RaycastHit, magnitude, m_RayCollisionMask, QueryTriggerInteraction.Ignore))
				{
					float f2 = Vector3.Dot(vector2, m_RaycastHit.normal);
					f2 = Mathf.Acos(f2) * 57.29578f;
					if (f2 > 95f)
					{
						return true;
					}
				}
			}
		}
		return false;
	}
}
