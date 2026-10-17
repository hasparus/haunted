// The therapist's light on the floor, drawn in ink (Balance.spotStyle 1-4). Multiplies the floor by twice its colour,
// so 0.5 leaves it, more lights it warm, less inks it. r is in radii of the light's reach: 1 is the edge that counts.
// _Phase is Spotlight.Phase (0 rest, 1 hunt, 2 aim, 3 strike), _Aim how far the aim is (0 to 1).
Shader "Haunted/SpotInk"
{
    Properties
    {
        _Style("Style (1 ink pool, 2 focus lines, 3 hatched dark, 4 reticle)", Float) = 1
        _Phase("Phase", Float) = 1
        _Aim("Aim", Range(0, 1)) = 0
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
                float _Style, _Phase, _Aim;
            CBUFFER_END
            float _MindTime;

            // The quad spans this many radii from its middle to its edge.
            #define SPAN 4.0
            static const half3 Lit = half3(0.86, 0.79, 0.62);
            static const half3 Flash = half3(1.0, 0.96, 0.86);
            static const half3 Ink = half3(0.08, 0.07, 0.09);
            static const half3 Shade = half3(0.24, 0.24, 0.3);

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

            // Ink-cut pool: a flat warm pool cut out with a boiling ink edge. Aim: a dashed edge and a ring closing in.
            half3 InkPool(float r, float turn)
            {
                half3 c = 0.5;
                if (_Phase == 3) return Over(r < 1 ? Flash : c, Ink, Line(r - 1, 0.07));
                if (_Phase == 2)
                {
                    c = r < 1 ? lerp(0.5, Lit, 0.3) : c;
                    half dash = frac(turn * 18 + _Time.y * 0.4) < 0.55;
                    c = Over(c, Ink, Line(r - 1, 0.045) * dash);
                    return Over(c, Ink, Line(r - lerp(1.9, 1, _Aim), 0.03));
                }
                return Over(r < 1 ? Lit : c, Ink, Line(r - 1, 0.035));
            }

            // Focus lines: the pool ringed by manga speed lines. Aim: the lines close in. Strike: they burst outward.
            half3 FocusLines(float r, float turn, float boil)
            {
                bool strike = _Phase == 3, aim = _Phase == 2;
                half3 c = r < 1 ? (strike ? Flash : aim ? lerp(0.5, Lit, 0.3) : Lit) : 0.5;
                float count = aim ? 56 : 40;
                float id = floor(turn * count), across = frac(turn * count) - 0.5;
                float from = strike ? 1.05 : aim ? lerp(1.9, 1.02, _Aim) : 1.08;
                float length = (strike ? 1.4 : 0.6) + 0.5 * Hash(float2(id, boil));
                float along = (r - from) / length;
                half stroke = along > 0 && along < 1 ? Line(across, (strike ? 0.22 : 0.16) * (1 - along)) : 0;
                return Over(c, Ink, stroke * (Hash(float2(id, 7)) > 0.2));
            }

            // Hatched dark: the room around the light cross-hatched into shadow. Aim: the target hatched, then ringed.
            half3 HatchedDark(float r, float2 xz, float boil)
            {
                float shade = smoothstep(1, 1.35, r) * (1 - smoothstep(2.6, SPAN, r)) * (_Phase == 3 ? 1.3 : 1);
                float2 jitter = float2(Noise(xz * 3 + boil), Noise(xz * 3 + 5 + boil)) * 0.04;
                float one = frac(dot(xz + jitter, float2(0.707, 0.707)) * 7), two = frac(dot(xz + jitter, float2(0.707, -0.707)) * 7);
                half hatch = (one < 0.3 * shade) + (shade > 0.55) * (two < 0.3 * (shade - 0.45));
                half3 c = r < 1 ? (_Phase == 3 ? Flash : _Phase == 2 ? half3(0.5, 0.5, 0.5) : Lit) : 0.5;
                c = Over(c, Shade, saturate(hatch));
                if (_Phase != 2) return c;
                c = Over(c, Ink, (r < 1) * (one < 0.22 * _Aim));
                return Over(c, Ink, Line(r - 1, 0.035));
            }

            // Reticle: the pool sighted through crosshair ticks. Aim: a clock of ticks filling up. Strike: a double ring.
            half3 Reticle(float r, float turn, float angle, float2 p)
            {
                half3 c = r < 1 ? (_Phase == 3 ? Flash : _Phase == 2 ? lerp(0.5, Lit, 0.3) : Lit) : 0.5;
                if (_Phase == 3) return Over(Over(c, Ink, Line(r - 1, 0.06)), Ink, Line(r - 1.14, 0.04));
                c = Over(c, Ink, Line(r - 1, 0.022));
                float quarter = abs(frac(angle / (PI / 2) + 0.5) - 0.5) * (PI / 2) * r;
                c = Over(c, Ink, Line(quarter, 0.035) * (abs(r - 1) < 0.2));
                if (_Phase != 2) return c;
                float tick = frac(turn * 12 + 0.5) - 0.5, which = floor(turn * 12 + 0.5) / 12;
                half on = which < _Aim ? 1 : 0.3;
                c = Over(c, Ink, Line(tick / 12 * 2 * PI * r, 0.03) * (r > 1.08 && r < 1.3) * on);
                half cross = Line(p.x, 0.02) * (abs(p.y) < 0.22) + Line(p.y, 0.02) * (abs(p.x) < 0.22);
                return Over(c, Ink, cross);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2 * SPAN;
                float boil = floor(_MindTime * 2.5) * 3.1;
                float angle = atan2(p.y, p.x), turn = angle / (2 * PI) + 0.5;
                // The edge wobbles like a hand-drawn circle and boils like the drawings' lines.
                float r = length(p) + (Noise(float2(turn * 9, boil)) - 0.5) * 0.06;
                half3 c = _Style < 1.5 ? InkPool(r, turn)
                    : _Style < 2.5 ? FocusLines(r, turn, boil)
                    : _Style < 3.5 ? HatchedDark(r, i.positionWS.xz, boil)
                    : Reticle(r, turn, angle, p);
                // Nothing at the quad's edge, so it never shows as a square.
                return half4(lerp(c, 0.5, smoothstep(SPAN * 0.92, SPAN, length(p))), 1);
            }
            ENDHLSL
        }
    }
}
