// The hand-drawn cards (demons, desks): their lines boil on twos like the office's ink, a paper-white flash lights the
// fills on impact while the ink stays dark, and they dim towards the edges of the room's light pool, gentler than the floor.
Shader "Haunted/Card"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Flash("Flash", Range(0, 1)) = 0
        _Boil("Line boil", Range(0, 1)) = 1
        _Aloft("Drawn this far nearer than where it stands (m): flying things that must stay in front", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        // Writes the depth of where it stands (see Vert) and cuts its see-through edge, so cards hide each other by that.
        ZWrite On

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Sorting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _Flash, _Boil;
                float _Aloft;
            CBUFFER_END
            float _MindTime;
            float4 _PoolCenter;
            // Light spilling from open cracks (xz, unused, strength), set by MindFx: warm daylight, Don't Starve's falloff.
            float4 _CrackGlows[5];
            half3 CrackLight(float2 xz)
            {
                half sum = 0;
                for (int g = 0; g < 5; g++)
                    sum += _CrackGlows[g].w * exp(-0.9 * pow(length(xz - _CrackGlows[g].xy) / 3.8, 2.2));
                return half3(1, 0.91, 0.69) * sum;
            }


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
                // The whole card sits at the depth of the floor under its origin, Don't Starve-style (Sorting.hlsl).
                // Sprites draw one per call here, so the origin is the feet (or, for a floating prop, above them).
                float3 feet = Underneath(TransformObjectToWorld(float3(0, 0, 0)));
                o.positionCS.z = SortedZ(o.positionCS, PushedBack(feet, -_Aloft));
                o.feet = feet.xz;
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
                float boil = floor(_MindTime * 2.5) * 3.1;
                float2 uv = i.uv + (float2(Noise(i.uv * 14 + boil), Noise(i.uv * 14 + 7 + boil)) - 0.5) * 0.004 * _Boil;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;

                c.rgb = lerp(c.rgb, half3(0.96, 0.92, 0.84), _Flash * smoothstep(0.15, 0.35, dot(c.rgb, half3(0.3, 0.59, 0.11))));

                // Lit by where the card stands, like Don't Starve's billboards: one light value for the whole drawing.
                half pool = exp(-0.9 * pow(length(i.feet - _PoolCenter.xy) / 9, 2.6));
                c.rgb *= 0.72 + 0.28 * pool + CrackLight(i.feet) * 0.9;

                // Rim light from the lamp, upper right: where the drawing ends in that direction, a thin warm edge.
                half beyond = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.012, 0.01)).a;
                c.rgb += half3(1, 0.83, 0.28) * saturate(c.a - beyond) * (0.2 + 0.3 * pool);
                clip(c.a - 0.5);
                return c;
            }
            ENDHLSL
        }
    }
}
