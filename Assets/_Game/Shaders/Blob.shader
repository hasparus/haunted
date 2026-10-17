// A soft flat ellipse of shade, drawn at the depth of the point in _Anchor (Sorting.hlsl): a thought's shadow on the
// desk top it rests on, over the desk's drawing and under the thought. Queued after the drawings: it writes no depth,
// so a drawing drawn after it would paint over it.
Shader "Haunted/Blob"
{
    Properties
    {
        _BaseColor("Shade", Color) = (0.05, 0.04, 0.08, 0.4)
        _Anchor("Where it stands (xyz)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
            #include "Sorting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _Anchor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionCS.z = SortedZ(o.positionCS, _Anchor.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2 - 1);
                return half4(_BaseColor.rgb, _BaseColor.a * (1 - smoothstep(0.6, 1, r)));
            }
            ENDHLSL
        }
    }
}
