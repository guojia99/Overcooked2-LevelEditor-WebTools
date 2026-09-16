using UnityEngine;

[RequireComponent(typeof(PilotMovement))]
public class MovingPlatformCosmeticDecisions : MonoBehaviour
{
	public Material ActiveLightstripMaterial;

	public Material InactiveLightstripMaterial;

	public MeshRenderer[] LightStripMeshes = new MeshRenderer[0];

	public string LightPFXName = "glow (2)";

	[HideInInspector]
	public ParticleSystem[] LightPFX;

	private void Awake()
	{
		ParticleSystem[] array = base.gameObject.RequestComponentsRecursive<ParticleSystem>();
		LightPFX = array.AllRemoved_Predicate((ParticleSystem x) => x.name != LightPFXName);
		OnPilotStatusChanged(false);
	}

	public void OnPilotStatusChanged(bool _hasPilot)
	{
		for (int i = 0; i < LightPFX.Length; i++)
		{
			LightPFX[i].gameObject.SetActive(_hasPilot);
		}
		Material material = ((!_hasPilot) ? InactiveLightstripMaterial : ActiveLightstripMaterial);
		for (int j = 0; j < LightStripMeshes.Length; j++)
		{
			LightStripMeshes[j].material = material;
		}
	}
}
