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
            float4 _Spread;
            float4 _Pattern;

            float Hash(float value)
            {
                float h = frac(value * 0.1031);
                h *= h + 33.33;
                h *= h + h;
                return frac(h);
            }

            float Boundary(float index, float count, float seed)
            {
                if (index <= 0.0) return 0.0;
                if (index >= count) return 1.0;
                return (index + (Hash(index * 17.17 + seed) - 0.5) * 0.8 * _Spread.z) / count;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Tear.x <= 0.0 || _Spread.y <= 0.0) return original;

                float2 screenUV = (uv - _BlitScaleBias.zw) / _BlitScaleBias.xy;
                float count = _Spread.x;
                float seed = _Pattern.x * 23.71 + 8.11;
                float cell = min(floor(saturate(screenUV.y) * count), count - 1.0);
                float lower = Boundary(cell, count, seed);
                float upper = Boundary(cell + 1.0, count, seed);
                if (screenUV.y < lower) cell = max(0.0, cell - 1.0);
                else if (screenUV.y > upper) cell = min(count - 1.0, cell + 1.0);
                lower = Boundary(cell, count, seed);
                upper = Boundary(cell + 1.0, count, seed);

                float randomSize = Hash(cell * 41.31 + seed + 3.0);
                float coverage = saturate(_Spread.y * lerp(1.0, lerp(0.7, 1.3, randomSize), _Spread.z));
                float available = (upper - lower) * (1.0 - coverage);
                float start = lower + available * Hash(cell * 13.13 + seed + 7.0);
                float end = start + (upper - lower) * coverage;
                float duty = lerp(0.5, 1.0, saturate((count - 2.0) / 12.0));
                float active = step(Hash(cell * 19.97 + seed + 11.0), duty);
                float mask = step(start, screenUV.y) * (1.0 - step(end, screenUV.y)) * active;
                if (mask <= 0.0) return original;

                float direction = Hash(cell * 29.29 + seed + 19.0) < 0.5 ? -1.0 : 1.0;
                float band = direction * lerp(0.4, 1.0, Hash(cell * 31.73 + seed + 29.0));

                float shift = band * _Tear.x * _Tear.y;
                float rgb = abs(band) * _Tear.x * _Tear.w;
                float2 displaced = float2(saturate(screenUV.x - shift), screenUV.y);
                displaced = displaced * _BlitScaleBias.xy + _BlitScaleBias.zw;
                half4 result = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displaced);
                result.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    float2(saturate(displaced.x + rgb * _BlitScaleBias.x), uv.y)).r;
                result.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    float2(saturate(displaced.x - rgb * _BlitScaleBias.x), uv.y)).b;
                return result;
            }
            ENDHLSL
        }
    }
}
