using UnityEngine;

[RequireComponent(typeof(HeldItemsMeshVisibility), typeof(HatMeshVisibility), typeof(ChefMeshReplacer))]
public class CloneTargetAppearance : MonoBehaviour
{
	[SerializeField]
	private GameObject m_target;

	private void Start()
	{
		CloneTarget();
	}

	private void CloneTarget()
	{
		if (m_target == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		int childCount = base.transform.childCount;
		for (int num = childCount - 1; num >= 0; num--)
		{
			Object.Destroy(base.transform.GetChild(num).gameObject);
		}
		GameSession.SelectedChefData chefData = m_target.GetComponent<ChefMeshReplacer>().GetChefData();
		base.gameObject.GetComponent<ChefMeshReplacer>().SetChefData(chefData);
		ClientHeldItemsMeshVisibility component = base.gameObject.GetComponent<ClientHeldItemsMeshVisibility>();
		component.ForceSetup();
		ClientHatMeshVisibility component2 = base.gameObject.GetComponent<ClientHatMeshVisibility>();
		component2.ForceSetup();
	}
}
