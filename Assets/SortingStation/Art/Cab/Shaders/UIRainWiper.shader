Shader "SortingStation/UI/RainWiper"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WiperStrength ("Wiper Strength", Range(0,1)) = 0
        _WiperRadiusScale ("Wiper Radius Scale", Range(1,3)) = 1
        _WiperCenterOverlap ("Wiper Center Overlap", Range(0,75)) = 55
        _ProceduralRain ("Procedural Rain", Range(0,1)) = 0
        _TileRect ("Tile Rect", Vector) = (-0.04,-0.04,1.04,1.04)
        _RectSize ("Rect Size", Vector) = (1000,500,0,0)
        _ViewportSize ("Viewport Size", Vector) = (1000,500,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "RainWiper"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _WiperStrength;
            float _WiperRadiusScale;
            float _WiperCenterOverlap;
            float _ProceduralRain;
            float4 _TileRect;
            float4 _RectSize;
            float4 _ViewportSize;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float SectorMask(float2 pixelPosition, float2 basePoint, float minAngle, float maxAngle,
                float viewportHeight, float radiusScale)
            {
                float2 delta = pixelPosition - basePoint;
                float radius = length(delta);
                float angle = atan2(-delta.x, delta.y) * 57.2957795;
                float radialIn = smoothstep(viewportHeight * 0.018, viewportHeight * 0.050, radius);
                float radialOut = 1.0 - smoothstep(viewportHeight * 0.36 * radiusScale,
                    viewportHeight * 0.40 * radiusScale, radius);
                float angularIn = smoothstep(minAngle - 6.0, minAngle + 3.0, angle);
                float angularOut = 1.0 - smoothstep(maxAngle - 3.0, maxAngle + 6.0, angle);
                return radialIn * radialOut * angularIn * angularOut;
            }

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            float RainStreak(float2 viewportUv)
            {
                float2 rainUv = float2(viewportUv.x * 19.0 + viewportUv.y * 2.3,
                    viewportUv.y * 8.0 + _Time.y * 1.45);
                float column = floor(rainUv.x);
                float randomOffset = Hash11(column);
                float across = abs(frac(rainUv.x) - randomOffset);
                float along = frac(rainUv.y + randomOffset * 5.0);
                float narrow = 1.0 - smoothstep(0.025, 0.075, across);
                float trail = smoothstep(0.48, 0.82, along);
                return narrow * trail;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                float2 safeRectSize = max(_RectSize.xy, float2(1.0, 1.0));
                float2 localUv = input.localPosition.xy / safeRectSize + 0.5;
                float2 viewportUv = lerp(_TileRect.xy, _TileRect.zw, localUv);
                float2 viewportSize = max(_ViewportSize.xy, float2(1.0, 1.0));
                float2 pixelPosition = viewportUv * viewportSize;
                float rain = RainStreak(viewportUv) * saturate(_ProceduralRain) * saturate(input.color.a * 5.0);
                color.rgb = lerp(color.rgb, float3(0.82, 0.93, 1.0), rain * 0.72);
                color.a = max(color.a, rain * 0.46);
                // Keep these pivots in sync with CabRideController.BuildWipers. The previous
                // bottom-edge pivots cleared an area below the visible blades.
                float2 leftBase = float2(0.410, 0.455) * viewportSize;
                float2 rightBase = float2(0.590, 0.455) * viewportSize;
                float clean = max(
                    SectorMask(pixelPosition, leftBase, -_WiperCenterOverlap, 106.0, viewportSize.y, _WiperRadiusScale),
                    SectorMask(pixelPosition, rightBase, -106.0, _WiperCenterOverlap, viewportSize.y, _WiperRadiusScale));
                color.a *= 1.0 - clean * saturate(_WiperStrength) * 0.97;
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
