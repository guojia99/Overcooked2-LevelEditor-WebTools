using System;

public class CircleArc
{
	public float MinAngle;

	public float MaxAngle;

	public CircleArc(float _min, float _max)
	{
		MinAngle = _min;
		MaxAngle = _max;
	}

	private bool Contains(float x)
	{
		float minAngle = MinAngle;
		float num = MaxAngle;
		while (x < minAngle)
		{
			x += (float)Math.PI * 2f;
		}
		for (; num < minAngle; num += (float)Math.PI * 2f)
		{
		}
		return x >= minAngle && x <= num;
	}

	public static CircleArcSet operator -(CircleArc _a, CircleArc _b)
	{
		bool flag = _b.Contains(_a.MinAngle);
		bool flag2 = _b.Contains(_a.MaxAngle);
		if (flag && flag2)
		{
			return new CircleArcSet();
		}
		if (flag)
		{
			return new CircleArcSet(new CircleArc[1]
			{
				new CircleArc(_b.MaxAngle, _a.MaxAngle)
			});
		}
		if (flag2)
		{
			return new CircleArcSet(new CircleArc[1]
			{
				new CircleArc(_a.MinAngle, _b.MinAngle)
			});
		}
		if (_a.Contains(_b.MinAngle) && _a.Contains(_b.MaxAngle))
		{
			return new CircleArcSet(new CircleArc[2]
			{
				new CircleArc(_a.MinAngle, _b.MinAngle),
				new CircleArc(_b.MaxAngle, _a.MaxAngle)
			});
		}
		return new CircleArcSet(new CircleArc[1] { _a });
	}
}
