using UnityEngine;

namespace ProceduralUITool.Runtime
{

    [CreateAssetMenu(fileName = "New Procedural UI Profile", menuName = "Procedural UI Tool/Procedural UI Profile", order = 1)]
    public class ProceduralUIProfile : ScriptableObject
    {

        public enum Unit { Pixels, Percent }


        public enum ShapeType
        {
            Rectangle = 0,
            Triangle = 3,
            Square = 4,
            Pentagon = 5,
            Hexagon = 6,
            Star = 7,
            Circle = 8
        }

        public enum ProgressDirection { Clockwise, CounterClockwise }


        [Header("Shape Configuration")]
        [Tooltip("Type of shape for correct border calculation.")]
        public ShapeType shapeType = ShapeType.Rectangle;

        [Tooltip("Number of points for the Star shape.")]
        [Range(3, 20)]
        public int starPoints = 5;

        [Tooltip("Ratio of the inner radius for the Star shape, controlling the sharpness of the points.")]
        [Range(0.1f, 1.0f)]
        public float starInnerRatio = 0.5f;

        [Tooltip("Custom vertices for complex shapes, defined in normalized (0-1) coordinates.")]
        public Vector2[] customVertices = new Vector2[0];

        [Header("")]
        [Tooltip("Unit for radius values: absolute pixels or a percentage of the smallest dimension.")]
        public Unit cornerRadiusUnit = Unit.Pixels;

        [Tooltip("If enabled, allows setting a different radius for each corner.")]
        public bool useIndividualCorners = false;

        [Tooltip("The global corner radius applied to all corners when 'Individual Corners' is disabled.")]
        [Range(0f, 100f)]
        public float globalCornerRadius = 10f;

        [Tooltip("Radius of the top-left corner.")]
        [Range(0f, 100f)]
        public float cornerRadiusTopLeft = 10f;

        [Tooltip("Radius of the top-right corner.")]
        [Range(0f, 100f)]
        public float cornerRadiusTopRight = 10f;

        [Tooltip("Radius of the bottom-left corner.")]
        [Range(0f, 100f)]
        public float cornerRadiusBottomLeft = 10f;

        [Tooltip("Radius of the bottom-right corner.")]
        [Range(0f, 100f)]
        public float cornerRadiusBottomRight = 10f;

        [Header("Corner Transition Configuration")]
        [Tooltip("If enabled, allows setting a different transition smoothness for each corner.")]
        public bool useIndividualOffsets = false;

        [Tooltip("Controls the smoothness of the corner transition (0 = sharp, 1 = very smooth).")]
        [Range(0f, 1f)]
        public float globalCornerOffset = 0.2f;

        [Tooltip("Transition smoothness of the top-left corner.")]
        [Range(0f, 1f)]
        public float cornerOffsetTopLeft = 0.2f;

        [Tooltip("Transition smoothness of the top-right corner.")]
        [Range(0f, 1f)]
        public float cornerOffsetTopRight = 0.2f;

        [Tooltip("Transition smoothness of the bottom-left corner.")]
        [Range(0f, 1f)]
        public float cornerOffsetBottomLeft = 0.2f;

        [Tooltip("Transition smoothness of the bottom-right corner.")]
        [Range(0f, 1f)]
        public float cornerOffsetBottomRight = 0.2f;
        
        [Header("Border Configuration")]
        [Tooltip("Controls the sharpness of the edges. Low values = sharper edges.")]
        [Range(0.01f, 2f)]
        public float edgeSharpness = 0.2f;

        [Tooltip("Enables perfectly sharp (pixel-perfect) edges without anti-aliasing.")]
        public bool usePixelPerfectEdges = false;

        [Tooltip("Unit for border width: absolute pixels or a percentage of the smallest dimension.")]
        public Unit borderWidthUnit = Unit.Pixels;

        [Tooltip("Width of the border.")]
        [Range(0f, 100f)]
        public float borderWidth = 2f;

        [Tooltip("Color of the border.")]
        public Color borderColor = Color.black;
        
        [Header("")]
        [Tooltip("The main fill color of the shape.")]
        public Color fillColor = Color.white;

        [Header("")]
        [Tooltip("Enables progress border mode (useful for loading bars, indicators, etc.).")]
        public bool useProgressBorder = false;

        [Tooltip("Progress value from 0 to 1 (0% to 100%).")]
        [Range(0f, 1f)]
        public float progressValue = 1f;

        [Tooltip("Starting angle for the progress border in degrees (0=right, 90=up).")]
        [Range(-360f, 360f)]
        public float progressStartAngle = -90f;

        [Tooltip("Direction in which the progress fills.")]
        public ProgressDirection progressDirection = ProgressDirection.Clockwise;


        public void SetShapeData(ShapeType type, Vector2[] vertices = null, int starPoints = 5, float starInnerRatio = 0.5f)
        {
            shapeType = type;
            this.starPoints = starPoints;
            this.starInnerRatio = starInnerRatio;

            if (vertices != null && vertices.Length > 0)
            {
                customVertices = new Vector2[vertices.Length];
                System.Array.Copy(vertices, customVertices, vertices.Length);
            }
            else
            {
                customVertices = new Vector2[0];
            }
        }


        public Vector2[] GetShapeVertices()
        {
            if (customVertices != null && customVertices.Length > 0)
            {
                return customVertices;
            }
            return GenerateDefaultVertices();
        }

        private Vector2[] GenerateDefaultVertices()
        {
            switch (shapeType)
            {
                case ShapeType.Triangle:
                    return new Vector2[] { new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
                case ShapeType.Square:
                case ShapeType.Rectangle:
                    return new Vector2[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                case ShapeType.Pentagon: return GenerateRegularPolygonVertices(5);
                case ShapeType.Hexagon: return GenerateRegularPolygonVertices(6);
                case ShapeType.Star: return GenerateStarVertices(starPoints, starInnerRatio);
                case ShapeType.Circle: return GenerateRegularPolygonVertices(32); // Approximation of a circle
                default: return GenerateRegularPolygonVertices(4);
            }
        }


        private Vector2[] GenerateRegularPolygonVertices(int sides)
        {
            Vector2[] vertices = new Vector2[sides];
            float angleStep = 360f / sides;
            for (int i = 0; i < sides; i++)
            {
                float angle = (90f - i * angleStep) * Mathf.Deg2Rad;
                vertices[i] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
            }
            return vertices;
        }


        private Vector2[] GenerateStarVertices(int points, float innerRatio)
        {
            Vector2[] vertices = new Vector2[points * 2];
            float angleStep = 360f / (points * 2);
            for (int i = 0; i < points * 2; i++)
            {
                float radius = (i % 2 == 0) ? 0.5f : 0.5f * innerRatio;
                float angle = (90f - i * angleStep) * Mathf.Deg2Rad;
                vertices[i] = new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius);
            }
            return vertices;
        }


        public Vector4 GetCornerRadii()
        {
            if (useIndividualCorners)
                return new Vector4(cornerRadiusTopLeft, cornerRadiusTopRight, cornerRadiusBottomRight, cornerRadiusBottomLeft);
            else
                return new Vector4(globalCornerRadius, globalCornerRadius, globalCornerRadius, globalCornerRadius);
        }
        

        public Vector4 GetCornerOffsets()
        {
            if (useIndividualOffsets)
                return new Vector4(Mathf.Clamp01(cornerOffsetTopLeft), Mathf.Clamp01(cornerOffsetTopRight), Mathf.Clamp01(cornerOffsetBottomRight), Mathf.Clamp01(cornerOffsetBottomLeft));
            else
            {
                float clampedOffset = Mathf.Clamp01(globalCornerOffset);
                return new Vector4(clampedOffset, clampedOffset, clampedOffset, clampedOffset);
            }
        }
        

        public void ApplyTo(ProceduralUIComponent component)
        {
            if (component == null) return;
            component.SetProfile(this);
        }
        

        public ProceduralUIProfile Clone()
        {
            ProceduralUIProfile clone = CreateInstance<ProceduralUIProfile>();

            clone.shapeType = shapeType;
            clone.starPoints = starPoints;
            clone.starInnerRatio = starInnerRatio;
            clone.customVertices = (Vector2[])customVertices.Clone();
            
            clone.cornerRadiusUnit = cornerRadiusUnit;
            clone.borderWidthUnit = borderWidthUnit;
            clone.useIndividualCorners = useIndividualCorners;
            clone.globalCornerRadius = globalCornerRadius;
            clone.cornerRadiusTopLeft = cornerRadiusTopLeft;
            clone.cornerRadiusTopRight = cornerRadiusTopRight;
            clone.cornerRadiusBottomLeft = cornerRadiusBottomLeft;
            clone.cornerRadiusBottomRight = cornerRadiusBottomRight;
            clone.useIndividualOffsets = useIndividualOffsets;
            clone.globalCornerOffset = globalCornerOffset;
            clone.cornerOffsetTopLeft = cornerOffsetTopLeft;
            clone.cornerOffsetTopRight = cornerOffsetTopRight;
            clone.cornerOffsetBottomLeft = cornerOffsetBottomLeft;
            clone.cornerOffsetBottomRight = cornerOffsetBottomRight;
            clone.borderWidth = borderWidth;
            clone.borderColor = borderColor;
            clone.fillColor = fillColor;
            clone.edgeSharpness = edgeSharpness;
            clone.usePixelPerfectEdges = usePixelPerfectEdges;

            clone.useProgressBorder = useProgressBorder;
            clone.progressValue = progressValue;
            clone.progressStartAngle = progressStartAngle;
            clone.progressDirection = progressDirection;

            clone.name = $"{name}_Clone";
            return clone;
        }

        public void ResetToDefaults()
        {
            shapeType = ShapeType.Rectangle;
            starPoints = 5;
            starInnerRatio = 0.5f;
            customVertices = new Vector2[0];

            cornerRadiusUnit = Unit.Pixels;
            borderWidthUnit = Unit.Pixels;

            useIndividualCorners = false;
            globalCornerRadius = 10f;
            cornerRadiusTopLeft = 10f;
            cornerRadiusTopRight = 10f;
            cornerRadiusBottomLeft = 10f;
            cornerRadiusBottomRight = 10f;

            useIndividualOffsets = false;
            globalCornerOffset = 0.2f;
            cornerOffsetTopLeft = 0.2f;
            cornerOffsetTopRight = 0.2f;
            cornerOffsetBottomLeft = 0.2f;
            cornerOffsetBottomRight = 0.2f;
            
            edgeSharpness = 0.2f;
            usePixelPerfectEdges = false;

            borderWidth = 2f;
            borderColor = Color.black;
            fillColor = Color.white;

            useProgressBorder = false;
            progressValue = 1f;
            progressStartAngle = -90f;
            progressDirection = ProgressDirection.Clockwise;
        }

        private void Reset()
        {
            ResetToDefaults();
        }


        private void OnValidate()
        {
            globalCornerRadius = Mathf.Max(0f, globalCornerRadius);
            cornerRadiusTopLeft = Mathf.Max(0f, cornerRadiusTopLeft);
            cornerRadiusTopRight = Mathf.Max(0f, cornerRadiusTopRight);
            cornerRadiusBottomLeft = Mathf.Max(0f, cornerRadiusBottomLeft);
            cornerRadiusBottomRight = Mathf.Max(0f, cornerRadiusBottomRight);
            
            globalCornerOffset = Mathf.Clamp01(globalCornerOffset);
            cornerOffsetTopLeft = Mathf.Clamp01(cornerOffsetTopLeft);
            cornerOffsetTopRight = Mathf.Clamp01(cornerOffsetTopRight);
            cornerOffsetBottomLeft = Mathf.Clamp01(cornerOffsetBottomLeft);
            cornerOffsetBottomRight = Mathf.Clamp01(cornerOffsetBottomRight);

            borderWidth = Mathf.Max(0f, borderWidth);
        }
    }
}