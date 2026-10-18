// The therapist's aim and strike drawn on the floor, over its soft glow: the aim's sight in the lamp's warm colour,
// the strike's shadow multiplied into the floor. r is in radii of the light's reach: 1 is the edge that counts. _Aim is
// how far the aim is (0 to 1), _Dark how far the strike has darkened the board (0 to 1).
Shader "Haunted/SpotInk"
{
    Properties
    {
        _Aim("Aim", Range(0, 1)) = 0
        _Dark("Dark", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-50" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One SrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Aim, _Dark;
            CBUFFER_END
            float _MindTime;

            // The quad spans this many radii from its middle to its edge: past the board's corners from anywhere on it.
            #define SPAN 16.0
            // The lamp's warm (1, 0.88, 0.6), in linear.
            static const half3 Warm = half3(1, 0.75, 0.32);
            static const half Shade = 0.11;
            static const half Dim = 0.3;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
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

            // A line of half-width w around d = 0, drawn with a little ink bleed.
            half Line(float d, float w) { return 1 - smoothstep(w * 0.6, w, abs(d)); }

            // The aim: the strike's edge sighted through crosshair ticks, a clock of ticks filling up around it. How much of
            // the sight covers this point.
            half Sight(float r, float turn, float angle, float2 p)
            {
                half k = Line(r - 1, 0.022);
                float quarter = abs(frac(angle / (PI / 2) + 0.5) - 0.5) * (PI / 2) * r;
                k = max(k, Line(quarter, 0.035) * (abs(r - 1) < 0.2));
                float tick = frac(turn * 12 + 0.5) - 0.5, which = floor(turn * 12 + 0.5) / 12;
                half on = which < _Aim ? 1 : 0.3;
                k = max(k, Line(tick / 12 * 2 * PI * r, 0.03) * (r > 1.08 && r < 1.3) * on);
                half cross = Line(p.x, 0.02) * (abs(p.y) < 0.22) + Line(p.y, 0.02) * (abs(p.x) < 0.22);
                return saturate(max(k, cross));
            }

            // The strike: the board around the light dims, cross-hatched into shadow near it. What the floor is multiplied by.
            half Darken(float r, float2 xz, float boil)
            {
                half c = 1;
                float outside = smoothstep(1, 1.2, r);
                c = lerp(c, Dim * 2, outside * _Dark);
                float shade = outside * (1 - smoothstep(2.6, 4, r)) * 1.3 * _Dark;
                float2 jitter = float2(Noise(xz * 3 + boil), Noise(xz * 3 + 5 + boil)) * 0.04;
                float one = frac(dot(xz + jitter, float2(0.707, 0.707)) * 7), two = frac(dot(xz + jitter, float2(0.707, -0.707)) * 7);
                half hatch = (one < 0.3 * shade) + (shade > 0.55) * (two < 0.3 * (shade - 0.45));
                return lerp(c, Shade * 2, saturate(hatch));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2 * SPAN;
                float boil = floor(_MindTime * 2.5) * 3.1;
                float angle = atan2(p.y, p.x), turn = angle / (2 * PI) + 0.5;
                // The edge wobbles like a hand-drawn circle and boils like the drawings' lines.
                float r = length(p) + (Noise(float2(turn * 9, boil)) - 0.5) * 0.06;
                half k = _Aim > 0 ? Sight(r, turn, angle, p) : 0;
                half shadow = Darken(r, i.positionWS.xz, boil);
                // Nothing at the quad's edge, so it never shows as a square.
                half fade = smoothstep(SPAN * 0.92, SPAN, length(p));
                shadow = lerp(shadow, 1, fade);
                k *= 1 - fade;
                // Blend One SrcAlpha: the sight's colour over the floor times its shadow; the floor is only ever darkened.
                return half4(Warm * k, shadow * (1 - k));
            }
            ENDHLSL
        }
    }
}
