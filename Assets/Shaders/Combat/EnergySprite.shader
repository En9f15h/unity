Shader "Combat/Energy Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        [HDR] _GlowColor("Glow",Color)=(0.35,0.75,1,1)
        _Intensity("Intensity",Range(0,4))=1.35
        _Opacity("Opacity",Range(0,1))=1
        _GlowStrength("Glow Strength",Range(0,2))=0.35
        _FlowStrength("Energy Flow",Range(0,0.5))=0.12
        _FlowSpeed("Flow Speed",Float)=1
        _Additive("Additive Contribution",Range(0,1))=0.65
        [Enum(Energy,0,Ward,1,Rune,2,Ghost,3)] _Mode("Effect",Float)=0
        _WardCenterOpacity("Ward Center Opacity",Range(0,1))=0.18
        _WardRimStrength("Ward Rim Light",Range(0,1))=0.35
        [HideInInspector] _SpriteUVRect("Sprite UV",Vector)=(0,0,1,1)
        [HideInInspector] _EffectProgress("Progress",Float)=0
        [HideInInspector] _ImpactStart("Impact Start",Float)=-10000
        [HideInInspector] _ImpactUV("Impact UV",Vector)=(0.5,0.5,0,0)
        [HideInInspector] _RendererColor("Renderer Color",Color)=(1,1,1,1)
        [HideInInspector] _MeshSprite("Baked Sprite Mesh",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "CombatNoise.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color,_GlowColor;
                float4 _SpriteUVRect,_ImpactUV;
                float _Intensity,_Opacity,_GlowStrength,_FlowStrength,_FlowSpeed,_Additive,_Mode,_EffectProgress,_ImpactStart,_MeshSprite;
                float _WardCenterOpacity,_WardRimStrength;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                if (_MeshSprite<0.5) input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonUnlitVertex(input);
                o.color=input.color*_Color;
                if (_MeshSprite<0.5) o.color*=unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 source=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                float2 uv=(input.uv-_SpriteUVRect.xy)/max(_SpriteUVRect.zw-_SpriteUVRect.xy,0.00001);
                float clock=_CombatVisualTime*_FlowSpeed;
                float flow=CombatNoise(uv*8+float2(clock*0.4,-clock*0.6));
                half alpha=source.a*input.color.a*_Opacity;
                half3 rgb=source.rgb*input.color.rgb*_Intensity*(1+_FlowStrength*(flow-0.5));
                float luminance=max(source.r,max(source.g,source.b));
                rgb+=_GlowColor.rgb*_GlowStrength*luminance*luminance;
                if (_Mode>0.5 && _Mode<1.5)
                {
                    float radius=length((uv-0.5)*2);
                    float rim=smoothstep(0.35,0.9,radius);
                    float age=_CombatVisualTime-_ImpactStart;
                    float ring=exp(-pow((length(uv-_ImpactUV.xy)-age*1.7)*24,2));
                    float impact=ring*saturate(1-age/0.55)*step(0,age);
                    alpha*=saturate(lerp(_WardCenterOpacity,1,rim)+impact*0.28);
                    rgb+=_GlowColor.rgb*(impact*1.5+rim*_WardRimStrength+0.035*sin(uv.y*32-clock*2));
                }
                else if (_Mode>1.5 && _Mode<2.5)
                {
                    float angle=atan2(uv.y-0.5,uv.x-0.5);
                    float sweep=pow(saturate(0.5+0.5*cos(angle-clock*1.8)),10);
                    rgb+=_GlowColor.rgb*luminance*(sweep*0.3+0.18*_EffectProgress);
                }
                else if (_Mode>2.5)
                {
                    float threshold=lerp(-0.1,1.1,_EffectProgress);
                    float coverage=smoothstep(threshold,threshold+0.06,flow);
                    alpha*=coverage;
                    rgb=lerp(rgb,_GlowColor.rgb,0.65);
                }
                return half4(max(rgb,0)*alpha,alpha*(1-_Additive));
            }
            ENDHLSL
        }
    }
}
