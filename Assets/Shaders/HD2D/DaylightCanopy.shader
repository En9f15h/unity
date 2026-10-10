Shader "HD2D/Daylight Canopy"
{
    Properties
    {
        _SunCanopy("Overhead sunlight veil",Range(0,.5))=0
        _SunOrigin("Sun XY and ground Y",Vector)=(-3,8,-2,0) _SunColor("Light color",Color)=(1,.98,.92,1) _BeamShape("Slope width spread focus",Vector)=(.3,1.2,.23,1.6) _BeamDetail("Secondary beam and crown",Vector)=(.28,.65,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always Blend One One
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DaylightCanopy.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _SunCanopy; float4 _SunOrigin; half4 _SunColor; float4 _BeamShape; float4 _BeamDetail;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 worldXY:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.worldXY=TransformObjectToWorld(v.positionOS.xyz).xy; return o;
            }
            half4 Frag(Varyings i):SV_Target { return half4(HD2DDaylightCanopy(i.worldXY,_SunOrigin,_SunCanopy,_SunColor.rgb,_BeamShape,_BeamDetail),0); }
            ENDHLSL
        }
    }
}

