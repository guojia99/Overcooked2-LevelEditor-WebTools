using UnityEngine;
using UnityEngine.Rendering;

public class CarryItemShadowDisable : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private ServerPlayerAttachmentCarrier m_attachmentCarrier;

	private void Awake()
	{
		m_attachmentCarrier.RegisterCarriedItemChangeCallback(OnItemChanged);
	}

	private void OnItemChanged(GameObject _before, GameObject _after)
	{
		if (_before != null)
		{
			SetShadowCastingState(_before, ShadowCastingMode.On);
		}
		if (_after != null)
		{
			SetShadowCastingState(_after, ShadowCastingMode.Off);
		}
	}

	private void SetShadowCastingState(GameObject _obj, ShadowCastingMode _shadowCasting)
	{
		Renderer[] array = _obj.RequestComponentsRecursive<Renderer>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].shadowCastingMode = _shadowCasting;
		}
	}
}
