// rts/src/Game/Assets/Resources/Shaders/RtsVertexColorUnlit.shader
// Unlit shader that outputs the mesh's per-vertex colour multiplied by a material tint. Used by TerrainView (one generated mesh
// whose tiles are coloured per vertex). Part of Rts.Game (Unity side, presentation only). It lives under Resources so player builds
// include it and RtsMaterials can load it by path; it writes depth (so plateaus and cliffs sort correctly) and draws both faces.
// Works in the Built-in Render Pipeline and in URP (a pass without a LightMode tag is drawn by URP as an unlit pass).
Shader "Rts/VertexColorUnlit"
{
    Properties
    {
        // Tint multiplied into every vertex colour. Unit: RGBA, each channel 0 to 1. Range: white (default) leaves vertex colours unchanged.
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            // Material tint, linear space once Unity has converted the Color property. Unit: RGBA, each channel 0 to 1.
            float4 _Color;

            // Per-vertex input: object-space position and the mesh colour (gamma-encoded, 0 to 1 per channel; white when the mesh has none).
            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
            };

            // Vertex to fragment: clip-space position and the interpolated vertex colour.
            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR0;
            };

            // Transforms the vertex to clip space and passes the vertex colour on.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                return output;
            }

            // Converts the gamma-encoded vertex colour to linear when the project uses linear colour space, then applies the tint.
            float4 Frag(Varyings input) : SV_Target
            {
                // Vertex colour of this pixel. Unit: RGBA, each channel 0 to 1.
                float4 colour = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                colour.rgb = GammaToLinearSpace(colour.rgb);
                #endif
                return colour * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
