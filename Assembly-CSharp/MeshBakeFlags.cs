using System;

[Flags]
public enum MeshBakeFlags
{
	None = 0,
	RecieveShadows = 1,
	CastShadowOn = 4,
	CastShadowOff = 8,
	CastShadowTwoSided = 0x10,
	CastShadowOnly = 0x20,
	GIContributeOff = 0x40,
	GIContributeOn = 0x80,
	GIContributeProbe = 0x100,
	RenderVisible = 0x200,
	ObjVisible = 0x400,
	lightProbeUsageOff = 0x800,
	reflProbeUsageOff = 0x1000,
	lightProbeUsageAnc = 0x2000,
	lightProbeUsageBlnd = 0x4000,
	reflProbeUsageBlnd = 0x8000,
	requiresNormals = 0x10000
}
