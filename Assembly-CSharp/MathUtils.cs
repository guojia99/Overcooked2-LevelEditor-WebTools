using System;
using UnityEngine;

public static class MathUtils
{
	public static void FloorModf(float _dividend, float _divisor, out float quotient, out float remainder)
	{
		quotient = Mathf.Floor(_dividend / _divisor);
		remainder = _dividend - quotient * _divisor;
	}

	public static void TruncModf(float _dividend, float _divisor, out float quotient, out float remainder)
	{
		quotient = Truncate(_dividend / _divisor);
		remainder = _dividend - quotient * _divisor;
	}

	public static void TruncModf(int _dividend, int _divisor, out int quotient, out int remainder)
	{
		quotient = _dividend / _divisor;
		remainder = _dividend - quotient * _divisor;
	}

	public static float Truncate(float _value)
	{
		return _value - _value % 1f;
	}

	public static float Quantize(float _input, float _gridSize)
	{
		return Mathf.Round(_input / _gridSize) * _gridSize;
	}

	public static float ClampedRemap(float _value, float _a, float _b, float _newA, float _newB)
	{
		return Mathf.Clamp(Remap(_value, _a, _b, _newA, _newB), Mathf.Min(_newA, _newB), Mathf.Max(_newA, _newB));
	}

	public static float ClampedRemap01(float value, float min, float max)
	{
		return Mathf.Clamp01((value - min) / (max - min));
	}

	public static float Wrap(float _value, float _intervalMin, float _intervalMax)
	{
		float num = _intervalMax - _intervalMin;
		float num2 = (_value - _intervalMin) % num;
		num2 = (num2 + num) % num;
		return num2 + _intervalMin;
	}

	public static float AngleWrap(float _x)
	{
		float quotient;
		float remainder;
		FloorModf(_x + (float)Math.PI, (float)Math.PI * 2f, out quotient, out remainder);
		return remainder - (float)Math.PI;
	}

	public static float AngleDifference(float _a, float _b)
	{
		return AngleWrap(_a - _b);
	}

	public static int Wrap(int _value, int _intervalMin, int _intervalMax)
	{
		int num = _intervalMax - _intervalMin;
		int num2 = (_value - _intervalMin) % num;
		num2 = (num2 + num) % num;
		return num2 + _intervalMin;
	}

	public static float Remap(float _value, float _a, float _b, float _newA, float _newB)
	{
		float num = (_value - _a) / (_b - _a);
		return _newA + num * (_newB - _newA);
	}

	public static float SinusoidalSCurve(float _prop)
	{
		float num = Mathf.Clamp(_prop, 0f, 1f);
		return 0.5f * (1f - Mathf.Cos((float)Math.PI * num));
	}

	public static float Square(float _value)
	{
		return _value * _value;
	}

	public static void AdvanceToTarget_Sinusoidal(ref float _nCurrentX, ref float _nCurrentGradient, float _nTargetX, float _nGradientLimit, float _nTimeToMax, float _nDeltaTime)
	{
		float num = _nCurrentX;
		float value = _nCurrentGradient;
		float num2 = (float)Math.PI / (2f * _nTimeToMax);
		float num3 = _nGradientLimit / num2;
		value = Mathf.Clamp(value, 0f - _nGradientLimit, _nGradientLimit);
		float? num4 = null;
		if (Mathf.Abs(_nTargetX - num) <= num3)
		{
			float num5 = ((_nTargetX - num > 0f) ? 1 : (-1));
			float num6 = num5 * Mathf.Acos(1f - num2 * Mathf.Abs(_nTargetX - num) / _nGradientLimit) / num2;
			num6 = ((!(num5 > 0f)) ? Mathf.Min(num6 + _nDeltaTime, 0f) : Mathf.Max(num6 - _nDeltaTime, 0f));
			num4 = _nGradientLimit * Mathf.Sin(num2 * num6);
			if (num5 > 0f && num4.HasValue && num4.GetValueOrDefault() > value)
			{
				num4 = null;
			}
			else if (num5 < 0f && num4.HasValue && num4.GetValueOrDefault() < value)
			{
				num4 = null;
			}
		}
		if (!num4.HasValue)
		{
			float num7 = 1f / num2 * ((float)Math.PI / 2f - Mathf.Acos(value / _nGradientLimit));
			num7 = ((!(_nTargetX > num)) ? Mathf.Max(num7 - _nDeltaTime, -(float)Math.PI / (2f * num2)) : Mathf.Min(num7 + _nDeltaTime, (float)Math.PI / (2f * num2)));
			num4 = _nGradientLimit * Mathf.Cos((float)Math.PI / 2f - num2 * num7);
		}
		value = num4.Value;
		value = ((!(_nTargetX > num)) ? Mathf.Clamp(value, (_nTargetX - num) / _nDeltaTime, _nGradientLimit) : Mathf.Clamp(value, 0f - _nGradientLimit, (_nTargetX - num) / _nDeltaTime));
		num += value * _nDeltaTime;
		_nCurrentX = num;
		_nCurrentGradient = value;
	}

	public static string AsHexString(uint _value)
	{
		return "0x" + _value.ToString("x");
	}

	public static Vector3 MultiplyByMatrix(Matrix4x4 matrix, Vector3 vec)
	{
		return matrix * new Vector4(vec.x, vec.y, vec.z, 1f);
	}

	public static Vector3 CircleNearestPoint(Vector3 centre, float radius, Vector3 point)
	{
		return (point - centre).normalized * radius + centre;
	}

	public static void CircleCircleOuterTangents(out Vector3 t0, out Vector3 t1, out Vector3 t2, out Vector3 t3, Vector3 centre0, float radius0, Vector3 centre1, float radius1)
	{
		if (radius0 == radius1)
		{
			Vector3 vector = centre1 - centre0;
			Vector3 normalized = new Vector3(0f - vector.z, 0f, vector.x).normalized;
			Vector3 vector2 = normalized * radius0;
			Vector3 vector3 = normalized * (0f - radius0);
			t0 = vector2 + centre0;
			t0.y = centre0.y;
			t1 = vector3 + centre0;
			t1.y = centre0.y;
			t2 = vector2 + centre1;
			t2.y = centre1.y;
			t3 = vector3 + centre1;
			t3.y = centre1.y;
		}
		else
		{
			float num = Vector3.Distance(centre0, centre1);
			Vector3 vector4 = new Vector3((centre1.x * radius0 - centre0.x * radius1) / (radius0 - radius1), centre0.y, (centre1.z * radius0 - centre0.z * radius1) / (radius0 - radius1));
			Vector3 vector5 = vector4 - centre0;
			Vector3 vector6 = vector4 - centre1;
			float num2 = radius0 * radius0;
			float num3 = radius1 * radius1;
			float num4 = Mathf.Sqrt(vector5.x * vector5.x + vector5.z * vector5.z - num2);
			float num5 = Mathf.Sqrt(vector6.x * vector6.x + vector6.z * vector6.z - num3);
			float num6 = vector5.x * vector5.x + vector5.z * vector5.z;
			float num7 = vector6.x * vector6.x + vector6.z * vector6.z;
			t0 = new Vector3((num2 * vector5.x + radius0 * vector5.z * num4) / num6 + centre0.x, centre0.y, (num2 * vector5.z - radius0 * vector5.x * num4) / num6 + centre0.z);
			t1 = new Vector3((num2 * vector5.x - radius0 * vector5.z * num4) / num6 + centre0.x, centre0.y, (num2 * vector5.z + radius0 * vector5.x * num4) / num6 + centre0.z);
			t2 = new Vector3((num3 * vector6.x + radius1 * vector6.z * num5) / num7 + centre1.x, centre1.y, (num3 * vector6.z - radius1 * vector6.x * num5) / num7 + centre1.z);
			t3 = new Vector3((num3 * vector6.x - radius1 * vector6.z * num5) / num7 + centre1.x, centre1.y, (num3 * vector6.z + radius1 * vector6.x * num5) / num7 + centre1.z);
		}
	}
}
