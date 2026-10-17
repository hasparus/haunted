// One full-screen pass for everything the host's mind does to the picture, so WebGL pays for a single extra blit:
// paper grain and a breathing vignette, Don't Starve's insanity wobble at the screen's edge, a chromatic split while the pills flip the controls,
// and the boss's card burning in cold blue flame, banded like ink, with heat haze around its rectangle.
// Everything runs on _MindTime (unscaled), so it keeps moving while the card holds the game paused.
Shader "Haunted/MindDistort"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "MindDistort"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set by MindFx. _CardRect is the card in uv (xMin, yMin, xMax, yMax).
            float _MindFxTime, _MindTime, _MindWobble, _MindInverted, _CardFire, _CardBurn;
            float4 _CardRect;
            // Up to four ink ripples: centre in uv (xy), age 0 to 1 (z), strength (w).
            float4 _Ripples[4];

            half3 Sample(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float t = _MindTime;

                // Signed distance to the card, in screen heights.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2(uv.x * aspect, uv.y);
                float2 lo = float2(_CardRect.x * aspect, _CardRect.y), hi = float2(_CardRect.z * aspect, _CardRect.w);
                float2 q = abs(p - (lo + hi) * 0.5) - (hi - lo) * 0.5;
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0);

                float haze = _CardFire * saturate(1 - abs(d) / 0.14);
                uv += haze * 0.005 * float2(Noise(p * 30 + float2(0, -t * 4)) - 0.5, Noise(p * 30 + float2(7, -t * 4)) - 0.5);

                // Ink ripples: a ring that pushes the picture outward and darkens it a touch, like a wet stroke.
                half rippleInk = 0;
                for (int r = 0; r < 4; r++)
                {
                    float4 rp = _Ripples[r];
                    if (rp.w <= 0) continue;
                    float2 dc = (uv - rp.xy) * float2(aspect, 1);
                    float dist = length(dc);
                    half band = saturate(1 - abs(dist - rp.z * 0.28) / 0.035) * (1 - rp.z) * rp.w;
                    uv -= dc / max(dist, 1e-4) * band * 0.018 * float2(1 / aspect, 1);
                    rippleInk = max(rippleInk, band);
                }

                half3 col = Sample(uv);
                col *= 1 - rippleInk * 0.26;

                // Don't Starve's insanity wobble: a ghost copy circling the frame, strongest at the edges.
                if (_MindWobble > 0.001)
                {
                    float2 o = float2(cos(_MindFxTime * 50 + 0.25), sin(_MindFxTime * 50)) * 0.00625;
                    half3 ghost = Sample(uv * (1 - 2 * 0.00625) + 0.00625 + o);
                    half mask = saturate((1 - distance(uv, 0.5) - 0.5) / 0.185);
                    col = lerp(lerp(ghost, col, 1 - _MindWobble), col, mask);
                }

                if (_MindInverted > 0.001)
                {
                    float2 dir = (uv - 0.5) * _MindInverted * 0.012;
                    col.r = Sample(uv + dir).r;
                    col.b = Sample(uv - dir).b;
                }

                // Flames: tongues of noise rising off the card, reaching furthest above it, cut into three hard bands
                // (navy rim, electric blue, a near-white core) like a cartoon fire, over a soft blue glow.
                float above = saturate((p.y - hi.y) / 0.15 + 0.5);
                float tongues = Fbm(float2(p.x * 16, p.y * 3 - t * 2.4));
                float reach = (0.01 + 0.13 * tongues * tongues * tongues * 2) * (0.35 + 2.2 * above);
                float flame = saturate(1 - d / reach) * step(-0.004, d) * _CardFire;
                // Burning out: the flames eat in across the card from its edges as it fades.
                float eaten = _CardBurn * 1.4 - Fbm(p * 14 + t) * 0.6;
                flame = max(flame, step(d, 0) * saturate((eaten + d * 6) * 4) * _CardFire);
                // Wobbly band edges so the layers lick at each other instead of following the outline.
                float edge = (Noise(p * 40 + float2(0, -t * 5)) - 0.5) * 0.18;
                col += half3(0.1, 0.3, 0.9) * saturate(1 - d / 0.12) * step(0, d) * 0.3 * _CardFire;
                col = lerp(col, half3(0.03, 0.08, 0.35), smoothstep(0.02, 0.05, flame));
                col = lerp(col, half3(0.15, 0.45, 1.0), smoothstep(0.45, 0.48, flame + edge));
                col = lerp(col, half3(0.82, 0.95, 1.0), smoothstep(0.82, 0.85, flame + edge));

                // Paper grain, re-rolled twelve times a second like film, and Don't Starve's breathing vignette.
                float2 pixel = floor(input.texcoord * _ScreenParams.xy / 2);
                col *= 1 + (Hash(pixel + floor(t * 12) * 17.3) - 0.5) * 0.07;
                col *= 1 - smoothstep(0.42, 0.95, distance(input.texcoord, 0.5)) * (0.28 + 0.06 * sin(t * 0.9));

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
