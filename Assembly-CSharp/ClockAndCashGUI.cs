using System;
using UnityEngine;

public class ClockAndCashGUI : MonoBehaviour
{
	[SerializeField]
	private int m_segments = 20;

	[SerializeField]
	private Material m_emptyClockSurface;

	[SerializeField]
	private Material m_fullClockSurface;

	[SerializeField]
	private Material m_borderClockSurface;

	[SerializeField]
	private Vector2 m_clockLocation = new Vector2(0.05f, 0.95f);

	[SerializeField]
	private float m_clockRadius = 0.01f;

	[SerializeField]
	private float m_backgroundRadius = 0.011f;

	private GameObject m_filledMeshObj;

	private GameObject m_emptiedMeshObj;

	private GameObject m_borderMeshObj;

	private Camera m_camera;

	private Mesh m_borderMesh;

	private Mesh m_emptiedMesh;

	private Mesh m_filledMesh;

	private float m_value;

	public void SetTimeProp(float _prop)
	{
		m_value = Mathf.Clamp01(_prop);
	}

	private void Awake()
	{
		m_camera = Camera.main;
		CreateObject(out m_filledMeshObj, out m_filledMesh, "FilledMesh", m_fullClockSurface);
		CreateObject(out m_emptiedMeshObj, out m_emptiedMesh, "EmptiedMesh", m_emptyClockSurface);
		CreateObject(out m_borderMeshObj, out m_borderMesh, "BorderMesh", m_borderClockSurface);
	}

	private void Update()
	{
		m_value %= 1f;
		Vector2 clockLocation = m_clockLocation;
		clockLocation.y = 1f - m_clockLocation.y;
		UpdateClockMesh(m_filledMesh, clockLocation, m_clockRadius, 0f, m_value);
		UpdateClockMesh(m_emptiedMesh, clockLocation, m_clockRadius, m_value, 1f);
		UpdateAnulusMesh(m_borderMesh, clockLocation, m_clockRadius, m_backgroundRadius);
	}

	private void UpdateAnulusMesh(Mesh _mesh, Vector2 _centreVS, float _startRadius, float _endRadius)
	{
		Vector3 vector = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS, 1f));
		Vector3 vector2 = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.right * _startRadius, 1f)) - vector;
		Vector3 vector3 = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.right * _endRadius, 1f)) - vector;
		Vector3 vector4 = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.up * _startRadius, 1f)) - vector;
		Vector3 vector5 = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.up * _endRadius, 1f)) - vector;
		vector4 = vector4.normalized * vector2.magnitude;
		vector5 = vector5.normalized * vector3.magnitude;
		Vector3[] array = new Vector3[2 * m_segments];
		Vector2[] uv = new Vector2[2 * m_segments];
		int[] array2 = new int[2 * m_segments * 3];
		for (int i = 0; i < m_segments; i++)
		{
			float f = MathUtils.ClampedRemap(i, 0f, m_segments, 0f, (float)Math.PI * 2f);
			array[2 * i] = vector + vector4 * Mathf.Cos(f) + vector2 * Mathf.Sin(f);
			array[2 * i + 1] = vector + vector5 * Mathf.Cos(f) + vector3 * Mathf.Sin(f);
		}
		for (int j = 0; j < 2 * m_segments; j++)
		{
			array2[3 * j] = j;
			array2[3 * j + 1] = (j + 1) % (2 * m_segments);
			array2[3 * j + 2] = (j + 2) % (2 * m_segments);
		}
		_mesh.Clear();
		_mesh.vertices = array;
		_mesh.uv = uv;
		_mesh.triangles = array2;
		_mesh.RecalculateNormals();
	}

	private void UpdateClockMesh(Mesh _mesh, Vector2 _centreVS, float _radius, float _propStart, float _propEnd)
	{
		Vector3 vector = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS, 1f));
		Vector3 vector2 = m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.right * _radius, 1f)) - vector;
		Vector3 vector3 = (m_camera.ViewportToWorldPoint(VectorUtils.FromXY(_centreVS + Vector2.up * _radius, 1f)) - vector).normalized * vector2.magnitude;
		int num = (int)Mathf.Max((float)m_segments * (_propEnd - _propStart), 1f);
		Vector3[] array = new Vector3[num + 2];
		Vector2[] array2 = new Vector2[num + 2];
		int[] array3 = new int[num * 3];
		float newA = (float)Math.PI * 2f * _propStart;
		float newB = (float)Math.PI * 2f * _propEnd;
		array[0] = vector;
		array2[0] = new Vector2(0.5f, 0f);
		for (int i = 1; i < num + 2; i++)
		{
			float f = MathUtils.ClampedRemap(i - 1, 0f, num, newA, newB);
			array[i] = vector + vector3 * Mathf.Cos(f) + vector2 * Mathf.Sin(f);
			float x = MathUtils.ClampedRemap(i - 1, (float)num * 0.25f, (float)num * 0.75f, 0f, 1f);
			float a = MathUtils.ClampedRemap(i - 1, 0f, (float)num * 0.25f, 0f, 1f);
			float b = MathUtils.ClampedRemap(i - 1, (float)num * 0.75f, num, 1f, 0f);
			array2[i] = new Vector2(x, Mathf.Min(a, b));
		}
		for (int j = 0; j < num; j++)
		{
			array3[3 * j] = 0;
			array3[3 * j + 1] = j + 1;
			array3[3 * j + 2] = j + 2;
		}
		_mesh.Clear();
		_mesh.vertices = array;
		_mesh.uv = array2;
		_mesh.triangles = array3;
		_mesh.RecalculateNormals();
	}

	private void CreateObject(out GameObject _obj, out Mesh _mesh, string _name, Material _material)
	{
		_obj = new GameObject(_name);
		_obj.transform.SetParent(null);
		_obj.transform.position = Vector3.zero;
		_obj.transform.rotation = Quaternion.identity;
		_obj.transform.localScale = Vector3.one;
		_obj.AddComponent(typeof(MeshFilter));
		_obj.AddComponent(typeof(MeshRenderer));
		_obj.GetComponent<Renderer>().material = _material;
		_mesh = new Mesh();
		_mesh.name = _name;
		_obj.GetComponent<MeshFilter>().mesh = _mesh;
	}
}
