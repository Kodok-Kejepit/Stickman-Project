using UnityEngine;

namespace Stickman.Core
{
    /// <summary>
    /// Cached URP materials loaded from <c>Resources/</c>. Avoids runtime <c>Shader.Find</c> and prevents
    /// magenta primitives in player builds.
    /// </summary>
    public static class RuntimeMat
    {
        static Material _lit;
        static Material _unlit;
        static Material _unlitTransparent;

        public static Material Lit => _lit != null ? _lit : (_lit = Resources.Load<Material>("RuntimeLit"));
        public static Material Unlit => _unlit != null ? _unlit : (_unlit = Resources.Load<Material>("RuntimeUnlit"));
        public static Material UnlitTransparent => _unlitTransparent != null
            ? _unlitTransparent
            : (_unlitTransparent = Resources.Load<Material>("RuntimeUnlitTransparent"));

        /// <summary>Creates a tinted URP Lit material cloned from <see cref="Lit"/>.</summary>
        public static Material TintedLit(Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            var src = Lit;
            var mat = new Material(src);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;
            return mat;
        }

        /// <summary>Creates a tinted URP Unlit material (opaque or alpha-blended).</summary>
        public static Material TintedUnlit(Color color, bool transparent = false)
        {
            var src = transparent ? UnlitTransparent : Unlit;
            if (src == null) src = Lit;
            var mat = new Material(src);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.enableInstancing = true;
            return mat;
        }

        /// <summary>Assigns a URP material to a freshly created primitive so it never renders magenta.</summary>
        public static void ApplyToPrimitive(GameObject go, Material material)
        {
            if (go == null || material == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = material;
        }
    }
}
