using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/FoliageDeformer")]
public class FoliageDeformer : MonoBehaviour
{
	[SerializeField]
	private float m_radiusOfEffect = 2f;

	[SerializeField]
	private LayerMask m_layerToEffect;

	[SerializeField]
	private float m_wobbleTime = 0.5f;

	[SerializeField]
	private float m_wobbleAmount = 0.3f;

	private static Collider[] ms_colliders = new Collider[30];

	private void Update()
	{
		int num = Physics.OverlapSphereNonAlloc(base.transform.position, m_radiusOfEffect, ms_colliders, m_layerToEffect.value);
		for (int i = 0; i < num; i++)
		{
			Collider collider = ms_colliders[i];
			GameObject gameObject = collider.gameObject;
			DeformingFoliage deformingFoliage = gameObject.GetComponent<DeformingFoliage>();
			if (deformingFoliage == null)
			{
				deformingFoliage = gameObject.AddComponent<DeformingFoliage>();
				deformingFoliage.WobbleTime = m_wobbleTime;
				deformingFoliage.MinWobbleAmount = m_wobbleAmount;
				deformingFoliage.MaxWobbleAmount = m_wobbleAmount;
			}
			deformingFoliage.AddDeformer(base.transform);
		}
	}
}
