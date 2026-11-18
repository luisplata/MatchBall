// Guard de build de los íconos de launcher Android de MatchBallance.
//
// Causa raíz del rechazo en Play: el AAB publicado se compiló fuera del pipeline con
// íconos asignados sólo en el estado del Editor y nunca commiteados. Este script hace
// que el build Android re-aplique SIEMPRE los 18 slots desde los assets versionados en
// Assets/Art/AppIcon/ y aborte si alguno no se puede aplicar.
//
// Uso manual: Tools > App Icons > Apply to Android Player Settings
// (persiste ProjectSettings.asset; el build no lo necesita porque asigna en memoria).

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarkVoid.AppIcons
{
    public class AppIconBuildGuard : IPreprocessBuildWithReport
    {
        const string IconDir = "Assets/Art/AppIcon";
        const string Regenerate = "python tools/icons/generate_icons.py";
        const string Contract = "Docs/release/play-store.md  \u00a7Icono";

        public int callbackOrder { get { return 0; } }

        sealed class IconGroup
        {
            public string Prefix;
            public int Expected;
            public string[] Files;
            public string[] Roles;

            public IconGroup(string prefix, int expected, string[] files, string[] roles)
            {
                Prefix = prefix;
                Expected = expected;
                Files = files;
                Roles = roles;
            }
        }

        // [0] = background, [1] = foreground (orden verificado en AndroidPlayerBuildProgram.exe).
        static readonly IconGroup[] Groups =
        {
            new IconGroup("Adaptive", 432,
                new[] { "app_icon_background.png", "app_icon_foreground.png" },
                new[] { "background", "foreground" }),
            new IconGroup("Legacy", 192, new[] { "app_icon_legacy.png" }, new[] { "legacy" }),
            new IconGroup("Round", 192, new[] { "app_icon_round.png" }, new[] { "round" }),
        };

        // Slots que expone cada kind Android (6 densidades). Si un kind expusiera menos,
        // SetPlatformIcons dejaria slots sin textura y el build publicaria un icono default.
        const int AndroidSlots = 6;

        // Resuelve el kind por ToString() en vez de referenciar UnityEditor.Android.*:
        // así Assets/Editor compila aunque el módulo Android no esté instalado.
        // GetSupportedIconKinds(NamedBuildTarget) es la API vigente; GetSupportedIconKindsForPlatform
        // está [Obsolete] en este Editor (verificado por reflexión: "Use GetSupportedIconKinds(NamedBuildTarget) instead").
        static PlatformIconKind Kind(string prefix)
        {
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
                if (kind.ToString().StartsWith(prefix, StringComparison.Ordinal))
                    return kind;
            return null;
        }

        static Texture2D Load(string kindName, int layer, string role, int expected, string file, List<string> errors)
        {
            string path = IconDir + "/" + file;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string problem = null;

            if (texture == null)
                problem = "no existe o no importa como Texture2D (Texture Type debe ser Default, no Sprite)";
            else if (!texture.isReadable)
                problem = "no es legible (Texture Import Settings > Read/Write = On)";
            else if (texture.width != expected || texture.height != expected)
                problem = string.Format("mide {0}x{1}, se esperaba {2}x{2} (revisá Non-Power of 2 = None y Max Size = 512)",
                    texture.width, texture.height, expected);

            if (problem != null)
            {
                errors.Add(string.Format("  · Slot: {0} {1}x{1}, capa {2} ({3})\n    Archivo: {4}\n    Problema: {5}",
                    kindName, expected, layer, role, path, problem));
                return null;
            }

            return texture;
        }

        /// <summary>Re-aplica los 6 slots de cada kind Android. No persiste: devuelve los errores.</summary>
        public static List<string> Apply()
        {
            List<string> errors = new List<string>();

            foreach (IconGroup group in Groups)
            {
                PlatformIconKind kind = Kind(group.Prefix);
                if (kind == null)
                {
                    errors.Add(string.Format("  · Kind no disponible: {0} (¿está instalado el módulo Android?)", group.Prefix));
                    continue;
                }

                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (icons == null || icons.Length != AndroidSlots)
                {
                    errors.Add(string.Format("  · Kind {0} expone {1} slots, se esperaban {2} (una por densidad): "
                        + "Player Settings > Icon quedó incompleto",
                        group.Prefix, icons == null ? 0 : icons.Length, AndroidSlots));
                    continue;
                }

                Texture2D[] textures = new Texture2D[group.Files.Length];
                for (int layer = 0; layer < group.Files.Length; layer++)
                {
                    textures[layer] = Load(group.Prefix, layer, group.Roles[layer], group.Expected, group.Files[layer], errors);
                    if (textures[layer] == null)
                        break;
                }

                if (Array.IndexOf(textures, null) >= 0)
                {
                    // Falta una capa: no se toca el resto de este kind.
                    continue;
                }

                foreach (PlatformIcon icon in icons)
                {
                    if (icon.maxLayerCount < textures.Length)
                    {
                        errors.Add(string.Format("  · Slot: {0} {1}x{1} no admite {2} capas (maxLayerCount={3})",
                            group.Prefix, group.Expected, textures.Length, icon.maxLayerCount));
                        break;
                    }

                    icon.SetTextures(textures);
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            return errors;
        }

        /// <summary>Cuenta los slots Android que quedaron con texturas tras un Apply.</summary>
        public static int CountAppliedSlots()
        {
            int applied = 0;

            foreach (IconGroup group in Groups)
            {
                PlatformIconKind kind = Kind(group.Prefix);
                if (kind == null)
                    continue;

                foreach (PlatformIcon icon in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                {
                    Texture2D[] textures = icon.GetTextures();
                    if (textures == null || textures.Length == 0 || Array.IndexOf(textures, null) >= 0)
                        continue;

                    applied++;
                }
            }

            return applied;
        }

        static string BuildMessage(List<string> errors)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[AppIcon] Build bloqueado: no se pudieron aplicar los íconos de launcher Android.");
            foreach (string error in errors)
                sb.AppendLine(error);
            sb.AppendLine("  Regenerar:  " + Regenerate);
            sb.AppendLine("  Contrato:   " + Contract);
            return sb.ToString();
        }

        [MenuItem("Tools/App Icons/Apply to Android Player Settings")]
        public static void ApplyFromMenu()
        {
            List<string> errors = Apply();
            if (errors.Count > 0)
            {
                Debug.LogError(BuildMessage(errors));
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("[AppIcon] {0} slots Android asignados (Adaptive 6 x 2 capas, Round 6, Legacy 6). "
                + "Revisá 'git diff --stat ProjectSettings/ProjectSettings.asset'.", CountAppliedSlots()));
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            // Otras plataformas (iOS, WebGL, Standalone) no se tocan ni se bloquean.
            if (report.summary.platform != BuildTarget.Android)
                return;

            List<string> errors = Apply();
            if (errors.Count > 0)
                throw new BuildFailedException(BuildMessage(errors));

            Debug.Log(string.Format("[AppIcon] {0} slots re-aplicados desde assets versionados: "
                + "Adaptive 6 x 2 capas (bg, fg), Round 6, Legacy 6.", CountAppliedSlots()));
        }
    }
}
#endif
