using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
[ExecuteInEditMode]
public class FixTextSize : MonoBehaviour
{
	private void Start()
	{
		AdjustSize();
	}

	private void AdjustSize()
	{
		Text text = base.gameObject.RequireComponent<Text>();
		text.fontSize = CalculateFontSize();
	}

	private int CalculateFontSize()
	{
		RectTransform rectTransform = base.transform as RectTransform;
		Canvas overlayCanvas = base.gameObject.RequestComponentUpwardsRecursive<Canvas>();
		return (int)GetRectInPixels(rectTransform, overlayCanvas).height;
	}

	private Rect GetRectInPixels(RectTransform _transform, Canvas _overlayCanvas)
	{
		Vector3[] canvasWorldCorners = new Vector3[4];
		_overlayCanvas.gameObject.RequireComponent<RectTransform>().GetWorldCorners(canvasWorldCorners);
		Vector3[] array = new Vector3[4];
		_transform.GetWorldCorners(array);
		Rect canvasPixelRect = _overlayCanvas.pixelRect;
		Converter<Vector3, Vector2> converter = delegate(Vector3 _worldPos)
		{
			Vector3 lhs = _worldPos - canvasWorldCorners[0];
			Vector3 rhs = canvasWorldCorners[2] - canvasWorldCorners[1];
			Vector3 rhs2 = canvasWorldCorners[1] - canvasWorldCorners[0];
			float num = Vector3.Dot(lhs, rhs) / rhs.sqrMagnitude;
			float num2 = Vector3.Dot(lhs, rhs2) / rhs2.sqrMagnitude;
			return new Vector2(canvasPixelRect.width * num, canvasPixelRect.height * num2);
		};
		Vector2[] array2 = array.ConvertAll(converter);
		float o_score;
		array2.FindLowestScoring((Vector2 x) => x.x, out o_score);
		float o_score2;
		array2.FindHighestScoring((Vector2 x) => x.x, out o_score2);
		float o_score3;
		array2.FindLowestScoring((Vector2 x) => x.y, out o_score3);
		float o_score4;
		array2.FindHighestScoring((Vector2 x) => x.y, out o_score4);
		return new Rect(o_score, o_score3, o_score2 - o_score, o_score4 - o_score3);
	}
}
