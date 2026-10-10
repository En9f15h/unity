Shader "HD2D/Stage Stone"
{
    Properties
    {
        _BaseColor("Stone color",Color)=(.53,.49,.40,1)
        _Ambient("Ambient floor",Range(0,1))=.38
        _Emission("Emission",Color)=(0,0,0,1)
        _TileScale("Stone course scale",Float)=1.3
        _BackdropTex("Existing stage detail",2D)="white"{}
        _ProjectBackdrop("Project existing floor detail",Float)=0
        _ProjectionRect("Stage bounds",Vector)=(-9.6,-5.44,9.6,5.44)
        _BackdropUV("Stage sprite UV",Vector)=(0,0,1,1)
        [HideInInspector] _BaseMap("Base map",2D)="white"{}
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _Emission; float _Ambient; float _TileScale;
                float _ProjectBackdrop; float4 _ProjectionRect; float4 _BackdropUV;
            CBUFFER_END
            TEXTURE2D(_BackdropTex); SAMPLER(sampler_BackdropTex);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes v) { Varyings o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                float2 uv=abs(n.y)>.65?i.positionWS.xz:i.positionWS.xy;
                uv*=max(.1,_TileScale); uv.x+=fmod(floor(uv.y),2)*.5;
                float2 grid=frac(uv); float2 edge=min(grid,1-grid);
                float seam=1-smoothstep(.008,.025,min(edge.x,edge.y));
                float grain=frac(sin(dot(floor(uv),float2(12.9898,78.233)))*43758.5453);
                half3 albedo=_BaseColor.rgb*(.91+.12*grain)*(1-.18*seam);
                float2 stageUV=saturate((i.positionWS.xy-_ProjectionRect.xy)/max(.001,_ProjectionRect.zw-_ProjectionRect.xy));
                albedo=lerp(albedo,SAMPLE_TEXTURE2D(_BackdropTex,sampler_BackdropTex,lerp(_BackdropUV.xy,_BackdropUV.zw,stageUV)).rgb,_ProjectBackdrop);
                Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lighting=half3(.88,.94,1)*_Ambient;
                lighting+=main.color*saturate(dot(n,main.direction))*main.shadowAttenuation;
                #if defined(_ADDITIONAL_LIGHTS)
                uint count=GetAdditionalLightsCount();
                for(uint index=0;index<count;index++) { Light l=GetAdditionalLight(index,i.positionWS); lighting+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation*l.shadowAttenuation; }
                #endif
                return half4(albedo*lighting+_Emission.rgb,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
