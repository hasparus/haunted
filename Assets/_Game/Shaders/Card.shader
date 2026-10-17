// The hand-drawn cards (demons, desks): their lines boil on twos like the office's ink, a paper-white flash lights the
// fills on impact while the ink stays dark, and they dim towards the edges of the room's light pool, gentler than the floor.
Shader "Haunted/Card"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Flash("Flash", Range(0, 1)) = 0
        _Boil("Line boil", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

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
                half _Flash, _Boil;
            CBUFFER_END
            float _MindTime;
            float4 _PoolCenter;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 feet : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                // URP 17 hands a SpriteRenderer's flip and colour over per draw, not in the vertices.
                float3 pos = float3(v.positionOS.xy * unity_SpriteProps.xy, v.positionOS.z);
                o.positionCS = TransformObjectToHClip(pos);
                o.color = v.color * unity_SpriteColor;
                o.uv = v.uv;
                // Per vertex, so it survives sprite batching; an upright card barely moves in xz from feet to head.
                o.feet = TransformObjectToWorld(pos).xz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float boil = floor(_MindTime * 10) * 3.1;
                float2 uv = i.uv + (float2(Noise(i.uv * 14 + boil), Noise(i.uv * 14 + 7 + boil)) - 0.5) * 0.004 * _Boil;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;

                c.rgb = lerp(c.rgb, half3(0.96, 0.92, 0.84), _Flash * smoothstep(0.15, 0.35, dot(c.rgb, half3(0.3, 0.59, 0.11))));

                // Lit by where the card stands, like Don't Starve's billboards: one light value for the whole drawing.
                half pool = exp(-0.9 * pow(length(i.feet - _PoolCenter.xy) / 9, 2.6));
                c.rgb *= 0.72 + 0.28 * pool;
                return c;
            }
            ENDHLSL
        }
    }
}
