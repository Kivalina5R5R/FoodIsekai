Shader "Hidden/FoodIsekaiZ/BackgroundScenePreview"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct FragmentInput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 cameraPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };
            sampler2D _MainTex;
            float4 _TextureSampleAdd;
            float4x4 _BackgroundCameraProjection;

            FragmentInput vert(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.cameraPosition = mul(_BackgroundCameraProjection, mul(unity_ObjectToWorld, input.position));
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(FragmentInput input) : SV_Target
            {
                // Only the editor preview uses this material; crop to the actual source camera viewport.
                clip(input.cameraPosition.w);
                clip(input.cameraPosition.w - abs(input.cameraPosition.xy));
                return (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
            }
            ENDCG
        }
    }
}
