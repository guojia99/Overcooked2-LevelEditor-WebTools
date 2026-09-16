namespace UnityEngine.UI
{
	[AddComponentMenu("T17_UI/Layout/Grid Layout Group", 152)]
	public class T17GridLayoutGroup : LayoutGroup
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
		protected RectOffset m_OurPadding;

		[SerializeField]
		public Vector2 m_ScalingFactor = new Vector2(1f, 1f);

		[SerializeField]
		public bool m_ScaleAspect;

		[SerializeField]
		protected Vector2 m_CellSize = new Vector2(100f, 100f);

		[SerializeField]
		protected Vector2 m_Spacing = Vector2.zero;

		[SerializeField]
		protected Corner m_StartCorner;

		[SerializeField]
		protected Axis m_StartAxis;

		[SerializeField]
		protected Constraint m_Constraint;

		[SerializeField]
		protected int m_ConstraintCount = 2;

		[SerializeField]
		public Vector2 m_CurrentSizeFactor = Vector2.one;

		[SerializeField]
		protected bool m_ResizeToFitChildren;

		[SerializeField]
		public int m_CellCountX;

		[SerializeField]
		public int m_CellCountY;

		private Vector2 m_NewCellSize;

		private Vector2 m_NewSpacing;

		public RectOffset ourPadding
		{
			get
			{
				return m_OurPadding;
			}
			set
			{
				SetProperty(ref m_OurPadding, value);
			}
		}

		public Vector2 scalingFactor
		{
			get
			{
				return m_ScalingFactor;
			}
			set
			{
				SetProperty(ref m_ScalingFactor, value);
			}
		}

		public bool scaleAspect
		{
			get
			{
				return m_ScaleAspect;
			}
			set
			{
				SetProperty(ref m_ScaleAspect, value);
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

		public bool resizeToFitChildren
		{
			get
			{
				return m_ResizeToFitChildren;
			}
			set
			{
				SetProperty(ref m_ResizeToFitChildren, value);
			}
		}

		protected T17GridLayoutGroup()
		{
		}

		public void ForceRefresh()
		{
			SetDirty();
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
					rectTransform.anchorMin = Vector2.up;
					rectTransform.anchorMax = Vector2.up;
					rectTransform.sizeDelta = ((m_Constraint != Constraint.Flexible) ? m_NewCellSize : cellSize);
				}
				return;
			}
			float x = base.rectTransform.rect.size.x;
			float y = base.rectTransform.rect.size.y;
			int num = 1;
			int num2 = 1;
			if (m_Constraint == Constraint.FixedColumnCount)
			{
				num = m_ConstraintCount;
				num2 = Mathf.CeilToInt((float)base.rectChildren.Count / (float)num - 0.001f);
			}
			else if (m_Constraint != Constraint.FixedRowCount)
			{
				num = ((!(cellSize.x + spacing.x <= 0f)) ? Mathf.Max(1, Mathf.FloorToInt((x - (float)base.padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x))) : int.MaxValue);
				num2 = ((!(cellSize.y + spacing.y <= 0f)) ? Mathf.Max(1, Mathf.FloorToInt((y - (float)base.padding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y))) : int.MaxValue);
			}
			else
			{
				num2 = m_ConstraintCount;
				num = Mathf.CeilToInt((float)base.rectChildren.Count / (float)num2 - 0.001f);
			}
			int num3 = (int)startCorner % 2;
			int num4 = (int)startCorner / 2;
			int num5;
			int num6;
			int num7;
			if (startAxis == Axis.Horizontal)
			{
				num5 = num;
				num6 = Mathf.Clamp(num, 1, base.rectChildren.Count);
				num7 = Mathf.Clamp(num2, 1, Mathf.CeilToInt((float)base.rectChildren.Count / (float)num5));
			}
			else
			{
				num5 = num2;
				num7 = Mathf.Clamp(num2, 1, base.rectChildren.Count);
				num6 = Mathf.Clamp(num, 1, Mathf.CeilToInt((float)base.rectChildren.Count / (float)num5));
			}
			m_CellCountX = num6;
			m_CellCountY = num7;
			Vector2 vector = new Vector2((float)num6 * cellSize.x + (float)(num6 - 1) * spacing.x, (float)num7 * cellSize.y + (float)(num7 - 1) * spacing.y);
			if (m_Constraint != Constraint.Flexible)
			{
				float num8 = Mathf.Clamp((x - (float)m_OurPadding.horizontal) / vector.x, m_ScalingFactor.x, m_ScalingFactor.y);
				float num9 = Mathf.Clamp((y - (float)m_OurPadding.vertical) / vector.y, m_ScalingFactor.x, m_ScalingFactor.y);
				if (m_ScaleAspect)
				{
					float num10 = 1f;
					num10 = ((constraint == Constraint.FixedColumnCount) ? Mathf.Max(num8) : ((constraint != Constraint.FixedRowCount) ? Mathf.Max(num8, num9) : Mathf.Max(num9)));
					m_CurrentSizeFactor = new Vector2(num10, num10);
				}
				else
				{
					m_CurrentSizeFactor = new Vector2(num8, num9);
				}
				m_NewCellSize.x = m_CurrentSizeFactor.x * cellSize.x;
				m_NewCellSize.y = m_CurrentSizeFactor.y * cellSize.y;
				m_NewSpacing.x = m_CurrentSizeFactor.x * spacing.x;
				m_NewSpacing.y = m_CurrentSizeFactor.y * spacing.y;
				base.padding.left = Mathf.FloorToInt((float)m_OurPadding.left * m_CurrentSizeFactor.x);
				base.padding.right = Mathf.FloorToInt((float)m_OurPadding.right * m_CurrentSizeFactor.x);
				base.padding.top = Mathf.FloorToInt((float)m_OurPadding.top * m_CurrentSizeFactor.y);
				base.padding.bottom = Mathf.FloorToInt((float)m_OurPadding.bottom * m_CurrentSizeFactor.y);
				vector = new Vector2((float)num6 * m_NewCellSize.x + (float)(num6 - 1) * m_NewSpacing.x, (float)num7 * m_NewCellSize.y + (float)(num7 - 1) * m_NewSpacing.y);
			}
			Vector2 vector2 = new Vector2(GetStartOffset(0, vector.x), GetStartOffset(1, vector.y));
			Vector2 vector3 = ((m_Constraint != Constraint.Flexible) ? m_NewCellSize : cellSize);
			Vector2 vector4 = ((m_Constraint != Constraint.Flexible) ? m_NewSpacing : spacing);
			for (int j = 0; j < base.rectChildren.Count; j++)
			{
				int num11;
				int num12;
				if (startAxis == Axis.Horizontal)
				{
					num11 = j % num5;
					num12 = j / num5;
				}
				else
				{
					num11 = j / num5;
					num12 = j % num5;
				}
				if (num3 == 1)
				{
					num11 = num6 - 1 - num11;
				}
				if (num4 == 1)
				{
					num12 = num7 - 1 - num12;
				}
				SetChildAlongAxis(base.rectChildren[j], 0, vector2.x + (vector3[0] + vector4[0]) * (float)num11, vector3[0]);
				SetChildAlongAxis(base.rectChildren[j], 1, vector2.y + (vector3[1] + vector4[1]) * (float)num12, vector3[1]);
			}
			if (m_ResizeToFitChildren)
			{
				if (constraint == Constraint.FixedColumnCount)
				{
					m_Tracker.Add(this, base.rectTransform, DrivenTransformProperties.SizeDeltaY);
					base.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, vector.y + (float)base.padding.top + (float)base.padding.bottom);
				}
				else if (constraint == Constraint.FixedRowCount)
				{
					m_Tracker.Add(this, base.rectTransform, DrivenTransformProperties.SizeDeltaX);
					base.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, vector.x + (float)base.padding.left + (float)base.padding.right);
				}
			}
		}
	}
}
