// The therapist's aim and strike drawn in ink on the floor, over its soft glow. Multiplies the floor by twice its
// colour, so 0.5 leaves it, more lights it warm, less inks it. r is in radii of the light's reach: 1 is the edge that
// counts. _Aim is how far the aim is (0 to 1), _Dark how far the strike has darkened the board (0 to 1).
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
        Blend DstColor SrcColor
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
            static const half3 Lit = half3(0.86, 0.79, 0.62);
            static const half3 Ink = half3(0.08, 0.07, 0.09);
            static const half3 Shade = half3(0.1, 0.1, 0.14);
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

            half3 Over(half3 c, half3 ink, half a) { return lerp(c, ink, saturate(a)); }

            // The aim: the strike's edge sighted through crosshair ticks, a clock of ticks filling up around it.
            half3 Sight(float r, float turn, float angle, float2 p)
            {
                half3 c = r < 1 ? lerp(0.5, Lit, 0.3) : 0.5;
                c = Over(c, Ink, Line(r - 1, 0.022));
                float quarter = abs(frac(angle / (PI / 2) + 0.5) - 0.5) * (PI / 2) * r;
                c = Over(c, Ink, Line(quarter, 0.035) * (abs(r - 1) < 0.2));
                float tick = frac(turn * 12 + 0.5) - 0.5, which = floor(turn * 12 + 0.5) / 12;
                half on = which < _Aim ? 1 : 0.3;
                c = Over(c, Ink, Line(tick / 12 * 2 * PI * r, 0.03) * (r > 1.08 && r < 1.3) * on);
                half cross = Line(p.x, 0.02) * (abs(p.y) < 0.22) + Line(p.y, 0.02) * (abs(p.x) < 0.22);
                return Over(c, Ink, cross);
            }

            // The strike: the board around the light dims, cross-hatched into shadow near it.
            half3 Darken(half3 c, float r, float2 xz, float boil)
            {
                float outside = smoothstep(1, 1.2, r);
                c = lerp(c, c * Dim * 2, outside * _Dark);
                float shade = outside * (1 - smoothstep(2.6, 4, r)) * 1.3 * _Dark;
                float2 jitter = float2(Noise(xz * 3 + boil), Noise(xz * 3 + 5 + boil)) * 0.04;
                float one = frac(dot(xz + jitter, float2(0.707, 0.707)) * 7), two = frac(dot(xz + jitter, float2(0.707, -0.707)) * 7);
                half hatch = (one < 0.3 * shade) + (shade > 0.55) * (two < 0.3 * (shade - 0.45));
                return Over(c, Shade, hatch);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2 * SPAN;
                float boil = floor(_MindTime * 2.5) * 3.1;
                float angle = atan2(p.y, p.x), turn = angle / (2 * PI) + 0.5;
                // The edge wobbles like a hand-drawn circle and boils like the drawings' lines.
                float r = length(p) + (Noise(float2(turn * 9, boil)) - 0.5) * 0.06;
                half3 c = _Aim > 0 ? Sight(r, turn, angle, p) : 0.5;
                c = Darken(c, r, i.positionWS.xz, boil);
                // Nothing at the quad's edge, so it never shows as a square.
                return half4(lerp(c, 0.5, smoothstep(SPAN * 0.92, SPAN, length(p))), 1);
            }
            ENDHLSL
        }
    }
}
