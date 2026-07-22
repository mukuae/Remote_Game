Shader "Custom/OutlineUnlit"
{
    // Built-in Render Pipeline shader. Renders only back faces in a flat
    // colour — used by Outline.cs for the inverted-hull outline effect.
    // If your project uses URP, let me know and I'll swap this for a
    // Shader Graph / URP-compatible version instead.

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
