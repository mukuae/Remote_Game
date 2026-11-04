Shader "Custom/OutlineUnlit"
{
    // Renders only back faces in a flat colour - used by Outline.cs for the
    // inverted-hull outline effect. The pass has no LightMode tag, so URP draws
    // it as "SRPDefaultUnlit". Listed under Always Included Shaders.

    Properties
    {
        _Color ("Color", Color) = (1,1,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
