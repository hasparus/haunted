// One full-screen pass for everything the host's mind does to the picture, so WebGL pays for a single extra blit:
// Don't Starve's insanity wobble at the screen's edge, a chromatic split while the pills flip the controls,
// and the boss's card burning: flames and heat haze licking up around its rectangle.
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
            float _MindFxTime, _MindWobble, _MindInverted, _CardFire;
            float4 _CardRect;

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
                float t = _Time.y;

                // Signed distance to the card, in screen heights.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2(uv.x * aspect, uv.y);
                float2 lo = float2(_CardRect.x * aspect, _CardRect.y), hi = float2(_CardRect.z * aspect, _CardRect.w);
                float2 q = abs(p - (lo + hi) * 0.5) - (hi - lo) * 0.5;
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0);

                float haze = _CardFire * saturate(1 - abs(d) / 0.14);
                uv += haze * 0.005 * float2(Noise(p * 30 + float2(0, -t * 4)) - 0.5, Noise(p * 30 + float2(7, -t * 4)) - 0.5);

                half3 col = Sample(uv);

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

                // Flames: tongues of noise rising off the card, reaching furthest above it.
                float above = saturate((p.y - hi.y) / 0.15 + 0.5);
                float tongues = Fbm(float2(p.x * 9, p.y * 4 - t * 2.2));
                float reach = (0.015 + 0.16 * tongues * tongues) * (0.4 + 2.4 * above);
                float flame = saturate(1 - d / reach) * step(-0.004, d) * _CardFire;
                flame = smoothstep(0, 1, flame);
                half3 fire = lerp(half3(0.55, 0.06, 0.02), half3(1, 0.82, 0.35), saturate(flame * 1.5));
                col += fire * flame * 1.6;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
