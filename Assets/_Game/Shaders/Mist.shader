// Black mist drifting around an exhibit past the Office's left wall: _Reveal opens a smoky hole in its middle (0 shut,
// 1 open) and the cloud thins out towards the quad's edge so it melts into the dark around it.
Shader "Haunted/Mist"
{
    Properties
    {
        _BaseColor("Mist", Color) = (0.02, 0.02, 0.03, 1)
        _Reveal("Reveal", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Reveal;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            float Smoke(float2 p)
            {
                float sum = 0, weight = 0.5;
                for (int o = 0; o < 4; o++)
                {
                    sum += Noise(p) * weight;
                    p = p * 2.03 + float2(1.7, 9.2);
                    weight *= 0.5;
                }
                return sum;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float2 drift = float2(_Time.y * 0.06, -_Time.y * 0.04);
                float smoke = Smoke(i.uv * 3 + drift) + 0.5 * Smoke(i.uv * 5 - drift * 1.6);
                float d = length(p) + (smoke - 0.55) * 0.5;
                float hole = lerp(-0.4, 0.6, _Reveal);
                float cloud = smoothstep(hole - 0.25, hole, d);
                float rim = 1 - smoothstep(0.75, 1.0, length(p) + (smoke - 0.55) * 0.25);
                return half4(_BaseColor.rgb, cloud * rim);
            }
            ENDHLSL
        }
    }
}
