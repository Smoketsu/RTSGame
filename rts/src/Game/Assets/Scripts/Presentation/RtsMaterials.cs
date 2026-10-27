// rts/src/Game/Assets/Scripts/Presentation/RtsMaterials.cs
// Creates the vertex-colour material that TerrainView draws with, choosing the best shader that exists in this project.
// Part of Rts.Game (Unity side, presentation only); nothing here touches the simulation.
//
// Shader fallback chain (first one Unity can find wins; each step is tried only if the previous is missing):
//   1. "Rts/VertexColorUnlit"   our own shader, Assets/Resources/Shaders/RtsVertexColorUnlit.shader, loaded by path so builds include it.
//                               Writes depth, so terrain layers sort correctly from any angle.
//   2. "Sprites/Default"        built in, in the always-included list, honours vertex colour. It does not write depth, so the terrain
//                               mesh is generated far rows first (painter's order) to stay correct for the fixed camera.
//   3. "Universal Render Pipeline/Particles/Unlit"   URP only; honours vertex colour.
//   4. "Universal Render Pipeline/Unlit"             URP only; IGNORES vertex colour, so the terrain would be one flat colour (warning logged).
//   5. "Unlit/Color"                                 ignores vertex colour as well (warning logged).
//   6. "Standard"                                    last resort, lit and ignores vertex colour (warning logged).
using UnityEngine;

namespace Rts.Game.Presentation
{
    /// <summary>Factory for presentation materials. Static and stateless; the caller owns (and must Destroy) what it creates.</summary>
    public static class RtsMaterials
    {
        /// <summary>Path of our shader below a Resources folder, without extension. Unit: Resources path.</summary>
        private const string VertexColorShaderResource = "Shaders/RtsVertexColorUnlit";

        /// <summary>Fallback shader names in order of preference (steps 2 to 6 of the chain above). Unit: shader names as Shader.Find takes them.</summary>
        private static readonly string[] FallbackShaderNames =
        {
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Standard",
        };

        /// <summary>
        /// Creates a new material that shows per-vertex colours without lighting.
        /// </summary>
        /// <param name="materialName">Name for the material in the profiler and Inspector. Range: any text.</param>
        /// <returns>A new material, or null if no shader of the chain exists (an error is logged). The caller must Destroy it.</returns>
        public static Material CreateVertexColorMaterial(string materialName)
        {
            // Shader chosen from the chain; null until one is found.
            Shader shader = Resources.Load<Shader>(VertexColorShaderResource);
            if (shader != null && !shader.isSupported)
            {
                // Our shader failed to compile or is unsupported on this platform; use the chain instead.
                shader = null;
            }
            // True when the chosen shader draws vertex colours; false means the terrain will look flat.
            bool honoursVertexColour = shader != null;
            if (shader == null)
            {
                for (int i = 0; i < FallbackShaderNames.Length && shader == null; i++)
                {
                    shader = Shader.Find(FallbackShaderNames[i]);
                    if (shader != null && !shader.isSupported)
                    {
                        shader = null;
                    }
                    // Entries 0 and 1 of the fallback list (Sprites/Default and URP Particles/Unlit) honour vertex colour.
                    honoursVertexColour = i <= 1;
                }
            }
            if (shader == null)
            {
                Debug.LogError("[RtsMaterials] No usable shader found (tried " + VertexColorShaderResource + " and " + string.Join(", ", FallbackShaderNames) + ").");
                return null;
            }
            if (!honoursVertexColour)
            {
                Debug.LogWarning("[RtsMaterials] Shader '" + shader.name + "' ignores vertex colours, so the terrain will be drawn in one colour. Check that Assets/Resources/Shaders/RtsVertexColorUnlit.shader compiled (see the Console).");
            }
            Material material = new Material(shader);
            material.name = materialName;
            return material;
        }
    }
}
