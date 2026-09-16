namespace UnityEngine.UI
{
	[AddComponentMenu("AnchorGridLayoutGroup")]
	public class AnchorGridLayoutGroup : LayoutGroup
	{
		public enum Corner
		{
			UpperLeft = 0,
			UpperRight = 1,
			LowerLeft = 2,
			LowerRight = 3
		}

		public enum Axis
		{
			Horizontal = 0,
			Vertical = 1
		}

		public enum Constraint
		{
			Flexible = 0,
			FixedColumnCount = 1,
			FixedRowCount = 2
		}

		[SerializeField]
		protected TextAnchor m_anchorPoint;

		[SerializeField]
		protected Corner m_StartCorner;

		[SerializeField]
		protected Axis m_StartAxis;

		[SerializeField]
		protected Vector2 m_CellSize = new Vector2(1f, 1f);

		[SerializeField]
		protected Vector2 m_Spacing = Vector2.zero;

		[SerializeField]
		protected Constraint m_Constraint;

		[SerializeField]
		protected int m_ConstraintCount = 2;

		public Corner startCorner
		{
			get
			{
				return m_StartCorner;
			}
			set
			{
				SetProperty(ref m_StartCorner, value);
			}
		}

		public Axis startAxis
		{
			get
			{
				return m_StartAxis;
			}
			set
			{
				SetProperty(ref m_StartAxis, value);
			}
		}

		public Vector2 cellSize
		{
			get
			{
				return m_CellSize;
			}
			set
			{
				SetProperty(ref m_CellSize, value);
			}
		}

		public Vector2 spacing
		{
			get
			{
				return m_Spacing;
			}
			set
			{
				SetProperty(ref m_Spacing, value);
			}
		}

		public Constraint constraint
		{
			get
			{
				return m_Constraint;
			}
			set
			{
				SetProperty(ref m_Constraint, value);
			}
		}

		public int constraintCount
		{
			get
			{
				return m_ConstraintCount;
			}
			set
			{
				SetProperty(ref m_ConstraintCount, Mathf.Max(1, value));
			}
		}

		protected AnchorGridLayoutGroup()
		{
		}

		public override void CalculateLayoutInputHorizontal()
		{
			base.CalculateLayoutInputHorizontal();
			int num = 0;
			int num2 = 0;
			if (m_Constraint == Constraint.FixedColumnCount)
			{
				num = (num2 = m_ConstraintCount);
			}
			else if (m_Constraint == Constraint.FixedRowCount)
			{
				num = (num2 = Mathf.CeilToInt((float)base.rectChildren.Count / (float)m_ConstraintCount - 0.001f));
			}
			else
			{
				num = 1;
				num2 = Mathf.CeilToInt(Mathf.Sqrt(base.rectChildren.Count));
			}
			SetLayoutInputForAxis((float)base.padding.horizontal + (cellSize.x + spacing.x) * (float)num - spacing.x, (float)base.padding.horizontal + (cellSize.x + spacing.x) * (float)num2 - spacing.x, -1f, 0);
		}

		public override void CalculateLayoutInputVertical()
		{
			int num = 0;
			if (m_Constraint == Constraint.FixedColumnCount)
			{
				num = Mathf.CeilToInt((float)base.rectChildren.Count / (float)m_ConstraintCount - 0.001f);
			}
			else if (m_Constraint == Constraint.FixedRowCount)
			{
				num = m_ConstraintCount;
			}
			else
			{
				float x = base.rectTransform.rect.size.x;
				int num2 = Mathf.Max(1, Mathf.FloorToInt((x - (float)base.padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
				num = Mathf.CeilToInt((float)base.rectChildren.Count / (float)num2);
			}
			float num3 = (float)base.padding.vertical + (cellSize.y + spacing.y) * (float)num - spacing.y;
			SetLayoutInputForAxis(num3, num3, -1f, 1);
		}

		public override void SetLayoutHorizontal()
		{
			SetCellsAlongAxis(0);
		}

		public override void SetLayoutVertical()
		{
			SetCellsAlongAxis(1);
		}

		private void SetCellsAlongAxis(int axis)
		{
			if (axis == 0)
			{
				for (int i = 0; i < base.rectChildren.Count; i++)
				{
					RectTransform rectTransform = base.rectChildren[i];
					m_Tracker.Add(this, rectTransform, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
					rectTransform.anchorMin = Vector2.zero;
					rectTransform.anchorMax = cellSize;
					rectTransform.sizeDelta = Vector2.zero;
				}
				return;
			}
			float num = 1f;
			float num2 = 1f;
			float x = base.rectTransform.rect.size.x;
			float y = base.rectTransform.rect.size.y;
			int num3 = 1;
			int num4 = 1;
			if (m_Constraint == Constraint.FixedColumnCount)
			{
				num3 = m_ConstraintCount;
				num4 = Mathf.CeilToInt((float)base.rectChildren.Count / (float)num3 - 0.001f);
			}
			else if (m_Constraint != Constraint.FixedRowCount)
			{
				num3 = ((!(cellSize.x + spacing.x <= 0f)) ? Mathf.Max(1, Mathf.FloorToInt((num - (float)base.padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x))) : int.MaxValue);
				num4 = ((!(cellSize.y + spacing.y <= 0f)) ? Mathf.Max(1, Mathf.FloorToInt((num2 - (float)base.padding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y))) : int.MaxValue);
			}
			else
			{
				num4 = m_ConstraintCount;
				num3 = Mathf.CeilToInt((float)base.rectChildren.Count / (float)num4 - 0.001f);
			}
			int num5 = (int)startCorner % 2;
			int num6 = (int)startCorner / 2;
			int num7;
			int num8;
			int num9;
			if (startAxis == Axis.Horizontal)
			{
				num7 = num3;
				num8 = Mathf.Clamp(num3, 1, base.rectChildren.Count);
				num9 = Mathf.Clamp(num4, 1, Mathf.CeilToInt((float)base.rectChildren.Count / (float)num7));
			}
			else
			{
				num7 = num4;
				num9 = Mathf.Clamp(num4, 1, base.rectChildren.Count);
				num8 = Mathf.Clamp(num3, 1, Mathf.CeilToInt((float)base.rectChildren.Count / (float)num7));
			}
			Vector2 vector = new Vector2((float)num8 * cellSize.x + (float)(num8 - 1) * spacing.x, (float)num9 * cellSize.y + (float)(num9 - 1) * spacing.y);
			Vector2 vector2 = new Vector2(GetStartOffset(0, x * vector.x) / x, GetStartOffset(1, y * vector.y) / y);
			for (int j = 0; j < base.rectChildren.Count; j++)
			{
				int num10;
				int num11;
				if (startAxis == Axis.Horizontal)
				{
					num10 = j % num7;
					num11 = j / num7;
				}
				else
				{
					num10 = j / num7;
					num11 = j % num7;
				}
				if (num5 == 1)
				{
					num10 = num8 - 1 - num10;
				}
				if (num6 == 1)
				{
					num11 = num9 - 1 - num11;
				}
				SetChildAlongAxis(base.rectChildren[j], 0, x * (vector2.x + (cellSize[0] + spacing[0]) * (float)num10), x * cellSize[0]);
				SetChildAlongAxis(base.rectChildren[j], 1, y * (vector2.y + (cellSize[1] + spacing[1]) * (float)num11), y * cellSize[1]);
			}
		}
	}
}
