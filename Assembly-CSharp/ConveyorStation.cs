using UnityEngine;

[RequireComponent(typeof(TabletopConveyenceReceiver))]
[RequireComponent(typeof(StaticGridLocation))]
[RequireComponent(typeof(AttachStation))]
public class ConveyorStation : MonoBehaviour
{
	public enum XZDirection
	{
		Leftwards = 0,
		Rightwards = 1
	}

	[SerializeField]
	public float m_conveySpeed = 1f;

	[SerializeField]
	public XZDirection m_conveyanceDirectionXZ = XZDirection.Rightwards;

	public MeshRenderer m_targetRenderer;
}
