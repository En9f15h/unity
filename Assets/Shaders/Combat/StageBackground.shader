Shader "Combat/Stage Background"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _MaskTex("2D Light Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _Color("Tint", Color) = (1,1,1,1)
        _StageGrade("Brightness / Contrast / Saturation", Vector) = (1,1,1,0)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "StageGrade.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _StageGrade;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color = CommonLitFragment(input, input.color);
                color.rgb = CombatStageGrade(color.rgb, _StageGrade);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_NORMALS_INPUTS half4 color:COLOR; };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target { return CommonNormalsFragment(input, input.color); }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
