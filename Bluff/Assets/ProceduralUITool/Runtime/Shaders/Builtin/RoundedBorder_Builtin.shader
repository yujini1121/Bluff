Shader "ProceduralUITool/RoundedBorder_Builtin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        
        // --- Shape Configuration ---
        _ShapeType ("Shape Type", Float) = 0 
        _ShapeVertices ("Shape Vertices", Vector) = (0,0,0,0)
        _ShapeVerticesExt ("Shape Vertices Extended", Vector) = (0,0,0,0)
   
        _VertexCount ("Vertex Count", Float) = 4 
        
        // --- Border and Corner Style ---
        _CornerRadii ("Corner Radii", Vector) = (10,10,10,10)
        _CornerOffsets ("Corner Offsets", Vector) = (0.2,0.2,0.2,0.2)
        _BorderWidth ("Border Width", float) = 2
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _UseIndividualCorners ("Use Individual Corners", float) = 1
        _UseIndividualOffsets ("Use Individual Offsets", float) = 0
        _GlobalCornerOffset ("Global Corner Offset", float) = 0.2 
        _AA ("Anti alias", float) = 1.0
        _RectSize ("Rect Size", Vector) = (100,100,0,0)
        _EdgeSharpness ("Edge Sharpness", float) = 0.2
        _UsePixelPerfectEdges ("Use Pixel Perfect Edges", float) = 0
        
      
        // --- Stencil Operations for Masking ---
        _StencilComp ("Stencil Comparison", Float) = 8 
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _UseProgressBorder ("Use Progress Border", float) = 0
        _ProgressValue ("Progress Value", Range(0,1)) = 1
        _ProgressStartAngle ("Progress Start Angle", float) = -90
        _ProgressDirection ("Progress Direction", float) = 0
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "Default" 
            
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask] 
                WriteMask [_StencilWriteMask]
            }

            Cull Off
            Lighting Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask] 
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc" 
            #include "UnityUI.cginc"

   
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            float4 _MainTex_ST; 
            fixed4 _Color;
            
            float _ShapeType; 
            float4 _ShapeVertices;
            float4 _ShapeVerticesExt;
            float _VertexCount;
            
            float4 _CornerRadii;
            float4 _CornerOffsets;
            float _BorderWidth;
            fixed4 _BorderColor;
            float _UseIndividualCorners;
            float _UseIndividualOffsets;
            float _GlobalCornerOffset;
            float _AA;
            float4 _RectSize;
            float _EdgeSharpness;
            float _UsePixelPerfectEdges;
            
            float _UseProgressBorder;
            float _ProgressValue;
            float _ProgressStartAngle;
            float _ProgressDirection;
            
            float4 _ClipRect;
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR; 
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 rectPos       : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO 
            };
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                OUT.rectPos = v.texcoord;
                return OUT;
            }
            
            float roundedRectSDF(float2 uv, float2 rectSize, float4 radii)
            {
                float2 pos = (uv - 0.5) * rectSize;
                float2 halfSize = rectSize * 0.5; 
                float radius;
                
                if (pos.x > 0 && pos.y > 0) { radius = radii.y; } 
                else if (pos.x <= 0 && pos.y > 0) { radius = radii.x; }
                else if (pos.x <= 0 && pos.y <= 0) { radius = radii.w; }
                else { radius = radii.z; }
                
                float maxRadius = min(halfSize.x, halfSize.y);
                radius = clamp(radius, 0.0, maxRadius); 
                
                float2 d = abs(pos) - (halfSize - radius);
                float outsideDistance = length(max(d, 0.0));
                float insideDistance = min(max(d.x, d.y), 0.0); 
                
                return outsideDistance + insideDistance - radius;
            }
            
            float triangleSDF(float2 uv, float2 rectSize)
            {
                float2 pos = (uv - 0.5) * rectSize;
                float2 p0 = float2(0, rectSize.y * 0.5);
                float2 p1 = float2(-rectSize.x * 0.5, -rectSize.y * 0.5);
                float2 p2 = float2(rectSize.x * 0.5, -rectSize.y * 0.5);
                float2 e0 = p1 - p0;
                float2 e1 = p2 - p1;
                float2 e2 = p0 - p2;
                
                float2 v0 = pos - p0;
                float2 v1 = pos - p1;
                float2 v2 = pos - p2;
                float2 pq0 = v0 - e0 * clamp(dot(v0, e0) / dot(e0, e0), 0.0, 1.0);
                float2 pq1 = v1 - e1 * clamp(dot(v1, e1) / dot(e1, e1), 0.0, 1.0);
                float2 pq2 = v2 - e2 * clamp(dot(v2, e2) / dot(e2, e2), 0.0, 1.0);
                float s = sign(e0.x * e2.y - e0.y * e2.x);
                float2 d = min(min(float2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                                   float2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                                   float2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x))); 
                
                return -sqrt(d.x) * sign(d.y);
            }
            
            float regularPolygonSDF(float2 uv, float2 rectSize, int sides)
            {
                float2 pos = (uv - 0.5) * rectSize;
                float radius = min(rectSize.x, rectSize.y) * 0.5; 
                
                float angle = atan2(pos.y, pos.x);
                float sectorAngle = 6.28318530718 / float(sides);
                float sectorIndex = floor((angle + sectorAngle * 0.5) / sectorAngle); 
                float localAngle = angle - sectorIndex * sectorAngle;
                float distToCenter = length(pos); 
                float distToEdge = distToCenter * cos(abs(localAngle)) - radius * cos(sectorAngle * 0.5);
                
                return distToEdge;
            }
            
            float getShapeSDF(float2 uv, float2 rectSize)
            {
                int shapeType = (int)_ShapeType;
                if (shapeType == 3) { return triangleSDF(uv, rectSize); }
                else if (shapeType == 5) { return regularPolygonSDF(uv, rectSize, 5); }
                else if (shapeType == 6) { return regularPolygonSDF(uv, rectSize, 6); }
                else if (shapeType == 7) { return regularPolygonSDF(uv, rectSize, 6); }
                else if (shapeType == 8) 
                {
                    float2 pos = (uv - 0.5) * rectSize;
                    float radius = min(rectSize.x, rectSize.y) * 0.5; 
                    return length(pos) - radius;
                }
                else
                {
                    float4 cornerRadii = _UseIndividualCorners > 0.5 ? _CornerRadii : _CornerRadii.xxxx; 
                    return roundedRectSDF(uv, rectSize, cornerRadii);
                }
            }
            
            float getProgressMask(float2 uv, float2 rectSize)
            {
                if (_UseProgressBorder < 0.5) return 1.0;
                if (_ProgressValue >= 1.0) return 1.0; // Correction for 100%
                if (_ProgressValue <= 0.0) return 0.0;
                float2 center = float2(0.5, 0.5);
                float2 dir = uv - center;
                // Calculate current angle
                float currentAngle = atan2(dir.y, dir.x) * 57.2958; // rad to deg
                
                // Normalize angle (0-360)
                if (currentAngle < 0) currentAngle += 360.0;
                // Normalized start angle
                float startAngle = _ProgressStartAngle;
                while (startAngle < 0) startAngle += 360.0;
                while (startAngle > 360) startAngle -= 360.0;
                // Calculate target angle based on progress
                float progressAngle = _ProgressValue * 360.0;
                float targetAngle = startAngle + (_ProgressDirection > 0.5 ? -progressAngle : progressAngle);
                // Normalize target angle
                while (targetAngle < 0) targetAngle += 360.0;
                while (targetAngle > 360) targetAngle -= 360.0;
                
                // Determine if the current pixel is within the progress range
                if (_ProgressDirection > 0.5) // CounterClockwise
                {
                    if (startAngle < targetAngle)
                    {
                         return (currentAngle < startAngle || currentAngle > targetAngle) ? 1.0 : 0.0;
                    }
                    else
                    {
                        return (currentAngle < startAngle && currentAngle > targetAngle) ? 1.0 : 0.0;
                    }
                }
                else // Clockwise
                {
                    if (startAngle > targetAngle)
                    {
                         return (currentAngle > startAngle || currentAngle < targetAngle) ? 1.0 : 0.0;
                    }
                    else
                    {
                        return (currentAngle > startAngle && currentAngle < targetAngle) ? 1.0 : 0.0;
                    }
                }
            }


            // NEW SHARP ALPHA FUNCTION - Sharper Edges
            float sharpAlpha(float distance, float smoothing)
            {
                // If pixel perfect is enabled, use pure step function
                if (_UsePixelPerfectEdges > 0.5)
                {
                    return distance <= 0.0 ? 1.0 : 0.0;
                }
                
                // Use edge sharpness to control the transition
                float sharpness = smoothing * _EdgeSharpness * 0.1; // Drastically reduce the transition area
                return 1.0 - smoothstep(-sharpness, sharpness, distance);
            }
            
            // Alternative function, completely pixel perfect
            float pixelPerfectAlpha(float distance)
            {
                return distance <= 0.0 ? 1.0 : 0.0;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                float2 rectSize = max(_RectSize.xy, float2(2, 2));
                float mainDistance = getShapeSDF(IN.rectPos, rectSize);
                
                // SHARP EDGES: Use a direct threshold instead of smooth
                float edgeThreshold = 0.5; // Pixel threshold for the edge
                float aaRange = _AA * _EdgeSharpness * 0.1; // Very small AA range
                
                // Calculate alpha with a very abrupt transition
                float mainAlpha;
                if (_UsePixelPerfectEdges > 0.5)
                {
                    // Completely pixel perfect without any anti-aliasing
                    mainAlpha = pixelPerfectAlpha(mainDistance);
                }
                else if (abs(mainDistance) < aaRange && aaRange > 0.001) 
                {
                    // Only apply smoothing in a very small range
                    mainAlpha = sharpAlpha(mainDistance, _AA);
                } 
                else 
                {
                    // Outside the range, use binary values
                    mainAlpha = mainDistance < 0.0 ? 1.0 : 0.0;
                }
                
                fixed4 finalColor = fixed4(0, 0, 0, 0);
                if (mainAlpha > 0.001)
                {
                    float borderInnerAlpha;
                    float borderMask;
                    
                    if (_BorderWidth > 0.0) 
                    {
                        float borderDistance = mainDistance + _BorderWidth;
                        // Apply the same sharp logic for the border
                        if (_UsePixelPerfectEdges > 0.5)
                        {
                            borderInnerAlpha = pixelPerfectAlpha(borderDistance);
                        }
                        else if (abs(borderDistance) < aaRange && aaRange > 0.001) 
                        {
                            borderInnerAlpha = sharpAlpha(borderDistance, _AA);
                        } 
                        else 
                        {
                            borderInnerAlpha = borderDistance < 0.0 ? 1.0 : 0.0;
                        }
                        
                        borderMask = saturate(mainAlpha - borderInnerAlpha);
                    }
                    else
                    {
                        borderInnerAlpha = mainAlpha;
                        borderMask = 0.0;
                    }

                    if (_UseProgressBorder > 0.5 && _BorderWidth > 0.0)
                    {
                        float progressMask = getProgressMask(IN.rectPos, rectSize);
                        borderMask *= progressMask;
                    }


                    if (borderInnerAlpha > 0.001)
                    {
                        fixed4 contentColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                        contentColor.a *= borderInnerAlpha;
                        finalColor = contentColor;
                    }
                    
                    if (borderMask > 0.001)
                    {
                        fixed4 borderColor = _BorderColor;
                        borderColor.a *= borderMask;
                        
                        // More aggressive blend for sharp edges
                        if (_UsePixelPerfectEdges > 0.5)
                        {
                            // For pixel perfect, use binary blend
                            if (borderColor.a > 0.5) 
                            {
                                finalColor = borderColor;
                            }
                        }
                        else
                        {
                            // Normal blend but with more defined alpha
                            float totalAlpha = finalColor.a + borderColor.a;
                            if (totalAlpha > 0.0001)
                            {
                                finalColor.rgb = (finalColor.rgb * finalColor.a + borderColor.rgb * borderColor.a) / totalAlpha;
                                finalColor.a = max(finalColor.a, borderColor.a); 
                            }
                            else
                            {
                                finalColor = borderColor;
                            }
                        }
                    }
                }
                
                #ifdef UNITY_UI_CLIP_RECT
                    finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                    clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}