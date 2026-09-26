Shader "Combat/Stage Atmosphere"
{
    Properties { _MistColor("Mist",Color)=(0.45,0.55,0.65,1) _Density("Density",Range(0,0.3))=0.075 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CombatNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _MistColor; float _Density;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                float band=smoothstep(0.02,0.12,uv.y)*(1-smoothstep(0.26,0.55,uv.y));
                float sides=smoothstep(0,0.08,uv.x)*(1-smoothstep(0.92,1,uv.x));
                float noise=CombatNoise(uv*float2(9,6)+float2(_CombatVisualTime*0.025,0));
                noise=lerp(noise,CombatNoise(uv*float2(19,12)-float2(_CombatVisualTime*0.015,0)),0.25);
                return half4(_MistColor.rgb,band*sides*(0.25+0.75*noise)*_Density*_MistColor.a);
            }
            ENDHLSL
        }
    }
}
