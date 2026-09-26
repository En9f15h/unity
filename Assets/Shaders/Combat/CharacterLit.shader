Shader "Combat/Character Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _MaskTex("2D Light Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HDR] _RimColor("Edge Light", Color) = (0.42,0.64,0.82,1)
        _RimStrength("Edge Strength", Range(0,0.5)) = 0.08
        [HDR] _PhaseColor("Phase Edge", Color) = (0.3,1.6,2.0,1)
        _PhaseWidth("Phase Edge Width", Range(0.005,0.15)) = 0.045
        [HideInInspector] _HitAmount("Hit", Float) = 0
        [HideInInspector] _HitColor("Hit Color", Color) = (1.7,1.3,0.9,1)
        [HideInInspector] _Dissolve("Phase", Float) = 0
        [HideInInspector] _CharacterHeight("Height", Float) = 1
        [HideInInspector] _SpriteUVRect("Sprite UV", Vector) = (0,0,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "CombatNoise.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _RimColor, _PhaseColor, _HitColor;
            float4 _MainTex_TexelSize, _SpriteUVRect;
            float4x4 _CharacterWorldToLocal;
            float _RimStrength, _PhaseWidth, _HitAmount, _Dissolve, _CharacterHeight;
        CBUFFER_END
        float PhaseField(float3 worldPosition)
        {
            float2 p = mul(_CharacterWorldToLocal, float4(worldPosition,1)).xy / max(_CharacterHeight,0.01);
            return CombatNoise(p * 24.0) * 0.7 + CombatNoise(p * 51.0) * 0.3;
        }
        float PhaseCoverage(float field)
        {
            // Endpoints are exact: 0 is fully visible, 1 is fully hidden.
            float threshold = lerp(-0.08,1.08,saturate(_Dissolve));
            return smoothstep(threshold, threshold + 0.035, field);
        }
        ENDHLSL
        Pass
        {
            Name "Character2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color:COLOR; float3 combatWorld:TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.combatWorld = TransformObjectToWorld(input.positionOS);
                return o;
            }
            half ReadAlpha(float2 uv)
            {
                float2 inside = step(_SpriteUVRect.xy,uv) * step(uv,_SpriteUVRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a * inside.x * inside.y;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 lit = CommonLitFragment(input,input.color);
                float field = PhaseField(input.combatWorld);
                float coverage = PhaseCoverage(field);
                float threshold = lerp(-0.08,1.08,saturate(_Dissolve));
                float edge = (1.0-smoothstep(threshold,threshold+_PhaseWidth,field)) * coverage;
                edge *= step(0.001,_Dissolve) * step(_Dissolve,0.999);
                float2 texel = _MainTex_TexelSize.xy;
                float neighbors = min(min(ReadAlpha(input.uv+float2(texel.x,0)), ReadAlpha(input.uv-float2(texel.x,0))),
                                      min(ReadAlpha(input.uv+float2(0,texel.y)), ReadAlpha(input.uv-float2(0,texel.y))));
                float rim = saturate(ReadAlpha(input.uv)-neighbors);
                lit.rgb = lerp(lit.rgb,_HitColor.rgb,saturate(_HitAmount));
                lit.rgb += _RimColor.rgb * rim * _RimStrength + _PhaseColor.rgb * edge;
                lit.a *= coverage;
                return lit;
            }
            ENDHLSL
        }
        Pass
        {
            Name "CharacterNormals"
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex VertNormals
            #pragma fragment FragNormals
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color:COLOR; float3 combatWorld:TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            Varyings VertNormals(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonNormalsVertex(input);
                o.color=input.color*_Color*unity_SpriteColor;
                o.combatWorld=TransformObjectToWorld(input.positionOS);
                return o;
            }
            half4 FragNormals(Varyings input):SV_Target
            {
                half4 result=CommonNormalsFragment(input,input.color);
                result.a*=PhaseCoverage(PhaseField(input.combatWorld));
                return result;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
