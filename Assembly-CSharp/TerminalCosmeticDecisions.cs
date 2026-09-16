using UnityEngine;

[RequireComponent(typeof(Terminal))]
public class TerminalCosmeticDecisions : MonoBehaviour
{
	public HoverIconUIController MoveIconPrefab;

	public Vector3 Iconoffset;

	public Material ActiveMaterial;

	public Material DisabledMaterial;

	public Material InUseMaterial;

	public Renderer[] ColouredMeshes = new Renderer[0];

	public Transform ShaftTransform;

	public float JoystickMaxAngle = 45f;

	public GameLoopingAudioTag Moving = GameLoopingAudioTag.MovingPlatform;

	public GameOneShotAudioTag StartMoving = GameOneShotAudioTag.MovingPlatformStart;

	public GameOneShotAudioTag StopMoving = GameOneShotAudioTag.MovingPlatformStop;

	[HideInInspector]
	public bool IsPlaying;
}
