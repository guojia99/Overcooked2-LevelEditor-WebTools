using UnityEngine;

public struct TransformHelper
{
	private Quaternion m_rotation;

	private Vector3 m_position;

	public static TransformHelper Indentity = new TransformHelper(Quaternion.identity, Vector3.zero);

	public Quaternion Rotation
	{
		get
		{
			return m_rotation;
		}
		set
		{
			m_rotation = value;
		}
	}

	public Vector3 Position
	{
		get
		{
			return m_position;
		}
		set
		{
			m_position = value;
		}
	}

	public TransformHelper(Transform t)
		: this(t.rotation, t.position)
	{
	}

	public TransformHelper(Quaternion rot, Vector3 pos)
	{
		m_rotation = rot;
		m_position = pos;
	}

	public Vector3 ToWorldDir(Vector3 _dir)
	{
		return m_rotation * _dir;
	}

	public Vector3 F()
	{
		return ToWorldDir(new Vector3(0f, 0f, 1f));
	}

	public Vector3 U()
	{
		return ToWorldDir(new Vector3(0f, 1f, 0f));
	}

	public Vector3 R()
	{
		return ToWorldDir(new Vector3(1f, 0f, 0f));
	}

	public Vector3 ToLocalDir(Vector3 _dir)
	{
		return Inverted(m_rotation) * _dir;
	}

	public Vector3 ToWorldPos(Vector3 _pos)
	{
		return ToWorldDir(_pos) + m_position;
	}

	public Vector3 ToLocalPos(Vector3 _pos)
	{
		return ToLocalDir(_pos - m_position);
	}

	public TransformHelper ToWorld(TransformHelper _trans)
	{
		return new TransformHelper(m_rotation * _trans.Rotation, m_position + m_rotation * _trans.Position);
	}

	public TransformHelper ToLocal(TransformHelper _trans)
	{
		return new TransformHelper(Inverted(m_rotation) * _trans.Rotation, Inverted(m_rotation) * (_trans.Position - m_position));
	}

	public static Quaternion Inverted(Quaternion q)
	{
		return new Quaternion(0f - q.x, 0f - q.y, 0f - q.z, q.w);
	}
}
