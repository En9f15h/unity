Shader "HD2D/Character Pose Shadow"
{
    Properties
    {
        _MainTex("Original sprite alpha",2D)="white"{}
        _ShadowColor("Shadow",Color)=(.045,.05,.07,.24)
        _UVRect("Atlas bounds",Vector)=(0,0,1,1)
        _Softness("Edge softness",Float)=.65
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize,_UVRect;half4 _ShadowColor;float _Softness;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
            half Alpha(float2 uv)
            {
                float2 inside=step(_UVRect.xy,uv)*step(uv,_UVRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,clamp(uv,_UVRect.xy,_UVRect.zw)).a*inside.x*inside.y;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 d=_MainTex_TexelSize.xy*_Softness;
                half alpha=Alpha(input.uv)*.5+(Alpha(input.uv+float2(d.x,0))+Alpha(input.uv-float2(d.x,0))+
                    Alpha(input.uv+float2(0,d.y))+Alpha(input.uv-float2(0,d.y)))*.125;
                return half4(_ShadowColor.rgb,alpha*_ShadowColor.a);
            }
            ENDHLSL
        }
    }
}
