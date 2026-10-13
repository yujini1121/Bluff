Shader "Hidden/Bluff/ScreenSplit"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Screen Split"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Split;

            void Crack(float2 p, float2 normal, float offset, float growth,
                float aspect, float pixel, inout float mask, inout float2 movement)
            {
                float2 tangent = float2(-normal.y, normal.x);
                float extent = dot(abs(tangent), float2(aspect, 1.0)) * 0.5;
                float along = (dot(p, tangent) + extent) / (2.0 * extent);
                float revealed = step(along, growth) * step(0.0001, growth);
                float distance = dot(p, normal) - offset;
                float width = _Split.z * pixel * 0.5;
                float crackMask = (1.0 - smoothstep(max(0.0, width - pixel * 0.5),
                    width + pixel * 0.5, abs(distance))) * step(0.0001, _Split.z);
                mask = max(mask, crackMask * revealed);
                movement += normal * (step(0.0, distance) * 2.0 - 1.0) * revealed;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Split.x <= 0.0 || _Split.y <= 0.0) return original;

                float2 screenUV = (uv - _BlitScaleBias.zw) / _BlitScaleBias.xy;
                float aspect = _ScaledScreenParams.x / _ScaledScreenParams.y;
                float pixel = rcp(_ScaledScreenParams.y);
                float2 p = (screenUV - 0.5) * float2(aspect, 1.0);
                float mask = 0.0;
                float2 movement = 0.0;
                float progress = _Split.y * 3.0;
                Crack(p, float2(0.6, 0.8), 0.06, saturate(progress), aspect, pixel, mask, movement);
                Crack(p, float2(-0.8, 0.6), -0.14, saturate(progress - 1.0), aspect, pixel, mask, movement);
                Crack(p, normalize(float2(0.25, 0.97)), 0.20, saturate(progress - 2.0), aspect, pixel, mask, movement);

                float2 displaced = screenUV - movement * (_Split.w * _Split.x / 3.0) / float2(aspect, 1.0);
                float2 sampleUV = saturate(displaced) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                half4 result = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                result.rgb *= 1.0 - mask * _Split.x;
                return result;
            }
            ENDHLSL
        }
    }
}
