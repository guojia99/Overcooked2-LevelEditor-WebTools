using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ButtonImage : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.Editable)]
	private Image m_image;

	[SerializeField]
	private ControlPadInput.Button m_buton = ControlPadInput.Button.A;

	[SerializeField]
	private AmbiPadButton m_ambiButton;

	[SerializeField]
	private ControllerIconLookup.IconContext m_context;

	[SerializeField]
	private ControllerIconLookup.DeviceContext m_device = ControllerIconLookup.DeviceContext.Pad;

	private bool m_awake;

	public void SetData(ControlPadInput.Button _button, ControllerIconLookup.DeviceContext _deviceContext)
	{
		m_buton = _button;
		m_device = _deviceContext;
		ControllerIconLookup controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		m_image.sprite = controllerIconLookup.GetIcon(m_buton, m_context, m_device);
		m_awake = true;
	}

	private void Awake()
	{
		if (!m_awake)
		{
			SetData(m_buton, m_device);
			m_awake = true;
		}
	}
}
