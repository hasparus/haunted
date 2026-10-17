// A demon's arm: a flat ink stroke (a LineRenderer in the drawing's plane), sorted like the drawing it belongs to
// (Sorting.hlsl): at the floor under its origin, _Aloft nearer, so a near arm sits over the body and a far arm under it.
Shader "Haunted/Limb"
{
    Properties
    {
        _BaseColor("Ink", Color) = (0.07, 0.06, 0.09, 1)
        _Aloft("Drawn this far nearer than where it stands (m)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite On
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
                float _Aloft;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 feet = Underneath(TransformObjectToWorld(float3(0, 0, 0)));
                o.positionCS.z = SortedZ(o.positionCS, PushedBack(feet, -_Aloft));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
