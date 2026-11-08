using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace City.Runtime
{
    /// <summary>
    /// Матеріали для превʼю. Головна причина існування цього файлу: URP/Lit за замовчуванням
    /// НЕПРОЗОРИЙ, і просто виставити альфу в кольорі недостатньо. Прозорість у URP це не колір,
    /// а режим поверхні плюс режим змішування плюс вимкнений запис у буфер глибини.
    /// </summary>
    public static class GhostMaterials
    {
        static Material _valid;
        static Material _invalid;

        public static Material Valid => _valid != null
            ? _valid
            : _valid = Build(new Color(0.25f, 0.95f, 0.45f, 0.45f), "GhostValid");

        public static Material Invalid => _invalid != null
            ? _invalid
            : _invalid = Build(new Color(1f, 0.28f, 0.25f, 0.45f), "GhostInvalid");

        public static Material For(bool valid) => valid ? Valid : Invalid;

        static Material Build(Color color, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = name };

            if (shader.name.StartsWith("Universal"))
            {
                m.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
                m.SetFloat("_Blend", 0f);   // 0 = Alpha
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                m.SetFloat("_Mode", 3f); // Transparent
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            m.renderQueue = (int)RenderQueue.Transparent;
            m.color = color;
            return m;
        }

        /// <summary>
        /// Перефарбувати весь інстанс у матеріал привида.
        ///
        /// sharedMaterial міняє лише ПЕРШИЙ сабмеш, а моделі доріг зазвичай багатоматеріальні.
        /// Тому масив матеріалів переписується цілком. Колайдери гасимо, щоб привид не ловив промінь
        /// курсора, а тіні вимикаємо, бо тінь від напівпрозорого превʼю виглядає як помилка.
        /// </summary>
        public static void Apply(GameObject instance, Material material, List<Renderer> buffer)
        {
            if (instance == null) return;

            instance.GetComponentsInChildren(true, buffer);

            for (int i = 0; i < buffer.Count; i++)
            {
                var r = buffer[i];
                if (r == null) continue;

                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[slots];
                for (int s = 0; s < slots; s++) mats[s] = material;

                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
    }
}
