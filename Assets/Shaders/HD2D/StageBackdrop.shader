Shader "HD2D/Backdrop"
{
    Properties { _MainTex("Whole background",2D)="white"{} _Softness("Far background softness",Range(0,2))=.8 _Mist("Distant haze",Range(0,.15))=.035 _UVRect("Sprite UV rectangle",Vector)=(0,0,1,1) _SunCanopy("Overhead sunlight veil",Range(0,.5))=0 _SunOrigin("Sun XY and ground Y",Vector)=(-3,8,-2,0) _SunColor("Light color",Color)=(1,.98,.92,1) _BeamShape("Slope width spread focus",Vector)=(.3,1.2,.23,1.6) _BeamDetail("Secondary beam and crown",Vector)=(.28,.65,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off ZWrite On
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DaylightCanopy.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial) float _Softness; float _Mist; float4 _UVRect; float _SunCanopy; float4 _SunOrigin; half4 _SunColor; float4 _BeamShape; float4 _BeamDetail; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 worldXY:TEXCOORD1; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=lerp(_UVRect.xy,_UVRect.zw,v.uv); o.worldXY=TransformObjectToWorld(v.positionOS.xyz).xy; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 d=_MainTex_TexelSize.xy*_Softness*1.5;
                half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb*.4;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(d.x,0)).rgb*.15;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(d.x,0)).rgb*.15;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,d.y)).rgb*.15;
                c+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,d.y)).rgb*.15;
                // Keep the original image's exposure; haze gently lifts distant dark detail.
                c=lerp(c,half3(.32,.38,.45),_Mist*smoothstep(.2,.85,i.uv.y));
                c+=HD2DDaylightCanopy(i.worldXY,_SunOrigin,_SunCanopy,_SunColor.rgb,_BeamShape,_BeamDetail);
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}

