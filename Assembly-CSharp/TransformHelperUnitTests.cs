using UnityEngine;

[AddComponentMenu("Scripts/Core/UnitTests/TransformHelperUnitTests")]
public class TransformHelperUnitTests : MonoBehaviour
{
	[SerializeField]
	private float m_testEpsilon = 0.01f;

	private void Start()
	{
		DoTests();
	}

	private void DoTests()
	{
		Quaternion quaternion = Quaternion.AngleAxis(60f, Vector3.up);
		Quaternion quaternion2 = TransformHelper.Inverted(quaternion);
		Quaternion quaternion3 = quaternion * quaternion2;
		Quaternion quaternion4 = quaternion2 * quaternion;
		Quaternion quaternion5 = Quaternion.AngleAxis(60f, Vector3.right);
		Quaternion quaternion6 = TransformHelper.Inverted(quaternion5);
		Quaternion quaternion7 = quaternion5 * quaternion6;
		Quaternion quaternion8 = quaternion6 * quaternion5;
		Quaternion quaternion9 = Quaternion.AngleAxis(60f, Vector3.forward);
		Quaternion quaternion10 = TransformHelper.Inverted(quaternion9);
		Quaternion quaternion11 = quaternion9 * quaternion10;
		Quaternion quaternion12 = quaternion10 * quaternion9;
		TransformHelper transformHelper = new TransformHelper(Quaternion.AngleAxis(45f, Vector3.up), Vector3.zero);
		TransformHelper trans = new TransformHelper(Quaternion.AngleAxis(135f, Vector3.up), Vector3.zero);
		float angle;
		Vector3 axis;
		transformHelper.ToLocal(trans).Rotation.ToAngleAxis(out angle, out axis);
		TransformHelper transformHelper2 = new TransformHelper(Quaternion.AngleAxis(45f, Vector3.right), Vector3.zero);
		TransformHelper trans2 = new TransformHelper(Quaternion.AngleAxis(135f, Vector3.right), Vector3.zero);
		float angle2;
		Vector3 axis2;
		transformHelper2.ToLocal(trans2).Rotation.ToAngleAxis(out angle2, out axis2);
		TransformHelper transformHelper3 = new TransformHelper(Quaternion.AngleAxis(45f, Vector3.forward), Vector3.zero);
		TransformHelper trans3 = new TransformHelper(Quaternion.AngleAxis(135f, Vector3.forward), Vector3.zero);
		float angle3;
		Vector3 axis3;
		transformHelper3.ToLocal(trans3).Rotation.ToAngleAxis(out angle3, out axis3);
		TransformHelper transformHelper4 = new TransformHelper(Quaternion.AngleAxis(45f, Vector3.forward), new Vector3(1f, 1f, 0f));
		TransformHelper trans4 = new TransformHelper(Quaternion.AngleAxis(-45f, Vector3.forward), new Vector3(2f, -2f, 0f));
		TransformHelper transformHelper5 = transformHelper4.ToLocal(trans4);
		float sqrMagnitude = (transformHelper5.R() - new Vector3(0f, -1f, 0f)).sqrMagnitude;
		float sqrMagnitude2 = (transformHelper5.U() - new Vector3(1f, 0f, 0f)).sqrMagnitude;
		float sqrMagnitude3 = (transformHelper5.Position - new Vector3(0f - Mathf.Sqrt(2f), 0f - Mathf.Sqrt(8f), 0f)).sqrMagnitude;
		TransformHelper trans5 = new TransformHelper(Quaternion.AngleAxis(135f, new Vector3(0.707f, 0f, 0.707f)), Vector3.zero);
		TransformHelper trans6 = new TransformHelper(Quaternion.AngleAxis(45f, Vector3.forward), Vector3.zero);
		TransformHelper transformHelper6 = trans6.ToWorld(trans6.ToLocal(trans5));
		TransformHelper transformHelper7 = trans5.ToWorld(trans5.ToLocal(trans6));
		float sqrMagnitude4 = (transformHelper6.F() - trans5.F()).sqrMagnitude;
		float sqrMagnitude5 = (transformHelper7.F() - trans6.F()).sqrMagnitude;
	}
}
