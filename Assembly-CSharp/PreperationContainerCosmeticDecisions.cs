using UnityEngine;

public class PreperationContainerCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	private LabelGUI m_labelGUI;

	private IOrderDefinition m_iOrderDefinition;

	private GUIRect m_startingRect;

	private string m_startingText;

	private void Awake()
	{
		if (m_labelGUI != null)
		{
			m_startingText = m_labelGUI.GetText();
			m_startingRect = m_labelGUI.GetGUIRect();
		}
		m_iOrderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_iOrderDefinition.RegisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
	}

	private void OnOrderCompositionChanged(AssembledDefinitionNode _contents)
	{
		CompositeAssembledNode compositeAssembledNode = _contents as CompositeAssembledNode;
		if (m_labelGUI != null)
		{
			string text = string.Empty;
			for (int i = 0; i < compositeAssembledNode.m_composition.Length; i++)
			{
				text = text + "\n+" + compositeAssembledNode.m_composition[i].ToString();
			}
			GUIRect gUIRect = m_startingRect.DeepCopy();
			gUIRect.m_rect.height = (float)(compositeAssembledNode.m_composition.Length + 1) * gUIRect.m_rect.height;
			m_labelGUI.SetGUIRect(gUIRect);
			m_labelGUI.SetText(m_startingText + ":" + text);
		}
	}
}
