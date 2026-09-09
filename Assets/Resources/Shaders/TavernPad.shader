Shader "FoodIsekaiZ/Floor/Tavern Pad"
{
    Properties
    {
        _BaseColor ("Panel tint", Color) = (1,1,1,1)
        _CenterColor ("Panel center", Color) = (0.24,0.075,0.085,1)
        _EdgeColor ("Panel edge", Color) = (0.11,0.025,0.03,1)
        _WoodColor ("Carved wood", Color) = (0.18,0.075,0.045,1)
        _GoldColor ("Brass inlay", Color) = (0.76,0.47,0.20,1)
        _LightColor ("Ivory highlight", Color) = (1,0.85,0.57,1)
        _AlertColor ("Low time glow", Color) = (1,0.24,0.08,1)
        _Aspect ("Width / height", Float) = 1.5714286
        _Style ("Guest 0 / Kitchen 1 / Bank 2", Float) = 0
        _WarningAmount ("Runtime warning", Range(0,1)) = 0
        _Visibility ("Runtime visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TavernPad"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CenterColor;
                float4 _EdgeColor;
                float4 _WoodColor;
                float4 _GoldColor;
                float4 _LightColor;
                float4 _AlertColor;
                float _Aspect;
                float _Style;
                float _WarningAmount;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Distances keep the brass border equally thick along both axes.
            float PanelDistance(float2 position, float2 halfSize, float corner)
            {
                float2 q = abs(position) - halfSize;
                return max(max(q.x, q.y), (q.x + q.y + corner) * 0.70710678);
            }

            float Coverage(float distance, float softness)
            {
                return 1.0 - smoothstep(-softness, softness, distance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1);
                float2 halfSize = float2(_Aspect * 0.5 - 0.018, 0.477);
                float aa = max(fwidth(p.y), 0.0005);
                float outer = Coverage(PanelDistance(p, halfSize, 0.095), aa);
                float brass = Coverage(PanelDistance(p, halfSize - 0.014, 0.09), aa);
                float wood = Coverage(PanelDistance(p, halfSize - 0.034, 0.08), aa);
                float innerBrass = Coverage(PanelDistance(p, halfSize - 0.076, 0.065), aa);
                float panel = Coverage(PanelDistance(p, halfSize - 0.085, 0.06), aa);

                float3 gold = lerp(_GoldColor.rgb, _LightColor.rgb, saturate(input.uv.y * 0.75));
                float3 result = _WoodColor.rgb * 0.30;
                result = lerp(result, gold, brass);
                float grain = 0.06 * sin(p.x * 76 + 2 * sin(p.y * 8))
                    + 0.035 * sin(p.x * 211 + p.y * 19);
                result = lerp(result, _WoodColor.rgb * (1 + grain), wood);
                result = lerp(result, gold * 0.85, innerBrass);
                float vignette = saturate(1 - length(p / halfSize) * 0.7);
                float3 center = lerp(_EdgeColor.rgb, _CenterColor.rgb, vignette);
                center *= 1 + 0.018 * sin(p.y * 133 + sin(p.x * 42));
                result = lerp(result, center, panel);

                // A separate parchment plaque gives each menu name generous breathing room.
                float kitchen = step(0.5, _Style);
                float2 plaquePosition = p + float2(0, 0.270);
                float plaqueBorder = Coverage(PanelDistance(plaquePosition,
                    float2(halfSize.x, 0.108), 0.025), aa) * kitchen * panel;
                float plaque = Coverage(PanelDistance(plaquePosition,
                    float2(halfSize.x, 0.097), 0.022), aa) * kitchen * panel;
                float3 parchment = lerp(_LightColor.rgb, float3(1, 0.97, 0.87), 0.65);
                parchment *= 0.98 + 0.02 * saturate((plaquePosition.y + 0.1) * 5);
                result = lerp(result, _WoodColor.rgb * 0.6, plaqueBorder);
                result = lerp(result, parchment, plaque);

                // Corner rivets and cut brass diamonds read clearly from above.
                float2 cornerPoint = abs(p) - (halfSize - float2(0.063, 0.063));
                float rivet = Coverage(length(cornerPoint) - 0.018, aa);
                float rivetHighlight = Coverage(length(cornerPoint - float2(-0.004,0.004)) - 0.007, aa);
                result = lerp(result, _GoldColor.rgb * 0.72, rivet);
                result = lerp(result, _LightColor.rgb, rivetHighlight);
                float diamond = Coverage(abs(p.x) + abs(p.y - 0.347) - 0.034, aa);
                result = lerp(result, gold, diamond * (1 - kitchen));
                float flourish = Coverage(abs(p.y - 0.347) - 0.0025, aa)
                    * (1 - smoothstep(0.23, 0.29, abs(p.x)))
                    * smoothstep(0.065, 0.085, abs(p.x));
                result = lerp(result, gold, flourish * (1 - kitchen));
                float lowerDiamond = Coverage(abs(p.x) + abs(p.y + 0.318) - 0.018, aa);
                result = lerp(result, gold * 0.8, lowerDiamond * (1 - kitchen));

                // An engraved medallion and paired laurel sprigs frame each table's Roman seal.
                float seal = Coverage(abs(length(p) - 0.265) - 0.0025, aa);
                float innerSeal = Coverage(abs(length(p) - 0.247) - 0.0015, aa);
                float stemX = 0.325 + 0.055 * (1 - pow(p.y / 0.24, 2));
                float stems = Coverage(abs(abs(p.x) - stemX) - 0.002, aa)
                    * (1 - smoothstep(0.20, 0.23, abs(p.y)));
                float leaves = 0;
                for (int leafIndex = 0; leafIndex < 5; leafIndex++)
                {
                    float leafY = -0.17 + leafIndex * 0.078;
                    float leafX = 0.333 + 0.055 * (1 - pow(leafY / 0.24, 2));
                    float2 leafPosition = float2(abs(p.x) - leafX, p.y - leafY);
                    float2 leafAxes = float2(leafPosition.x + leafPosition.y * 0.55,
                        leafPosition.y - leafPosition.x * 0.55);
                    float leafDistance = length(leafAxes / float2(0.057, 0.019)) - 1;
                    leaves = max(leaves, Coverage(leafDistance, aa / 0.019));
                }

                float engraving = max(max(seal * 0.55, innerSeal * 0.24), max(stems, leaves) * 0.8);
                result = lerp(result, gold, engraving * (1 - kitchen));

                // Gameplay supplies state only; the scene material owns alert styling.
                result = lerp(result, _AlertColor.rgb, _WarningAmount * (0.18 * panel + 0.75 * brass * (1 - wood)));
                return half4(result * _BaseColor.rgb, outer * _BaseColor.a * _Visibility);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
