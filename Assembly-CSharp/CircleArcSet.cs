public class CircleArcSet
{
	public CircleArc[] Arcs = new CircleArc[0];

	public CircleArcSet()
	{
	}

	public CircleArcSet(CircleArc[] _arcs)
	{
		Arcs = _arcs;
	}

	public static CircleArcSet operator -(CircleArcSet _a, CircleArc _b)
	{
		CircleArcSet circleArcSet = new CircleArcSet();
		for (int i = 0; i < _a.Arcs.Length; i++)
		{
			circleArcSet.Arcs = circleArcSet.Arcs.Union((_a.Arcs[i] - _b).Arcs);
		}
		return circleArcSet;
	}

	public static CircleArcSet operator -(CircleArcSet _a, CircleArcSet _b)
	{
		for (int i = 0; i < _b.Arcs.Length; i++)
		{
			_a -= _b.Arcs[i];
		}
		return _a;
	}
}
