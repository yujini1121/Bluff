Shader "Hidden/Bluff/DigitalScreenTearing"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Digital Screen Tearing"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Tear;
            float4 _Bands;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float a = 1.0 - step(_Tear.z * 0.5, abs(uv.y - _Bands.x));
                float b = (1.0 - step(_Tear.z * 0.325, abs(uv.y - _Bands.y))) * _Bands.z;
                float band = a > 0.0 ? a : -b;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Tear.x <= 0.0 || abs(band) <= 0.0) return original;

                float shift = band * _Tear.x * _Tear.y;
                float rgb = abs(band) * _Tear.x * _Tear.w;
                float2 displaced = float2(saturate(uv.x - shift), uv.y);
                half4 result = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displaced);
                result.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    float2(saturate(displaced.x + rgb), uv.y)).r;
                result.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    float2(saturate(displaced.x - rgb), uv.y)).b;
                return result;
            }
            ENDHLSL
        }
    }
}
