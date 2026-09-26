Shader "Combat/Ground Contact"
{
    Properties
    {
        _Color("Tint",Color)=(0.08,0.07,0.09,0.32)
        [Enum(Shadow,0,Dust,1)] _Mode("Mode",Float)=0
    }
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
                half4 _Color; float _Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color*_Color; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float radius=length(i.uv*2-1);
                float coverage=pow(saturate(1-radius*radius),2);
                if(_Mode>0.5) coverage*=0.5+0.5*CombatNoise(i.uv*9);
                return half4(i.color.rgb,i.color.a*coverage);
            }
            ENDHLSL
        }
    }
}
