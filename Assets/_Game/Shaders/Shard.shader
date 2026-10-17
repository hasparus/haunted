// An exhibit's live view hanging in the dark past the Office's left wall: drifting smoky noise eats its edges, never
// quite still, and _Reveal pushes the mist back from the middle (0 all mist, 1 open). It sits far behind the room, so
// the room's floor and walls always cover it.
Shader "Haunted/Shard"
{
    Properties
    {
        _MainTex("View", 2D) = "black" {}
        _Reveal("Reveal", Range(0, 1)) = 0
        _Seed("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Reveal, _Seed;
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
                float2 drift = float2(_Time.y * 0.06, -_Time.y * 0.04) + _Seed * 3.7;
                float smoke = Smoke(i.uv * 3 + drift) + 0.5 * Smoke(i.uv * 5 - drift * 1.6);
                float d = length(p) + (smoke - 0.55) * 0.55;
                float edge = lerp(-0.4, 0.95, _Reveal);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                c.a = 1 - smoothstep(edge - 0.22, edge, d);
                return c;
            }
            ENDHLSL
        }
    }
}
