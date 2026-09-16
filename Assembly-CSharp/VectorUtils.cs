using UnityEngine;

public static class VectorUtils
{
	public static Vector2 Splat2(float _input)
	{
		return new Vector2(_input, _input);
	}

	public static Vector3 Splat3(float _input)
	{
		return new Vector3(_input, _input, _input);
	}

	public static Vector2 XZ(this Vector3 _input)
	{
		return new Vector2(_input.x, _input.z);
	}

	public static Vector2 XY(this Vector3 _input)
	{
		return new Vector2(_input.x, _input.y);
	}

	public static Vector2 YZ(this Vector3 _input)
	{
		return new Vector2(_input.y, _input.z);
	}

	public static Vector3 FromXZ(Vector2 _xz, float _y)
	{
		return new Vector3(_xz.x, _y, _xz.y);
	}

	public static Vector3 FromXY(Vector2 _xy, float _z)
	{
		return new Vector3(_xy.x, _xy.y, _z);
	}

	public static Vector3 FromYZ(Vector2 _yz, float _x)
	{
		return new Vector3(_x, _yz.x, _yz.y);
	}

	public static Vector3 WithX(this Vector3 _input, float _x)
	{
		return new Vector3(_x, _input.y, _input.z);
	}

	public static Vector3 WithY(this Vector3 _input, float _y)
	{
		return new Vector3(_input.x, _y, _input.z);
	}

	public static Vector3 WithZ(this Vector3 _input, float _z)
	{
		return new Vector3(_input.x, _input.y, _z);
	}

	public static Vector2 WithX(this Vector2 _input, float _x)
	{
		return new Vector2(_x, _input.y);
	}

	public static Vector2 WithY(this Vector2 _input, float _y)
	{
		return new Vector2(_input.x, _y);
	}

	public static Vector3 AddX(this Vector3 _input, float _x)
	{
		return new Vector3(_x + _input.x, _input.y, _input.z);
	}

	public static Vector3 AddY(this Vector3 _input, float _y)
	{
		return new Vector3(_input.x, _input.y + _y, _input.z);
	}

	public static Vector3 AddZ(this Vector3 _input, float _z)
	{
		return new Vector3(_input.x, _input.y, _input.z + _z);
	}

	public static Vector3 XZY(this Vector3 _input)
	{
		return new Vector3(_input.x, _input.z, _input.y);
	}

	public static Vector3 SafeNormalised(this Vector3 _vector, Vector3 _default)
	{
		if (_vector.sqrMagnitude < 0.0001f)
		{
			return _default;
		}
		return _vector.normalized;
	}

	public static Vector3 MultipliedBy(this Vector3 _a, Vector3 _b)
	{
		return new Vector3(_a.x * _b.x, _a.y * _b.y, _a.z * _b.z);
	}

	public static Vector2 MultipliedBy(this Vector2 _a, Vector2 _b)
	{
		return new Vector2(_a.x * _b.x, _a.y * _b.y);
	}

	public static Vector2 MultipliedBy(this Vector2 _a, float x, float y)
	{
		return new Vector2(_a.x * x, _a.y * y);
	}

	public static Vector3 DividedBy(this Vector3 _a, Vector3 _b)
	{
		return new Vector3(_a.x / _b.x, _a.y / _b.y, _a.z / _b.z);
	}

	public static Vector2 DividedBy(this Vector2 _a, Vector2 _b)
	{
		return new Vector2(_a.x / _b.x, _a.y / _b.y);
	}

	public static Vector3 Select(Vector3 _a, Vector3 _b, bool _x, bool _y, bool _z)
	{
		return new Vector3((!_x) ? _b.x : _a.x, (!_y) ? _b.y : _a.y, (!_z) ? _b.z : _a.z);
	}

	public static Vector2 Select(Vector2 _a, Vector2 _b, bool _x, bool _y)
	{
		return new Vector2((!_x) ? _b.x : _a.x, (!_y) ? _b.y : _a.y);
	}

	public static Vector2 Clamp(this Vector2 _input, Vector2 _min, Vector2 _max)
	{
		return Max(Min(_input, _max), _min);
	}

	public static Vector3 Clamp(this Vector3 _input, Vector3 _min, Vector3 _max)
	{
		return Max(Min(_input, _max), _min);
	}

	public static Vector2 Min(Vector2 _a, Vector2 _b)
	{
		return new Vector2(Mathf.Min(_a.x, _b.x), Mathf.Min(_a.y, _b.y));
	}

	public static Vector2 Min(Vector2[] _a)
	{
		return new Vector2(Mathf.Min(_a.ConvertAll((Vector2 x) => x.x)), Mathf.Min(_a.ConvertAll((Vector2 x) => x.y)));
	}

	public static Vector3 Min(Vector3 _a, Vector3 _b)
	{
		return new Vector3(Mathf.Min(_a.x, _b.x), Mathf.Min(_a.y, _b.y), Mathf.Min(_a.z, _b.z));
	}

	public static Vector3 Min(Vector3[] _a)
	{
		return new Vector3(Mathf.Min(_a.ConvertAll((Vector3 x) => x.x)), Mathf.Min(_a.ConvertAll((Vector3 x) => x.y)), Mathf.Min(_a.ConvertAll((Vector3 x) => x.z)));
	}

	public static Vector2 Max(Vector2 _a, Vector2 _b)
	{
		return new Vector2(Mathf.Max(_a.x, _b.x), Mathf.Max(_a.y, _b.y));
	}

	public static Vector2 Max(Vector2[] _a)
	{
		return new Vector2(Mathf.Max(_a.ConvertAll((Vector2 x) => x.x)), Mathf.Max(_a.ConvertAll((Vector2 x) => x.y)));
	}

	public static Vector3 Max(Vector3 _a, Vector3 _b)
	{
		return new Vector3(Mathf.Max(_a.x, _b.x), Mathf.Max(_a.y, _b.y), Mathf.Max(_a.z, _b.z));
	}

	public static Vector3 Max(Vector3[] _a)
	{
		return new Vector3(Mathf.Max(_a.ConvertAll((Vector3 x) => x.x)), Mathf.Max(_a.ConvertAll((Vector3 x) => x.y)), Mathf.Max(_a.ConvertAll((Vector3 x) => x.z)));
	}

	public static float Hmin(Vector3 _a)
	{
		return Mathf.Min(Mathf.Min(_a.x, _a.y), _a.z);
	}

	public static float Hmax(Vector3 _a)
	{
		return Mathf.Max(Mathf.Max(_a.x, _a.y), _a.z);
	}

	public static Vector3 Rcp(Vector3 _a)
	{
		return new Vector3(1f / _a.x, 1f / _a.y, 1f / _a.z);
	}

	public static Vector3 Abs(Vector3 _a)
	{
		return new Vector3(Mathf.Abs(_a.x), Mathf.Abs(_a.y), Mathf.Abs(_a.z));
	}

	public static float DistanceSq(Vector3 _a, Vector3 _b)
	{
		return (_a - _b).sqrMagnitude;
	}

	public static float ProgressUnclamped(Vector3 _a, Vector3 _b, Vector3 _pos)
	{
		Vector3 onNormal = _b - _a;
		Vector3 vector = _pos - _a;
		Vector3 vector2 = Vector3.Project(vector, onNormal);
		float num = vector2.magnitude / onNormal.magnitude;
		float num2 = Vector3.Dot(onNormal.normalized, vector2.normalized);
		return num * num2;
	}

	public static float Progress(Vector3 _a, Vector3 _b, Vector3 _pos)
	{
		return Mathf.Clamp01(ProgressUnclamped(_a, _b, _pos));
	}
}
