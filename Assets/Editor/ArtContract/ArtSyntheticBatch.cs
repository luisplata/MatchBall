// ArtSyntheticBatch: red de seguridad del migrador (ASC-2 / ASC-6).
//
// Por que existe: antes de flipear 25 sprites a Single y remapear 52 fileIDs (S2) hace falta
// una prueba de que pisar los BYTES de una pieza no recorta lo que se dibuja. Hoy esa prueba
// no existe en el repo y S2 es riesgo sin auditar. Este archivo la construye:
//
//   1. GENERA un PNG sintetico exactamente al px del manifiesto (px_entrega; si la pieza
//      entrega un px distinto al actual, se declara y requiere --allow-invalid). Patron
//      deliberado: marco de 2 px magenta en los 4 bordes (borde incluido, no inset), bloques
//      blancos en las 4 esquinas, anillo amarillo, cruz central cian y diagonal verde.
//      Cualquier crop, corrimiento, escalado o tinte se ve y se mide.
//   2. RESPALDA el PNG y el .meta, con SHA-256 de cada uno.
//   3. Se AUTOPRUEBA: reescribe los mismos bytes y exige que los dos hashes sigan iguales.
//      Si la restauracion no es byte-identica, se aborta ANTES de tocar nada. Un harness que
//      no sabe restaurar es un peligro, no una herramienta.
//   4. INTERCAMBIA los bytes, espera el import (sincronico), mide la geometria del sprite
//      importado (rect / textureRect / textura) y RENDERIZA el sprite con una camara ortografica
//      a una RenderTexture de w x h: es la imagen que dibujaria el juego.
//   5. COMPARA pixel a pixel contra el patron esperado -> exact / cropped / offset / tinted /
//      mismatch, con numeros (cobertura, marco, ratio exacto, mejor corrimiento).
//   6. RESTAURA bytes + .meta y exige los dos SHA-256 originales. Si no coinciden: HARD FAILURE
//      (exit 4), se reporta y no se sigue. No se "arregla" por atras.
//
// Reglas duras de este archivo:
//   * Dry-run por defecto. Nada toca Assets/ sin el flag `apply` explicito.
//   * Nunca escribe una pieza fuera del cohorte pedido: los targets se seleccionan primero y
//     todo lo que se toca sale de esa lista (se verifica al final).
//   * Guard en dos niveles, para no caer en un deadlock con el propio PR:
//       - nivel 1 (duro): cualquier path que el harness vaya a escribir (PNG o .meta de un
//         target) con cambios sin commitear -> se NIEGA. Es el unico trabajo que un restore
//         podria pisar.
//       - nivel 2: cualquier OTRO path sucio bajo Assets/ -> se NIEGA tambien, salvo el fuente
//         de este mismo archivo (`ArtSyntheticBatch.cs` + su `.meta`), que por definicion esta
//         sin commitear mientras se revisa este PR y no lo toca ninguna escritura del run.
//     Ademas, al terminar se exige que `git status --porcelain -- Assets` sea BYTE A BYTE el
//     mismo que antes de arrancar: si Unity normaliza algo, se reporta como falla dura (exit 4).
//   * Nada de produccion: no toca spriteMode, ni .meta de arte, ni ProjectSettings. El .meta se
//     reescribe solo para devolver los bytes originales.
//   * System.IO.Path SIEMPRE calificado: en este proyecto hay un `Path : MonoBehaviour` global
//     que shadowea System.IO.Path (costo 8 CS0117 en un batch anterior).
//
// Uso:
//   Tools > Art Contract > Synthetic Harness > Print Plan (dry-run)
//   Tools > Art Contract > Synthetic Harness > Run "mode1" (escribe y restaura)
//   Unity -batchmode -quit -executeMethod DarkVoid.ArtContract.ArtSyntheticBatch.BatchDryRun
//   Unity -batchmode -quit -executeMethod DarkVoid.ArtContract.ArtSyntheticBatch.BatchApply
//
// Cohortes (alineadas a los slices de S2, para gatear cada uno por separado):
//   all      -> las 85 REPLACE
//   world    -> game_board_* + ball_*                       (S2a)
//   avatars  -> hero_* + ui_menu_avatar_* + panel/picture   (S2b)
//   rest     -> all - world - avatars                       (S2c)
//   mode1    -> REPLACE con spriteMode MEDIDO en el .meta == 1 y px_actual == px_entrega
// El parametro `pieces` (glob separado por comas) reemplaza al cohorte; matchea la pieza y el
// basename del origen. No hay parser de argv: los entrypoints son menu y -executeMethod.
//
// Exit codes: 0 ok (o dry-run ok) · 1 algun veredicto != exact · 2 plan/manifiesto ·
//             3 guard (Assets/ sucio) · 4 restauracion no byte-identica · 5 error interno.
//
// Gate: se decide con el spriteMode MEDIDO en el `.meta` (el mismo dato que lee el auditor
// Python), comparado contra `mode_target`. `mode_actual` de la sec.9 es el snapshot del
// escaneo y NO es un gate (F-S2a-2): despues de migrar una pieza, el gate tiene que poder
// correr sobre ella, no saltearla. Una pieza es corrible cuando su origen es PNG (ASC-5),
// px_actual == px_entrega y el spriteMode medido ya es Single.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DarkVoid.ArtContract
{
    /// <summary>Defecto inyectado a proposito. Solo lo usa el self-test del comparador.</summary>
    public enum SyntheticChaos
    {
        None = 0,
        Crop = 1,
        Tint = 2,
        Offset = 3,
    }

    public enum SyntheticVerdict
    {
        Exact = 0,
        Cropped = 1,
        Offset = 2,
        Tinted = 3,
        Mismatch = 4,
        Skipped = 5,
        Error = 6,
        Flipped = 7,
    }

    public static class ArtSyntheticBatch
    {
        public const string ReportDir = "Temp/art-contract/reports";
        public const string SyntheticDir = "Temp/art-contract/synthetic";
        public const string BackupDir = "Temp/art-contract/backup";
        public const string RenderDir = "Temp/art-contract/render";
        public const string PatternVersion = "synthetic-v1";

        const string MenuRoot = "Tools/Art Contract/Synthetic Harness/";
        const string DefaultCohort = "mode1";

        /// <summary>
        /// Delta de color tolerado en la media de una clase ancla antes de declarar tinte.
        /// Medido en este proyecto (formato comprimido del importador): el peor ancla comprimida
        /// da 12,8; un tinte x0.6 da 102. 32 deja aire para ambos lados.
        /// </summary>
        const double TintDelta = 32.0;

        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // Paleta del patron. El indice ES la clase esperada de cada pixel.
        static readonly Color32[] ClassColors =
        {
            new Color32(24, 24, 32, 255),    // 0 fondo
            new Color32(255, 0, 255, 255),   // 1 marco
            new Color32(255, 255, 255, 255), // 2 esquina
            new Color32(255, 220, 0, 255),   // 3 anillo
            new Color32(0, 255, 255, 255),   // 4 cruz
            new Color32(0, 255, 0, 255),     // 5 diagonal
        };

        const byte ClassBg = 0;
        const byte ClassFrame = 1;
        const byte ClassCorner = 2;
        const byte ClassRing = 3;
        const byte ClassCross = 4;
        const byte ClassDiagonal = 5;
        const byte ClassUncovered = 255;

        // ------------------------------------------------------------------ menu

        [MenuItem(MenuRoot + "Print Plan (dry-run)", false, 2100)]
        public static void PrintPlanMenu()
        {
            DryRun("all", null, false);
        }

        [MenuItem(MenuRoot + "Run cohort (escribe y restaura)", false, 2101)]
        public static void RunMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "ArtSyntheticBatch",
                "Vas a PISAR los bytes de las piezas del cohorte `" + DefaultCohort + "` y a restaurarlas al terminar.\n\n" +
                "Precondicion: `Assets/` limpio. Si la restauracion no queda byte-identica, el run se aborta.",
                "Correr",
                "Cancelar");
            if (confirmed)
            {
                Apply(DefaultCohort, null, false, SyntheticChaos.None);
            }
        }

        // ------------------------------------------------------------- entrypoints

        /// <summary>Plan sin escrituras (ni calidos ni en Assets/). Devuelve 0 si pudo planificar.</summary>
        public static int DryRun(string cohort, string pieces, bool allowInvalid)
        {
            return Run(false, new Options(cohort, pieces, allowInvalid, SyntheticChaos.None));
        }

        /// <summary>Corre el pipeline completo sobre el cohorte. Es el unico camino que escribe.</summary>
        public static int Apply(string cohort, string pieces, bool allowInvalid, SyntheticChaos chaos)
        {
            return Run(true, new Options(cohort, pieces, allowInvalid, chaos));
        }

        /// <summary>Dry-run del cohorte valido hoy (`mode1`).</summary>
        public static void BatchDryRun()
        {
            int code = DryRun(DefaultCohort, null, false);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        /// <summary>Apply del cohorte valido hoy (`mode1`).</summary>
        public static void BatchApply()
        {
            int code = Apply(DefaultCohort, null, false, SyntheticChaos.None);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        // ---------------------------------------------------------------- opciones

        sealed class Options
        {
            public readonly string Cohort;
            public readonly string Pieces;
            public readonly bool AllowInvalid;
            public readonly SyntheticChaos Chaos;

            public Options(string cohort, string pieces, bool allowInvalid, SyntheticChaos chaos)
            {
                Cohort = string.IsNullOrEmpty(cohort) ? DefaultCohort : cohort.Trim();
                Pieces = pieces == null ? null : pieces.Trim();
                AllowInvalid = allowInvalid;
                Chaos = chaos;
            }
        }

        sealed class Target
        {
            public ArtContractRow Row;
            public string Pieza;
            public string AssetPath;
            public string MetaPath;
            public int ExpectedW;
            public int ExpectedH;
            public string ExpectedSource;
            public int FileW;
            public int FileH;
            /// <summary>spriteMode medido en el `.meta` (el mismo dato que lee el auditor Python). -1 = no se pudo medir.</summary>
            public int MeasuredMode = -1;
            public string SkipReason;
        }

        sealed class Fingerprint
        {
            public string PngSha;
            public string MetaSha;
            public long PngLength;
            public long MetaLength;

            public bool SameAs(Fingerprint other)
            {
                return other != null
                    && string.Equals(PngSha, other.PngSha, StringComparison.Ordinal)
                    && string.Equals(MetaSha, other.MetaSha, StringComparison.Ordinal);
            }

            public string Describe()
            {
                return "png " + Short(PngSha) + " (" + PngLength + " B) · meta " + Short(MetaSha) + " (" + MetaLength + " B)";
            }

            static string Short(string sha)
            {
                return string.IsNullOrEmpty(sha) ? "?" : sha.Substring(0, 16) + "...";
            }
        }

        sealed class Outcome
        {
            public Target Target;
            public SyntheticVerdict Verdict;
            public string Reason;
            public Fingerprint Before;
            public Fingerprint After;
            public bool RestoreIdentical;
            public bool RestoreFirstOk;
            public string Geometry;
            public int TextureW;
            public int TextureH;
            public int SpriteRectX;
            public int SpriteRectY;
            public int SpriteRectW;
            public int SpriteRectH;
            public Comparison Compare;
            public string EvidenceDir;
            public bool HardFailure;
        }

        sealed class Comparison
        {
            public int Width;
            public int Height;
            public long Covered;
            public bool CoverageFull;
            public string CoverRect = "vacio";
            public double ExactRatio;
            public double FrameCoverage;
            public double ClassAgreement;
            public double OffsetRatio;
            public int OffsetX;
            public int OffsetY;
            public double FlippedRatio;
            public double ClassDelta;
            public string Anchor = "-";
            public SyntheticVerdict Verdict;
            public string Reason;

            public string Summary()
            {
                return "exacto(tol8) " + Pct(ExactRatio) + " · marco " + Pct(FrameCoverage) + " · clases " +
                       Pct(ClassAgreement) + " · ancla " + Anchor + " delta " +
                       ClassDelta.ToString("0.0", CultureInfo.InvariantCulture) +
                       " · cobertura " + CoverRect + " · mejor offset (" + OffsetX + "," + OffsetY + ") " + Pct(OffsetRatio) +
                       " · espejo vertical " + Pct(FlippedRatio);
            }

            static string Pct(double value)
            {
                return (value * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%";
            }
        }

        sealed class SyntheticPattern
        {
            public int Width;
            public int Height;
            public Color32[] Pixels;
            public byte[] Classes;
        }

        // ------------------------------------------------------------------- run

        static int Run(bool apply, Options options)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[ArtSyntheticBatch] rechazado: el editor esta en play mode (o entrando).");
                return 5;
            }

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            Debug.Log("<b>ArtSyntheticBatch</b> " + (apply ? "APPLY" : "DRY-RUN") + " · " + Describe(options));

            ArtContractManifest manifest;
            try
            {
                manifest = ArtContractManifest.Load();
            }
            catch (ArtContractManifestException error)
            {
                Debug.LogError("[ArtSyntheticBatch] no se pudo leer el manifiesto: " + error.Message);
                return 2;
            }

            List<Target> targets;
            try
            {
                targets = SelectTargets(manifest, options);
            }
            catch (Exception error)
            {
                Debug.LogError("[ArtSyntheticBatch] no se pudo construir el cohorte: " + error.Message);
                return 2;
            }

            if (targets.Count == 0)
            {
                Debug.Log("[ArtSyntheticBatch] el cohorte quedo vacio: no hay nada que probar.");
                return 2;
            }

            string beforePorcelain;
            string gitError;
            bool gitOk = TryReadDirtyAssets(out beforePorcelain, out gitError);
            int guardCode = CheckGuard(apply, gitOk, gitError, beforePorcelain, targets);
            if (guardCode != 0)
            {
                return guardCode;
            }

            PrintPlan(targets, options);

            if (!apply)
            {
                Debug.Log("[ArtSyntheticBatch] dry-run: 0 escrituras (ni Assets/, ni .meta, ni Temp/). " +
                          "Patron " + PatternVersion + " listo para " + targets.Count + " piezas.");
                return 0;
            }

            List<Outcome> outcomes = new List<Outcome>();
            List<string> hardFailures = new List<string>();
            string runDir = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), RenderDir, stamp);

            foreach (Target target in targets)
            {
                if (!string.IsNullOrEmpty(target.SkipReason))
                {
                    outcomes.Add(new Outcome
                    {
                        Target = target,
                        Verdict = SyntheticVerdict.Skipped,
                        Reason = target.SkipReason,
                    });
                    continue;
                }

                Outcome outcome = ProcessTarget(target, options, stamp, runDir);
                outcomes.Add(outcome);
                if (outcome.HardFailure)
                {
                    hardFailures.Add(target.Pieza + ": " + outcome.Reason);
                }
            }

            string afterPorcelain = beforePorcelain;
            string afterError;
            bool afterOk = TryReadDirtyAssets(out afterPorcelain, out afterError);
            bool treeRestored = afterOk && string.Equals(afterPorcelain ?? string.Empty,
                beforePorcelain ?? string.Empty, StringComparison.Ordinal);

            string reportPath = WriteReport(stamp, options, outcomes, hardFailures,
                gitOk && afterOk, beforePorcelain, afterPorcelain, treeRestored);

            int exitCode = 0;
            if (hardFailures.Count > 0)
            {
                exitCode = 4;
            }
            else if (!treeRestored)
            {
                exitCode = 4;
                hardFailures.Add("`git status --porcelain -- Assets` no volvio al estado inicial");
            }
            else
            {
                for (int i = 0; i < outcomes.Count; i++)
                {
                    SyntheticVerdict verdict = outcomes[i].Verdict;
                    if (verdict != SyntheticVerdict.Exact && verdict != SyntheticVerdict.Skipped)
                    {
                        exitCode = 1;
                        break;
                    }
                }
            }

            LogSummary(outcomes, hardFailures, treeRestored, reportPath, exitCode);
            return exitCode;
        }

        static string Describe(Options options)
        {
            string cohort = options.Pieces == null ? "cohorte `" + options.Cohort + "`" : "pieces `" + options.Pieces + "`";
            string extra = string.Empty;
            if (options.AllowInvalid)
            {
                extra += " · allow-invalid";
            }
            if (options.Chaos != SyntheticChaos.None)
            {
                extra += " · CHAOS " + options.Chaos;
            }
            return cohort + extra;
        }

        // --------------------------------------------------------------- cohorte

        static List<Target> SelectTargets(ArtContractManifest manifest, Options options)
        {
            var targets = new List<Target>();
            if (options.Pieces != null)
            {
                Debug.Log("[ArtSyntheticBatch] `pieces` reemplaza al cohorte `" + options.Cohort + "`.");
            }

            Regex pattern = options.Pieces == null ? null : BuildGlob(options.Pieces);

            foreach (ArtContractRow row in manifest.Rows)
            {
                if (!row.EsReplace)
                {
                    continue;
                }

                var target = new Target
                {
                    Row = row,
                    Pieza = row.Pieza,
                    AssetPath = ArtContractManifest.NormalizePath(row.Origen),
                    MetaPath = ArtContractManifest.NormalizePath(row.Origen) + ".meta",
                };
                // El modo medido se lee del `.meta` ANTES de elegir el cohorte: el cohorte `mode1`
                // ya no se define por la columna historica `mode_actual` de la sec.9.
                target.MeasuredMode = MeasureSpriteMode(target.MetaPath);

                if (pattern != null)
                {
                    if (!Matches(pattern, row.Pieza) && !Matches(pattern, BaseName(row.Origen)))
                    {
                        continue;
                    }
                }
                else if (!MatchesCohort(options.Cohort, target))
                {
                    continue;
                }

                ResolveExpectation(target);
                targets.Add(target);
            }

            if (pattern != null && targets.Count == 0)
            {
                throw new InvalidOperationException("`pieces` no matcheo ninguna pieza REPLACE: " + options.Pieces);
            }
            if (pattern == null && targets.Count == 0)
            {
                throw new InvalidOperationException("cohorte desconocido o vacio: " + options.Cohort +
                    " (validos: all, world, avatars, rest, mode1)");
            }
            return targets;
        }

        static bool MatchesCohort(string cohort, Target target)
        {
            ArtContractRow row = target.Row;
            string pieza = row.Pieza ?? string.Empty;
            switch ((cohort ?? string.Empty).ToLowerInvariant())
            {
                case "all":
                    return true;
                case "world":
                    return pieza.StartsWith("game_board_", StringComparison.Ordinal)
                        || pieza.StartsWith("ball_", StringComparison.Ordinal);
                case "avatars":
                    return pieza.StartsWith("hero_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_avatar_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_panel_profile_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_picture_profile_", StringComparison.Ordinal);
                case "rest":
                    return !MatchesCohort("world", target) && !MatchesCohort("avatars", target);
                case "mode1":
                    return IsValidToday(target);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Corrible HOY: el import ya esta en Single (medido, no el snapshot de la sec.9) y
        /// px_actual == px_entrega (si difieren, el swap cambia el tamano y el patron no calza).
        /// </summary>
        static bool IsValidToday(Target target)
        {
            return target.MeasuredMode == 1
                && string.Equals(target.Row.PxActual, target.Row.PxEntrega, StringComparison.Ordinal);
        }

        /// <summary>
        /// spriteMode real del import, leido del `.meta` (la MISMA medicion que hace
        /// tools/art/audit_art_manifest.py: la linea `spriteMode:` del TextureImporter).
        /// -1 si falta el `.meta` o no se puede leer. Solo lectura: no importa ni reimporta.
        /// </summary>
        static int MeasureSpriteMode(string metaPath)
        {
            if (string.IsNullOrEmpty(metaPath))
            {
                return -1;
            }
            string absolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), metaPath);
            if (!File.Exists(absolute))
            {
                return -1;
            }
            foreach (string line in File.ReadLines(absolute))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("spriteMode:", StringComparison.Ordinal))
                {
                    continue;
                }
                int mode;
                if (int.TryParse(trimmed.Substring("spriteMode:".Length).Trim(),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out mode))
                {
                    return mode;
                }
                return -1;
            }
            return -1;
        }

        static string ModeLabel(int mode)
        {
            switch (mode)
            {
                case 1: return "1 (Single)";
                case 2: return "2 (Multiple)";
                case 3: return "3 (Polygon)";
                case 0: return "0 (None)";
                default: return mode < 0 ? "?" : mode.ToString(CultureInfo.InvariantCulture);
            }
        }

        static void ResolveExpectation(Target target)
        {
            Vector2 size;
            if (ArtContractManifest.TryParseSize(target.Row.PxEntrega, out size))
            {
                target.ExpectedW = Mathf.RoundToInt(size.x);
                target.ExpectedH = Mathf.RoundToInt(size.y);
                target.ExpectedSource = "px_entrega";
            }
            else if (ArtContractManifest.TryParseSize(target.Row.PxActual, out size))
            {
                target.ExpectedW = Mathf.RoundToInt(size.x);
                target.ExpectedH = Mathf.RoundToInt(size.y);
                target.ExpectedSource = "px_actual (px_entrega TBD)";
            }
            else
            {
                target.SkipReason = "px desconocido en sec.9 (px_actual y px_entrega TBD)";
                return;
            }

            string absolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), target.AssetPath);
            if (!File.Exists(absolute))
            {
                target.SkipReason = "el origen no existe en disco: " + target.AssetPath;
                return;
            }

            byte[] bytes = File.ReadAllBytes(absolute);
            int w;
            int h;
            if (!TryReadPngSize(bytes, out w, out h))
            {
                target.SkipReason = "el origen no es PNG (ASC-5): " + target.AssetPath;
                return;
            }
            target.FileW = w;
            target.FileH = h;

            Vector2 declared;
            bool hasDeclared = ArtContractManifest.TryParseSize(target.Row.PxActual, out declared);
            if (hasDeclared && (Mathf.RoundToInt(declared.x) != w || Mathf.RoundToInt(declared.y) != h))
            {
                Debug.LogWarning("[ArtSyntheticBatch] " + target.Pieza + ": px_actual " + target.Row.PxActual +
                                 " != cabecera del archivo " + w + "x" + h + " (el manifiesto manda como esperado).");
            }
        }

        static Regex BuildGlob(string pieces)
        {
            var builder = new StringBuilder("^(?:");
            string[] terms = pieces.Split(',');
            int written = 0;
            foreach (string term in terms)
            {
                string trimmed = term.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }
                if (written > 0)
                {
                    builder.Append('|');
                }
                builder.Append(WildcardToRegex(trimmed));
                written++;
            }
            builder.Append(")$");
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        static string WildcardToRegex(string glob)
        {
            var builder = new StringBuilder();
            foreach (char character in glob)
            {
                if (character == '*')
                {
                    builder.Append(".*");
                }
                else if (character == '?')
                {
                    builder.Append('.');
                }
                else
                {
                    builder.Append(Regex.Escape(character.ToString()));
                }
            }
            return builder.ToString();
        }

        static bool Matches(Regex pattern, string value)
        {
            return !string.IsNullOrEmpty(value) && pattern.IsMatch(value);
        }

        static string BaseName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            string normalized = ArtContractManifest.NormalizePath(path);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        // ------------------------------------------------------------------ plan

        static void PrintPlan(List<Target> targets, Options options)
        {
            // El gate se resuelve ANTES de imprimir: lo que se lista como `skip` es lo que se saltea.
            if (!options.AllowInvalid)
            {
                foreach (Target target in targets)
                {
                    if (string.IsNullOrEmpty(target.SkipReason) && !IsValidToday(target))
                    {
                        target.SkipReason = GateReason(target);
                    }
                }
            }

            int runnable = 0;
            int skipped = 0;
            Debug.Log("[ArtSyntheticBatch] cohorte: " + targets.Count + " piezas REPLACE (" + Describe(options) + ")");
            foreach (Target target in targets)
            {
                if (string.IsNullOrEmpty(target.SkipReason))
                {
                    runnable++;
                    Debug.Log("  run  " + Pad(target.Pieza, 44) + " " + target.ExpectedW + "x" + target.ExpectedH +
                              " (" + target.ExpectedSource + ") · archivo " + target.FileW + "x" + target.FileH +
                              " · mode medido " + ModeLabel(target.MeasuredMode) +
                              " · target " + target.Row.ModeTarget + " · " + target.AssetPath);
                }
                else
                {
                    skipped++;
                    Debug.Log("  skip " + Pad(target.Pieza, 44) + target.SkipReason);
                }
            }
            Debug.Log("[ArtSyntheticBatch] plan: " + runnable + " corribles · " + skipped + " salteadas");
        }

        static string GateReason(Target target)
        {
            if (target.MeasuredMode != 1)
            {
                return "spriteMode medido " + ModeLabel(target.MeasuredMode) + " != mode_target " +
                       target.Row.ModeTarget + ": el gate sintetico solo es valido con el import ya en Single" +
                       " (design 3.5; mode_actual de sec.9 es historico y no se usa)";
            }
            return "px_entrega " + target.Row.PxEntrega + " != px_actual " + target.Row.PxActual +
                   ": el swap cambia el tamano del archivo, no es un swap de bytes 1:1";
        }

        // ------------------------------------------------------------------ apply

        static Outcome ProcessTarget(Target target, Options options, string stamp, string runDir)
        {
            var outcome = new Outcome { Target = target, Verdict = SyntheticVerdict.Error };
            string assetAbsolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), target.AssetPath);
            string metaAbsolute = assetAbsolute + ".meta";

            try
            {
                byte[] originalPng = File.ReadAllBytes(assetAbsolute);
                if (!File.Exists(metaAbsolute))
                {
                    outcome.Reason = "no existe el .meta: " + target.MetaPath;
                    outcome.HardFailure = true;
                    return outcome;
                }
                byte[] originalMeta = File.ReadAllBytes(metaAbsolute);
                outcome.Before = FingerprintOf(originalPng, originalMeta);

                // respaldo
                string backupDir = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), BackupDir, stamp,
                    SafeName(target.Pieza));
                Directory.CreateDirectory(backupDir);
                File.WriteAllBytes(System.IO.Path.Combine(backupDir, BaseName(target.AssetPath)), originalPng);
                File.WriteAllBytes(System.IO.Path.Combine(backupDir, BaseName(target.AssetPath) + ".meta"), originalMeta);

                // restore-first: la maquinaria de restauracion se prueba antes de tocar el asset
                WriteBoth(assetAbsolute, metaAbsolute, originalPng, originalMeta);
                ImportAsset(target.AssetPath);
                Fingerprint probe = FingerprintOf(File.ReadAllBytes(assetAbsolute), File.ReadAllBytes(metaAbsolute));
                if (!outcome.Before.SameAs(probe))
                {
                    outcome.Reason = "restore-first no es byte-identico (antes " + outcome.Before.Describe() +
                                     "; despues " + probe.Describe() + ")";
                    outcome.HardFailure = true;
                    return outcome;
                }
                outcome.RestoreFirstOk = true;

                // patron sintetico al px esperado
                SyntheticPattern pattern = BuildPattern(target.ExpectedW, target.ExpectedH);
                byte[] synthetic = EncodePng(pattern);
                string syntheticDir = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), SyntheticDir, stamp);
                Directory.CreateDirectory(syntheticDir);
                File.WriteAllBytes(System.IO.Path.Combine(syntheticDir, target.Pieza), synthetic);

                // swap + import
                File.WriteAllBytes(assetAbsolute, synthetic);
                ImportAsset(target.AssetPath);
                bool sized = WaitForTexture(target.AssetPath, pattern.Width, pattern.Height, 8000);
                if (!sized)
                {
                    Texture2D stalled = AssetDatabase.LoadAssetAtPath<Texture2D>(target.AssetPath);
                    outcome.Reason = "el import no alcanzo " + pattern.Width + "x" + pattern.Height + " (quedo " +
                                     (stalled == null ? "sin textura" : stalled.width + "x" + stalled.height) +
                                     "): downsample silencioso o import truncado";
                    RestoreAndVerify(target, outcome, originalPng, originalMeta, assetAbsolute, metaAbsolute);
                    outcome.Verdict = SyntheticVerdict.Cropped;
                    return outcome;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target.AssetPath);
                if (sprite == null)
                {
                    outcome.Reason = "el asset no importo como Sprite";
                    RestoreAndVerify(target, outcome, originalPng, originalMeta, assetAbsolute, metaAbsolute);
                    outcome.Verdict = SyntheticVerdict.Error;
                    return outcome;
                }

                MeasureGeometry(target, sprite, outcome);

                string renderError;
                Color32[] rendered;
                outcome.EvidenceDir = System.IO.Path.Combine(runDir, SafeName(target.Pieza));
                rendered = RenderSprite(sprite, pattern.Width, pattern.Height, options.Chaos, out renderError);
                if (rendered == null)
                {
                    outcome.Reason = "no se pudo renderizar el sprite: " + renderError;
                    RestoreAndVerify(target, outcome, originalPng, originalMeta, assetAbsolute, metaAbsolute);
                    outcome.Verdict = SyntheticVerdict.Error;
                    return outcome;
                }

                outcome.Compare = Compare(pattern, rendered);
                SaveEvidence(outcome, pattern, rendered);

                RestoreAndVerify(target, outcome, originalPng, originalMeta, assetAbsolute, metaAbsolute);
                if (outcome.HardFailure)
                {
                    return outcome;
                }

                outcome.Verdict = ResolveVerdict(target, outcome);
                return outcome;
            }
            catch (Exception error)
            {
                outcome.Reason = "excepcion: " + error.GetType().Name + ": " + error.Message;
                outcome.HardFailure = true;
                outcome.Verdict = SyntheticVerdict.Error;
                TryRestore(target);
                return outcome;
            }
        }

        static void RestoreAndVerify(Target target, Outcome outcome, byte[] originalPng, byte[] originalMeta,
            string assetAbsolute, string metaAbsolute)
        {
            WriteBoth(assetAbsolute, metaAbsolute, originalPng, originalMeta);
            ImportAsset(target.AssetPath);
            Fingerprint after = FingerprintOf(File.ReadAllBytes(assetAbsolute), File.ReadAllBytes(metaAbsolute));
            if (!outcome.Before.SameAs(after))
            {
                // un reintento: Unity puede reserializar el .meta en el primer import post-swap.
                WriteBoth(assetAbsolute, metaAbsolute, originalPng, originalMeta);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                after = FingerprintOf(File.ReadAllBytes(assetAbsolute), File.ReadAllBytes(metaAbsolute));
            }
            outcome.After = after;
            outcome.RestoreIdentical = outcome.Before.SameAs(after);
            if (!outcome.RestoreIdentical)
            {
                outcome.HardFailure = true;
                outcome.Reason = "RESTAURACION NO BYTE-IDENTICA: antes " + outcome.Before.Describe() +
                                 "; despues " + after.Describe();
            }
        }

        static void TryRestore(Target target)
        {
            try
            {
                string assetAbsolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), target.AssetPath);
                string metaAbsolute = assetAbsolute + ".meta";
                string backupDir = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), BackupDir);
                if (!Directory.Exists(backupDir))
                {
                    return;
                }
                string[] candidates = Directory.GetDirectories(backupDir, SafeName(target.Pieza), SearchOption.AllDirectories);
                if (candidates.Length == 0)
                {
                    return;
                }
                Array.Sort(candidates, StringComparer.Ordinal);
                string dir = candidates[candidates.Length - 1];
                File.WriteAllBytes(assetAbsolute, File.ReadAllBytes(System.IO.Path.Combine(dir, BaseName(target.AssetPath))));
                File.WriteAllBytes(metaAbsolute,
                    File.ReadAllBytes(System.IO.Path.Combine(dir, BaseName(target.AssetPath) + ".meta")));
                ImportAsset(target.AssetPath);
            }
            catch (Exception restoreError)
            {
                Debug.LogError("[ArtSyntheticBatch] no se pudo restaurar " + target.Pieza + ": " + restoreError.Message);
            }
        }

        static SyntheticVerdict ResolveVerdict(Target target, Outcome outcome)
        {
            // La geometria manda cuando explica el defecto: es la causa, no el sintoma.
            if (outcome.TextureW != target.ExpectedW || outcome.TextureH != target.ExpectedH)
            {
                outcome.Reason = "textura importada " + outcome.TextureW + "x" + outcome.TextureH +
                                 " != esperado " + target.ExpectedW + "x" + target.ExpectedH;
                return SyntheticVerdict.Cropped;
            }
            if (outcome.SpriteRectW != target.ExpectedW || outcome.SpriteRectH != target.ExpectedH)
            {
                outcome.Reason = "rect del sprite " + outcome.SpriteRectW + "x" + outcome.SpriteRectH +
                                 " != lienzo " + target.ExpectedW + "x" + target.ExpectedH;
                return SyntheticVerdict.Cropped;
            }
            if (outcome.SpriteRectX != 0 || outcome.SpriteRectY != 0)
            {
                outcome.Reason = "rect del sprite arranca en " + outcome.SpriteRectX + "," + outcome.SpriteRectY +
                                 " en vez de 0,0";
                return SyntheticVerdict.Offset;
            }
            outcome.Reason = outcome.Compare == null ? "sin comparacion" : outcome.Compare.Reason;
            return outcome.Compare == null ? SyntheticVerdict.Error : outcome.Compare.Verdict;
        }

        static void MeasureGeometry(Target target, Sprite sprite, Outcome outcome)
        {
            Texture2D texture = sprite.texture;
            outcome.TextureW = texture == null ? 0 : texture.width;
            outcome.TextureH = texture == null ? 0 : texture.height;
            Rect rect = sprite.rect;
            outcome.SpriteRectX = Mathf.RoundToInt(rect.x);
            outcome.SpriteRectY = Mathf.RoundToInt(rect.y);
            outcome.SpriteRectW = Mathf.RoundToInt(rect.width);
            outcome.SpriteRectH = Mathf.RoundToInt(rect.height);
            outcome.Geometry = "textura " + outcome.TextureW + "x" + outcome.TextureH +
                               " (" + (texture == null ? "?" : texture.format.ToString()) + ")" +
                               " · rect " + outcome.SpriteRectX + "," + outcome.SpriteRectY + " " +
                               outcome.SpriteRectW + "x" + outcome.SpriteRectH +
                               " · ppu " + sprite.pixelsPerUnit.ToString("0.##", CultureInfo.InvariantCulture) +
                               " · borde " + sprite.border;
        }

        // ----------------------------------------------------------------- patron

        static SyntheticPattern BuildPattern(int width, int height)
        {
            var pattern = new SyntheticPattern
            {
                Width = width,
                Height = height,
                Pixels = new Color32[width * height],
                Classes = new byte[width * height],
            };

            int frame = Math.Max(1, Math.Min(2, Math.Min(width, height) / 6));
            int corner = Math.Max(2, Math.Min(6, Math.Min(width, height) / 6));
            bool ring = width > 2 * (frame + 2) + 4 && height > 2 * (frame + 2) + 4;
            bool cross = width >= 9 && height >= 9;
            bool diagonal = width >= 16 && height >= 16;

            for (int i = 0; i < pattern.Pixels.Length; i++)
            {
                pattern.Pixels[i] = ClassColors[ClassBg];
                pattern.Classes[i] = ClassBg;
            }

            if (ring)
            {
                int inset = frame + 2;
                int thickness = 2;
                for (int y = inset; y < height - inset; y++)
                {
                    for (int x = inset; x < width - inset; x++)
                    {
                        if (x < inset + thickness || x >= width - inset - thickness ||
                            y < inset + thickness || y >= height - inset - thickness)
                        {
                            SetPixel(pattern, x, y, ClassRing);
                        }
                    }
                }
            }

            if (diagonal)
            {
                int steps = Math.Max(width, height);
                for (int step = frame; step < steps - frame; step++)
                {
                    int x = Math.Min(width - 1 - frame, step);
                    int y = (int)Math.Round((double)step * (height - 1) / Math.Max(1, width - 1));
                    SetPixel(pattern, x, y, ClassDiagonal);
                }
            }

            if (cross)
            {
                int centreX = width / 2;
                int centreY = height / 2;
                for (int x = 0; x < width; x++)
                {
                    SetPixel(pattern, x, centreY, ClassCross);
                    if (centreY - 1 >= 0)
                    {
                        SetPixel(pattern, x, centreY - 1, ClassCross);
                    }
                    if (centreY + 1 < height)
                    {
                        SetPixel(pattern, x, centreY + 1, ClassCross);
                    }
                }
                for (int y = 0; y < height; y++)
                {
                    SetPixel(pattern, centreX, y, ClassCross);
                    if (centreX - 1 >= 0)
                    {
                        SetPixel(pattern, centreX - 1, y, ClassCross);
                    }
                    if (centreX + 1 < width)
                    {
                        SetPixel(pattern, centreX + 1, y, ClassCross);
                    }
                }
            }

            // esquinas: identidad y orientacion (un flip o un espejo las mueve)
            for (int y = 0; y < corner; y++)
            {
                for (int x = 0; x < corner; x++)
                {
                    SetPixel(pattern, frame + x, frame + y, ClassCorner);
                    SetPixel(pattern, width - frame - 1 - x, frame + y, ClassCorner);
                    SetPixel(pattern, frame + x, height - frame - 1 - y, ClassCorner);
                    SetPixel(pattern, width - frame - 1 - x, height - frame - 1 - y, ClassCorner);
                }
            }

            // marco al final: siempre gana, es el borde inclusivo del lienzo
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < frame || y < frame || x >= width - frame || y >= height - frame)
                    {
                        SetPixel(pattern, x, y, ClassFrame);
                    }
                }
            }

            return pattern;
        }

        static void SetPixel(SyntheticPattern pattern, int x, int y, byte klass)
        {
            if (x < 0 || y < 0 || x >= pattern.Width || y >= pattern.Height)
            {
                return;
            }
            int index = y * pattern.Width + x;
            pattern.Pixels[index] = ClassColors[klass];
            pattern.Classes[index] = klass;
        }

        static byte[] EncodePng(SyntheticPattern pattern)
        {
            var texture = new Texture2D(pattern.Width, pattern.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pattern.Pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // ----------------------------------------------------------------- render

        /// <summary>
        /// "Renderiza" el sprite sobre un lienzo de expectedW x expectedH: compone la region que el
        /// sprite declara (`sprite.textureRect`) en el lugar que declara (`sprite.rect`), al px del
        /// lienzo. Es el mapeo sprite -> lienzo que hace el SpriteRenderer al construir la malla.
        ///
        /// Por que no se usa una camara: el proyecto es URP 17 y `Camera.Render()` no dibuja nada
        /// bajo un SRP (medido: RenderTexture vacia). `RenderPipeline.SubmitRenderRequest` tampoco
        /// (medido: 0 px opacos). `Graphics.DrawTexture` sobre un RenderTexture activo si dibuja y
        /// es independiente del pipeline (medido). Se usa `Point` para que no interpole.
        /// </summary>
        static Color32[] RenderSprite(Sprite sprite, int canvasWidth, int canvasHeight, SyntheticChaos chaos,
            out string error)
        {
            error = null;
            Texture2D readback = null;
            RenderTexture target = null;
            RenderTexture previous = null;
            try
            {
                Texture2D texture = sprite.texture;
                if (texture == null)
                {
                    error = "el sprite no tiene textura";
                    return null;
                }

                Rect textureRect = sprite.textureRect;
                float u = textureRect.x / texture.width;
                float v = textureRect.y / texture.height;
                float uw = textureRect.width / texture.width;
                float vh = textureRect.height / texture.height;

                Rect rect = sprite.rect;
                float scaleX = canvasWidth / (float)texture.width;
                float scaleY = canvasHeight / (float)texture.height;
                var destination = new Rect(rect.x * scaleX, rect.y * scaleY, rect.width * scaleX, rect.height * scaleY);

                if (chaos == SyntheticChaos.Crop)
                {
                    // simula un rect que apunta al 80% interior de la pieza: el marco se pierde
                    float insetU = uw * 0.1f;
                    float insetV = vh * 0.1f;
                    u += insetU;
                    v += insetV;
                    uw -= 2f * insetU;
                    vh -= 2f * insetV;
                    destination = new Rect(0f, 0f, canvasWidth, canvasHeight);
                }
                else if (chaos == SyntheticChaos.Offset)
                {
                    // simula un rect corrido: la pieza se dibuja desplazada 3 px en x e y
                    destination = new Rect(destination.x + 3f, destination.y + 3f, destination.width,
                        destination.height);
                }

                var source = new Rect(u, v, uw, vh);

                target = RenderTexture.GetTemporary(canvasWidth, canvasHeight, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                target.filterMode = FilterMode.Point;
                previous = RenderTexture.active;
                RenderTexture.active = target;

                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, canvasWidth, canvasHeight, 0f);
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                Graphics.DrawTexture(destination, texture, source, 0, 0, 0, 0);
                GL.PopMatrix();

                RenderTexture.active = target;
                readback = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0f, 0f, canvasWidth, canvasHeight), 0, 0, false);
                readback.Apply(false, false);

                Color32[] pixels = readback.GetPixels32();
                if (chaos == SyntheticChaos.Tint)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color32 pixel = pixels[i];
                        pixels[i] = new Color32((byte)(pixel.r * 0.6f), pixel.g, (byte)(pixel.b * 0.6f), pixel.a);
                    }
                }
                return pixels;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
                if (readback != null)
                {
                    Object.DestroyImmediate(readback);
                }
            }
        }

        static void SaveEvidence(Outcome outcome, SyntheticPattern pattern, Color32[] rendered)
        {
            if (string.IsNullOrEmpty(outcome.EvidenceDir))
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(outcome.EvidenceDir);
                SavePng(System.IO.Path.Combine(outcome.EvidenceDir, "esperado.png"), pattern.Pixels, pattern.Width, pattern.Height);
                SavePng(System.IO.Path.Combine(outcome.EvidenceDir, "render.png"), rendered, pattern.Width, pattern.Height);
                var diff = new Color32[rendered.Length];
                for (int i = 0; i < rendered.Length; i++)
                {
                    Color32 actual = rendered[i];
                    // rojo = la estructura se fue (clase distinta); ambar = estructura ok, color perdido
                    bool classOk = NearestClass(actual) == pattern.Classes[i];
                    Color32 expected = pattern.Pixels[i];
                    bool colourOk = actual.a > 8
                                    && Math.Abs(expected.r - actual.r) <= 8
                                    && Math.Abs(expected.g - actual.g) <= 8
                                    && Math.Abs(expected.b - actual.b) <= 8;
                    if (!classOk)
                    {
                        diff[i] = new Color32(255, 40, 40, 255);
                    }
                    else if (!colourOk)
                    {
                        diff[i] = new Color32(255, 170, 0, 255);
                    }
                    else
                    {
                        diff[i] = new Color32(0, 0, 0, 255);
                    }
                }
                SavePng(System.IO.Path.Combine(outcome.EvidenceDir, "diff.png"), diff, pattern.Width, pattern.Height);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[ArtSyntheticBatch] no se pudo guardar la evidencia de " + outcome.Target.Pieza +
                                 ": " + error.Message);
            }
        }

        static void SavePng(string path, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // --------------------------------------------------------------- comparar

        static Comparison Compare(SyntheticPattern expected, Color32[] rendered)
        {
            int width = expected.Width;
            int height = expected.Height;
            var result = new Comparison { Width = width, Height = height };
            Color32[] pixels = expected.Pixels;
            int total = width * height;

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            long covered = 0;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (rendered[row + x].a <= 8)
                    {
                        continue;
                    }
                    covered++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            result.Covered = covered;
            result.CoverageFull = maxX >= 0 && minX == 0 && minY == 0 && maxX == width - 1 && maxY == height - 1;
            result.CoverRect = maxX < 0
                ? "vacio"
                : minX + "," + minY + " " + (maxX - minX + 1) + "x" + (maxY - minY + 1);

            long exact = 0;
            long agree = 0;
            for (int i = 0; i < total; i++)
            {
                Color32 want = pixels[i];
                Color32 got = rendered[i];
                if (got.a > 8
                    && Math.Abs(want.r - got.r) <= 8
                    && Math.Abs(want.g - got.g) <= 8
                    && Math.Abs(want.b - got.b) <= 8)
                {
                    exact++;
                }
                if (NearestClass(got) == expected.Classes[i])
                {
                    agree++;
                }
            }
            result.ExactRatio = (double)exact / total;
            result.ClassAgreement = (double)agree / total;

            // Medias por clase sobre las anclas. La compresion (DXT/ETC) mueve pixeles sueltos
            // pero no la media de una clase; un tinte global si mueve la media. Medido: en las
            // piezas chicas comprimidas el delta maximo de ancla es 12,8; un tinte x0.6 da 102.
            var sumR = new double[ClassColors.Length];
            var sumG = new double[ClassColors.Length];
            var sumB = new double[ClassColors.Length];
            var counts = new long[ClassColors.Length];
            for (int i = 0; i < total; i++)
            {
                Color32 got = rendered[i];
                if (got.a <= 8)
                {
                    continue;
                }
                byte klass = expected.Classes[i];
                if (klass == ClassBg || klass == ClassDiagonal)
                {
                    continue;
                }
                sumR[klass] += got.r;
                sumG[klass] += got.g;
                sumB[klass] += got.b;
                counts[klass]++;
            }
            double worst = 0;
            string worstName = "-";
            for (int klass = 0; klass < ClassColors.Length; klass++)
            {
                if (counts[klass] < 8)
                {
                    continue;
                }
                Color32 want = ClassColors[klass];
                double delta = Math.Max(Math.Abs(sumR[klass] / counts[klass] - want.r),
                    Math.Max(Math.Abs(sumG[klass] / counts[klass] - want.g),
                        Math.Abs(sumB[klass] / counts[klass] - want.b)));
                if (delta > worst)
                {
                    worst = delta;
                    worstName = ClassName(klass);
                }
            }
            result.ClassDelta = worst;
            result.Anchor = worstName;

            long mirrored = 0;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int mirrorRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 want = pixels[row + x];
                    Color32 got = rendered[mirrorRow + x];
                    if (got.a > 8
                        && Math.Abs(want.r - got.r) <= 8
                        && Math.Abs(want.g - got.g) <= 8
                        && Math.Abs(want.b - got.b) <= 8)
                    {
                        mirrored++;
                    }
                }
            }
            result.FlippedRatio = (double)mirrored / total;

            long edge = 0;
            long edgeOk = 0;
            for (int x = 0; x < width; x++)
            {
                CountEdge(rendered[x], ref edge, ref edgeOk);
                CountEdge(rendered[(height - 1) * width + x], ref edge, ref edgeOk);
            }
            for (int y = 0; y < height; y++)
            {
                CountEdge(rendered[y * width], ref edge, ref edgeOk);
                CountEdge(rendered[y * width + width - 1], ref edge, ref edgeOk);
            }
            result.FrameCoverage = (double)edgeOk / Math.Max(1, edge);

            // corrimiento: 169 offsets x N pixeles. Se corre siempre que el tamano lo permita: es
            // la unica senal que distingue "corrido" de "recortado" cuando el marco se sale del borde.
            if (total <= 1200000)
            {
                int bestX = 0;
                int bestY = 0;
                double bestRatio = 0;
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }
                        long hits = 0;
                        long compared = 0;
                        int startY = Math.Max(0, dy);
                        int endY = Math.Min(height, height + dy);
                        int startX = Math.Max(0, dx);
                        int endX = Math.Min(width, width + dx);
                        for (int y = startY; y < endY; y++)
                        {
                            int row = y * width;
                            int sourceRow = (y - dy) * width;
                            for (int x = startX; x < endX; x++)
                            {
                                // solo pixeles estructurados: si se cuentan los de fondo, cualquier
                                // corrimiento "matchea" porque el fondo es uniforme.
                                if (expected.Classes[row + x] == ClassBg)
                                {
                                    continue;
                                }
                                compared++;
                                Color32 want = pixels[row + x];
                                Color32 got = rendered[sourceRow + (x - dx)];
                                if (got.a > 8
                                    && Math.Abs(want.r - got.r) <= 8
                                    && Math.Abs(want.g - got.g) <= 8
                                    && Math.Abs(want.b - got.b) <= 8)
                                {
                                    hits++;
                                }
                            }
                        }
                        double ratio = compared > 0 ? (double)hits / compared : 0;
                        if (ratio > bestRatio)
                        {
                            bestRatio = ratio;
                            bestX = dx;
                            bestY = dy;
                        }
                    }
                }
                result.OffsetRatio = bestRatio;
                result.OffsetX = bestX;
                result.OffsetY = bestY;
            }

            if (result.ExactRatio >= 0.995 && result.FrameCoverage >= 0.98 && result.CoverageFull)
            {
                result.Verdict = SyntheticVerdict.Exact;
                result.Reason = "1:1 exacto: los 4 bordes del marco estan en el borde del lienzo";
            }
            else if (result.OffsetRatio >= 0.95 && (result.OffsetX != 0 || result.OffsetY != 0)
                     && result.FrameCoverage < 0.98)
            {
                result.Verdict = SyntheticVerdict.Offset;
                result.Reason = "el patron completo aparece corrido (" + result.OffsetX + "," + result.OffsetY +
                                ") con " + (result.OffsetRatio * 100.0).ToString("0.00", CultureInfo.InvariantCulture) +
                                "% de match: la pieza se dibuja desplazada, no recortada";
            }
            else if (result.FrameCoverage < 0.98)
            {
                result.Verdict = SyntheticVerdict.Cropped;
                result.Reason = "el marco no toca los 4 bordes (" + (result.FrameCoverage * 100.0).ToString("0.00",
                    CultureInfo.InvariantCulture) + "%): la pieza se dibuja recortada o escalada";
            }
            else if (result.FlippedRatio >= 0.995)
            {
                result.Verdict = SyntheticVerdict.Flipped;
                result.Reason = "la imagen coincide espejo vertical (" +
                                (result.FlippedRatio * 100.0).ToString("0.00", CultureInfo.InvariantCulture) +
                                "%): el sprite sale invertido en Y, no es un crop";
            }
            else if (result.ClassAgreement >= 0.93 && result.ClassDelta <= TintDelta)
            {
                result.Verdict = SyntheticVerdict.Exact;
                result.Reason = "geometria y color dentro de tolerancia: marco 100%, clases " +
                                (result.ClassAgreement * 100.0).ToString("0.00", CultureInfo.InvariantCulture) +
                                "%, ancla " + result.Anchor + " delta " +
                                result.ClassDelta.ToString("0.0", CultureInfo.InvariantCulture) +
                                " (tolerancia " + TintDelta + ", absorbe la perdida de compresion)";
            }
            else if (result.ClassAgreement >= 0.93)
            {
                result.Verdict = SyntheticVerdict.Tinted;
                result.Reason = "la estructura coincide (" + (result.ClassAgreement * 100.0).ToString("0.00",
                    CultureInfo.InvariantCulture) + "%) pero el color no: ancla " + result.Anchor + " delta " +
                                result.ClassDelta.ToString("0.0", CultureInfo.InvariantCulture) +
                                " > " + TintDelta + " (tinte o conversion de color)";
            }
            else
            {
                result.Verdict = SyntheticVerdict.Mismatch;
                result.Reason = "el render no coincide con el patron (exacto " +
                                (result.ExactRatio * 100.0).ToString("0.00", CultureInfo.InvariantCulture) +
                                "%, clases " + (result.ClassAgreement * 100.0).ToString("0.00",
                                    CultureInfo.InvariantCulture) + "%)";
            }
            return result;
        }

        static void CountEdge(Color32 pixel, ref long edge, ref long edgeOk)
        {
            edge++;
            if (IsFrameLike(pixel))
            {
                edgeOk++;
            }
        }

        static string ClassName(int klass)
        {
            switch (klass)
            {
                case ClassFrame:
                    return "marco";
                case ClassCorner:
                    return "esquina";
                case ClassRing:
                    return "anillo";
                case ClassCross:
                    return "cruz";
                case ClassDiagonal:
                    return "diagonal";
                default:
                    return "fondo";
            }
        }

        static bool IsFrameLike(Color32 pixel)
        {
            // Magenta dominante, con tolerancia para tinte (chaos) y para el blending del bilinear.
            return pixel.a > 8 && pixel.r >= 120 && pixel.b >= 120
                && pixel.r > pixel.g + 40 && pixel.b > pixel.g + 40;
        }

        static byte NearestClass(Color32 pixel)
        {
            if (pixel.a <= 8)
            {
                return ClassUncovered;
            }
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < ClassColors.Length; i++)
            {
                Color32 candidate = ClassColors[i];
                int dr = pixel.r - candidate.r;
                int dg = pixel.g - candidate.g;
                int db = pixel.b - candidate.b;
                int distance = dr * dr + dg * dg + db * db;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return (byte)best;
        }

        // ------------------------------------------------------------------ Io

        static void WriteBoth(string assetAbsolute, string metaAbsolute, byte[] png, byte[] meta)
        {
            File.WriteAllBytes(assetAbsolute, png);
            File.WriteAllBytes(metaAbsolute, meta);
        }

        static void ImportAsset(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        static bool WaitForTexture(string assetPath, int width, int height, int timeoutMs)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null && texture.width == width && texture.height == height)
                {
                    return true;
                }
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                System.Threading.Thread.Sleep(100);
            }
            return false;
        }

        static Fingerprint FingerprintOf(byte[] png, byte[] meta)
        {
            return new Fingerprint
            {
                PngSha = Sha256(png),
                MetaSha = Sha256(meta),
                PngLength = png.LongLength,
                MetaLength = meta.LongLength,
            };
        }

        static string Sha256(byte[] bytes)
        {
            using (SHA256 hasher = SHA256.Create())
            {
                byte[] digest = hasher.ComputeHash(bytes);
                var text = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                {
                    text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        static bool TryReadPngSize(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes == null || bytes.Length < 24)
            {
                return false;
            }
            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            {
                return false;
            }
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return width > 0 && height > 0;
        }

        // ----------------------------------------------------------------- guard

        /// <summary>Fuente de este mismo harness: el PR lo revisa sin commitear, y ningun run lo escribe.</summary>
        static readonly string[] SelfSources =
        {
            "Assets/Editor/ArtContract/ArtSyntheticBatch.cs",
            "Assets/Editor/ArtContract/ArtSyntheticBatch.cs.meta",
        };

        /// <summary>Niveles del guard. Devuelve 0 si puede correr; 3 si se niega.</summary>
        static int CheckGuard(bool apply, bool gitOk, string gitError, string porcelain, List<Target> targets)
        {
            if (!gitOk)
            {
                Debug.LogWarning("[ArtSyntheticBatch] no se pudo leer `git status --porcelain -- Assets`: " + gitError);
                Debug.Log("[ArtSyntheticBatch] RECHAZADO: sin git no hay prueba de que el arbol vuelva a su estado inicial.");
                return 3;
            }

            List<string> dirty = ParsePorcelainPaths(porcelain);
            if (dirty.Count == 0)
            {
                Debug.Log("[ArtSyntheticBatch] guard ok: `git status --porcelain -- Assets` vacio");
                return 0;
            }

            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Target target in targets)
            {
                targetPaths.Add(target.AssetPath);
                targetPaths.Add(target.MetaPath);
            }

            var offenders = new List<string>();
            var exempt = new List<string>();
            foreach (string path in dirty)
            {
                bool isSelfSource = Array.Exists(SelfSources, s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase));
                bool isTarget = targetPaths.Contains(path);
                if (isTarget)
                {
                    offenders.Add("[TARGET] " + path);
                }
                else if (isSelfSource)
                {
                    exempt.Add(path);
                }
                else
                {
                    offenders.Add("[AJENO] " + path);
                }
            }

            if (exempt.Count > 0)
            {
                Debug.Log("[ArtSyntheticBatch] guard: exento el fuente del propio harness (PR en curso, no lo escribe el run): " +
                          string.Join(", ", exempt.ToArray()));
            }
            if (offenders.Count > 0)
            {
                Debug.LogWarning("[ArtSyntheticBatch] Assets/ tiene cambios sin commitear en paths que importan:\n" +
                                 Indent(string.Join("\n", offenders.ToArray())));
                Debug.Log("[ArtSyntheticBatch] RECHAZADO: el harness no corre con Assets/ sucio" +
                          (apply ? " y menos en apply" : string.Empty) + ".");
                return 3;
            }

            return 0;
        }

        static List<string> ParsePorcelainPaths(string porcelain)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(porcelain))
            {
                return paths;
            }
            string[] lines = porcelain.Replace("\r", string.Empty).Split('\n');
            foreach (string line in lines)
            {
                if (line.Trim().Length < 4)
                {
                    continue;
                }
                string path = line.Substring(3).Trim();
                int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    path = path.Substring(arrow + 4).Trim();
                }
                path = path.Trim('"').Replace('\\', '/');
                if (path.Length > 0)
                {
                    paths.Add(path);
                }
            }
            return paths;
        }

        static bool TryReadDirtyAssets(out string porcelain, out string error)
        {
            porcelain = null;
            error = null;
            var start = new ProcessStartInfo("git", "status --porcelain -- Assets")
            {
                WorkingDirectory = ArtContractManifest.ProjectRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        error = "no se pudo iniciar git";
                        return false;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = "git status salio con " + process.ExitCode + ": " + stderr.Trim();
                        return false;
                    }
                    // TrimEnd, no Trim: el primer path de `git status --porcelain` arranca con
                    // un espacio (" M Assets/...") y `Trim()` se lo comia, perdiendo el primer
                    // caracter (`ssets/...`) y rompiendo la exencion del propio fuente (F-S2a-3).
                    porcelain = output.TrimEnd();
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        // ---------------------------------------------------------------- reporte

        static string WriteReport(string stamp, Options options, List<Outcome> outcomes, List<string> hardFailures,
            bool gitAvailable, string gitBefore, string gitAfter, bool treeRestored)
        {
            string directory = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), ReportDir);
            Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "art-synthetic-batch-" + stamp + ".md");

            int exact = 0;
            int defects = 0;
            int skipped = 0;
            int errors = 0;
            int restored = 0;
            foreach (Outcome outcome in outcomes)
            {
                if (outcome.After != null)
                {
                    restored++;
                }
                switch (outcome.Verdict)
                {
                    case SyntheticVerdict.Exact:
                        exact++;
                        break;
                    case SyntheticVerdict.Skipped:
                        skipped++;
                        break;
                    case SyntheticVerdict.Error:
                        errors++;
                        break;
                    default:
                        defects++;
                        break;
                }
            }

            var text = new StringBuilder();
            text.AppendLine("# ArtSyntheticBatch — " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("| Campo | Valor |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Modo | APPLY (bytes pisados y restaurados) |");
            text.AppendLine("| Cohorte | " + Cell(Describe(options)) + " |");
            text.AppendLine("| Patron | `" + PatternVersion + "` |");
            text.AppendLine("| Piezas | " + outcomes.Count + " |");
            text.AppendLine("| exact | " + exact + " |");
            text.AppendLine("| defectos (cropped/offset/tinted/mismatch) | " + defects + " |");
            text.AppendLine("| skipped | " + skipped + " |");
            text.AppendLine("| errores | " + errors + " |");
            text.AppendLine("| git antes | " + Cell(string.IsNullOrEmpty(gitBefore) ? "limpio" : gitBefore) + " |");
            text.AppendLine("| git despues | " + Cell(string.IsNullOrEmpty(gitAfter) ? "limpio" : gitAfter) + " |");
            text.AppendLine("| arbol restaurado | " + (treeRestored ? "si" : "NO") + " |");
            text.AppendLine("| restauracion byte-identica | " +
                            (hardFailures.Count == 0
                                ? "si, en las " + restored + " piezas tocadas"
                                : "NO: ver fallas duras") + " |");
            text.AppendLine();
            text.AppendLine("## Veredicto por pieza");
            text.AppendLine();
            text.AppendLine("| Pieza | px | veredicto | rect | textura | marco | exacto(tol8) | notas |");
            text.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (Outcome outcome in outcomes)
            {
                Target target = outcome.Target;
                string rect = outcome.SpriteRectW > 0
                    ? outcome.SpriteRectX + "," + outcome.SpriteRectY + " " + outcome.SpriteRectW + "x" + outcome.SpriteRectH
                    : "-";
                string texture = outcome.TextureW > 0 ? outcome.TextureW + "x" + outcome.TextureH : "-";
                string frame = outcome.Compare == null
                    ? "-"
                    : (outcome.Compare.FrameCoverage * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%";
                string exactRatio = outcome.Compare == null
                    ? "-"
                    : (outcome.Compare.ExactRatio * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%";
                text.AppendLine("| `" + target.Pieza + "` | " + target.ExpectedW + "x" + target.ExpectedH + " | **" +
                                outcome.Verdict + "** | " + rect + " | " + texture + " | " + frame + " | " + exactRatio +
                                " | " + Cell(outcome.Reason ?? string.Empty) + " |");
            }

            text.AppendLine();
            text.AppendLine("## Detalle por pieza");
            foreach (Outcome outcome in outcomes)
            {
                Target target = outcome.Target;
                text.AppendLine();
                text.AppendLine("### " + target.Pieza);
                text.AppendLine();
                text.AppendLine("- origen: `" + target.AssetPath + "` a " + target.ExpectedW + "x" + target.ExpectedH +
                                " (" + target.ExpectedSource + ")");
                if (!string.IsNullOrEmpty(target.SkipReason))
                {
                    text.AppendLine("- skip: " + target.SkipReason);
                    continue;
                }
                text.AppendLine("- veredicto: **" + outcome.Verdict + "** — " + outcome.Reason);
                text.AppendLine("- restore-first byte-identico: " + (outcome.RestoreFirstOk ? "si" : "NO"));
                text.AppendLine("- sha256 png antes: `" + (outcome.Before == null ? "-" : outcome.Before.PngSha) + "`");
                text.AppendLine("- sha256 png despues: `" + (outcome.After == null ? "-" : outcome.After.PngSha) + "`");
                text.AppendLine("- sha256 meta antes: `" + (outcome.Before == null ? "-" : outcome.Before.MetaSha) + "`");
                text.AppendLine("- sha256 meta despues: `" + (outcome.After == null ? "-" : outcome.After.MetaSha) + "`");
                text.AppendLine("- restauracion identica: " + (outcome.RestoreIdentical ? "si" : "NO"));
                text.AppendLine("- geometria importada: " + (outcome.Geometry ?? "-"));
                if (outcome.Compare != null)
                {
                    text.AppendLine("- comparacion: " + outcome.Compare.Summary());
                }
                if (!string.IsNullOrEmpty(outcome.EvidenceDir))
                {
                    text.AppendLine("- evidencia: `" + ArtContractManifest.ToProjectPath(outcome.EvidenceDir) +
                                    "` (esperado.png / render.png / diff.png)");
                }
            }

            if (hardFailures.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Fallas duras (restauracion)");
                foreach (string failure in hardFailures)
                {
                    text.AppendLine("- " + failure);
                }
            }

            File.WriteAllText(path, text.ToString(), Utf8NoBom);
            return ArtContractManifest.ToProjectPath(path);
        }

        static void LogSummary(List<Outcome> outcomes, List<string> hardFailures, bool treeRestored, string reportPath,
            int exitCode)
        {
            foreach (Outcome outcome in outcomes)
            {
                if (outcome.Verdict == SyntheticVerdict.Skipped)
                {
                    continue;
                }
                string hashes = outcome.Before == null || outcome.After == null
                    ? "sin hashes"
                    : "png " + outcome.Before.PngSha + " -> " + outcome.After.PngSha +
                      " · meta " + outcome.Before.MetaSha + " -> " + outcome.After.MetaSha;
                Debug.Log("[ArtSyntheticBatch] " + outcome.Verdict.ToString().ToUpperInvariant() + " " +
                          outcome.Target.Pieza + " · " + outcome.Reason);
                Debug.Log("[ArtSyntheticBatch]   " + hashes +
                          (outcome.RestoreIdentical ? " · restauracion byte-identica" : " · RESTAURACION DISTINTA"));
            }
            if (hardFailures.Count > 0)
            {
                Debug.LogError("[ArtSyntheticBatch] fallas duras: " + string.Join(" | ", hardFailures.ToArray()));
            }
            Debug.Log("[ArtSyntheticBatch] arbol de Assets/ restaurado: " + (treeRestored ? "si" : "NO") +
                      " · reporte: " + reportPath + " · exit " + exitCode);
        }

        static string Cell(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", "; ");
        }

        static string Pad(string value, int width)
        {
            string text = value ?? string.Empty;
            return text.Length >= width ? text : text + new string(' ', width - text.Length);
        }

        static string SafeName(string pieza)
        {
            string name = pieza ?? "pieza";
            return name.Replace(".png", string.Empty).Replace('.', '-').Replace(' ', '_');
        }

        static string Indent(string text)
        {
            return "  " + (text ?? string.Empty).Replace("\r", string.Empty).Replace("\n", "\n  ");
        }

        static void Log(string message)
        {
            Debug.Log("[ArtSyntheticBatch] " + message);
        }
    }
}
#endif
