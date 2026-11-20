// ArtMetaMigrator: unico escritor del contrato de arte (sec.9 del manifiesto v2).
//
// Por que existe: 25 de las 85 piezas REPLACE importan con spriteMode 2 y 9 de ellas
// acumulan 10 rects que no coinciden con el lienzo del archivo (§3 del manifiesto).
// Pasar el .meta a Single es lo unico que hace seguro el swap de bytes a cualquier px
// (ASC-2), pero el flip NO se puede separar del remapeo de referencias: el fileID de
// un sub-sprite es un hash del nombre y solo es unico dentro de su GUID, asi que
// cualquier referencia a `fileID: <viejo>, guid: <g>` queda colgada si el flip va solo.
//
// Reglas duras de este archivo:
//   * Es el UNICO lugar que escribe .meta y referencias de sprite. tools/art/audit_art_manifest.py
//     es de solo lectura y su `--plan` es un espejo de este plan.
//   * NUNCA se ejecuta solo: no hay [InitializeOnLoad], ni [DidReloadScripts], ni
//     AssetPostprocessor. Solo menu o -executeMethod.
//   * Dry-run por defecto. `apply` es un parametro explicito.
//   * Guard: con el arbol sucio en Assets/ se niega a aplicar. El rollback documentado
//     es `git checkout -- Assets`, y eso borra trabajo ajeno si ya habia cambios.
//
// Uso:
//   Tools > Art Contract > Meta Migrator > Print Plan — all / — S2a (dry-run)
//   Tools > Art Contract > Meta Migrator > Apply Plan — all (escribe)
//   DryRun(cohort, pieces) / Apply(cohort, pieces, allowDirty) desde otro editor script
//   Unity -batchmode -quit -executeMethod ...BatchDryRun -artCohort s2a | -artPieces "game_board_*"
//
// Seleccion (design §10: S2 se parte por grupo de assets, no por tipo de cambio):
//   cohort = all | s2a | s2b | s2c   (s2a = game_board_*, s2b = avatares+heroes,
//   s2c = el resto). `pieces` (glob separado por comas) reemplaza al cohorte, igual que
//   ArtSyntheticBatch.SelectTargets. NO hay default: sin cohorte ni pieces se aborta (2).
//   OJO: el cohorte `world` del harness sintetico NO es s2a — mete ball_shadow_01, que es s2c.
//
// Orden de apply por pieza (una sola operacion, en este orden):
//   1. rect-resync (solo si la pieza queda en Multiple; ver BuildPlan)
//   2. spriteImportMode = Single
//   3. fileID remap GUID-scoped -> 21300000 en los YAML consumidores
//   4. spriteBorder desde sec.9
//   5. maxTextureSize desde sec.9
// El paso 2 reimporta antes del 3 (es el orden que pide el contrato), pero la ventana
// sin referencias no existe en la practica: los dos pasos corren dentro del mismo
// apply de la misma pieza y no hay ningun import intermedio que lea esas referencias.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkVoid.ArtContract
{
    public static class ArtMetaMigrator
    {
        public const string ManifestPath = "Docs/art/manifest-refresh-visual-v1.md";
        public const string ReportDir = "Temp/art-contract/reports";

        const string MenuRoot = "Tools/Art Contract/Meta Migrator/";
        const long SingleSpriteFileId = 21300000;
        const int ExpectedRows = 122;
        const int SpriteModeSingle = 1;
        const int SpriteModeMultiple = 2;

        static readonly string[] ConsumerSuffixes = { ".unity", ".prefab", ".asset", ".mat", ".controller" };
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        static readonly Regex RefRegex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})");
        static readonly Regex TableIdRegex = new Regex(@"^\s+213: (-?\d+)\s*$", RegexOptions.Multiline);

        // ------------------------------------------------------------------ menu

        [MenuItem(MenuRoot + "Print Plan — all (dry-run)", false, 2000)]
        public static void PrintPlanMenu()
        {
            DryRun("all", null);
        }

        [MenuItem(MenuRoot + "Print Plan — S2a game_board_* (dry-run)", false, 2001)]
        public static void PrintPlanS2aMenu()
        {
            DryRun("s2a", null);
        }

        [MenuItem(MenuRoot + "Apply Plan — all (escribe)", false, 2010)]
        public static void ApplyPlanMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "ArtMetaMigrator",
                "Vas a reescribir .meta y referencias fileID de TODAS las piezas planificadas (cohort=all).\n\n" +
                "Precondicion: arbol limpio en Assets/. El dry-run del cohorte ya tiene que estar revisado.\n" +
                "Para un solo grupo usar DryRun/Apply con cohorte (s2a|s2b|s2c) o `pieces`.",
                "Aplicar",
                "Cancelar");
            if (confirmed)
            {
                Apply("all", null, false);
            }
        }

        // ------------------------------------------------------------- entrypoints
        //
        // Seleccion obligatoria: no hay default silencioso. Sin cohorte ni `pieces` se
        // aborta con 2, aunque el plan de `all` sea el historico de 25 piezas / 52 refs.

        /// <summary>Imprime el plan del subconjunto y no escribe nada. Devuelve 0 si pudo planificar.</summary>
        public static int DryRun(string cohort, string pieces)
        {
            return Run(false, false, cohort, pieces);
        }

        /// <summary>Aplica el plan del subconjunto. allowDirty solo existe para el harness sintetico, nunca para un humano.</summary>
        public static int Apply(string cohort, string pieces, bool allowDirty)
        {
            return Run(true, allowDirty, cohort, pieces);
        }

        /// <summary>Entrada para -batchmode -executeMethod. Seleccion por `-artCohort` / `-artPieces`.</summary>
        public static void BatchDryRun()
        {
            int code = Run(false, false, BatchArg("-artCohort"), BatchArg("-artPieces"));
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        /// <summary>Entrada para -batchmode -executeMethod. Seleccion por `-artCohort` / `-artPieces`.</summary>
        public static void BatchApply()
        {
            int code = Run(true, false, BatchArg("-artCohort"), BatchArg("-artPieces"));
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        static string BatchArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.FindIndex(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        // ------------------------------------------------------------------- run

        public static int Run(bool apply, bool allowDirty, string cohort, string pieces)
        {
            Log("<b>ArtMetaMigrator</b> " + (apply ? "APPLY" : "DRY-RUN") + " · manifiesto " + ManifestPath +
                " · seleccion " + DescribeSelection(cohort, pieces));

            string porcelain;
            string gitError;
            bool gitOk = TryReadDirtyAssets(out porcelain, out gitError);
            bool dirty = gitOk && !string.IsNullOrEmpty(porcelain);
            if (!gitOk)
            {
                LogWarning("no se pudo leer `git status --porcelain -- Assets`: " + gitError +
                           "\n  -> el guard no puede garantizar un rollback limpio" + (apply && !allowDirty ? ": se aborta" : ""));
                if (apply && !allowDirty)
                {
                    return 3;
                }
            }
            else if (dirty)
            {
                LogWarning("Assets/ tiene cambios sin commitear:\n" + Indent(porcelain) +
                           "\n  -> el rollback (`git checkout -- Assets`) borraria ese trabajo");
                if (apply && !allowDirty)
                {
                    LogError("apply rechazado por el guard (allowDirty=false). Commitear o stashear primero.");
                    return 3;
                }
            }
            else
            {
                Log("guard ok: `git status --porcelain -- Assets` vacio");
            }

            List<Row> rows;
            try
            {
                rows = LoadManifest();
            }
            catch (Exception error)
            {
                LogError("no se pudo leer el manifiesto: " + error.Message);
                return 2;
            }

            List<PiecePlan> plan;
            try
            {
                plan = BuildPlan(SelectRows(rows, cohort, pieces));
            }
            catch (Exception error)
            {
                LogError("no se pudo construir el plan: " + error.Message);
                return 2;
            }

            PrintPlan(plan, DescribeGitState(dirty, gitOk, porcelain), DescribeSelection(cohort, pieces));

            if (!apply)
            {
                Log("dry-run: 0 escrituras (ni .meta, ni YAML, ni reporte). Usar Apply/ApplyBatch para ejecutar.");
                return 0;
            }

            if (plan.Count == 0)
            {
                Log("nada que aplicar: el contrato ya se cumple en la seleccion.");
                return 0;
            }

            return Execute(plan);
        }

        static string DescribeGitState(bool dirty, bool gitOk, string porcelain)
        {
            if (!gitOk)
            {
                return "no verificable";
            }
            return dirty ? porcelain.Replace("\r", string.Empty).Replace("\n", "; ") : "limpio";
        }

        // --------------------------------------------------------------- seleccion

        static string DescribeSelection(string cohort, string pieces)
        {
            if (!string.IsNullOrEmpty(pieces) && pieces.Trim().Length > 0)
            {
                return "pieces=" + pieces.Trim();
            }
            return "cohort=" + (string.IsNullOrEmpty(cohort) || cohort.Trim().Length == 0 ? "(ninguno)" : cohort.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Filtra las filas REPLACE antes de planificar. Es el unico punto donde se decide el
        /// alcance: BuildPlan y Execute no saben de cohortes, solo reciben menos filas. Un glob
        /// que no matchea nada aborta (no hay plan vacio "exitoso" por error de tipeo).
        /// </summary>
        static List<Row> SelectRows(List<Row> rows, string cohort, string pieces)
        {
            bool hasPieces = !string.IsNullOrEmpty(pieces) && pieces.Trim().Length > 0;
            string name = string.IsNullOrEmpty(cohort) ? null : cohort.Trim().ToLowerInvariant();
            if (!hasPieces && string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    "seleccion obligatoria: pasar cohorte (all|s2a|s2b|s2c) o `pieces` (glob). cohort=all aplica las 85 REPLACE.");
            }

            Regex pattern = hasPieces ? BuildGlob(pieces.Trim()) : null;
            var selected = new List<Row>();
            foreach (Row row in rows)
            {
                if (!string.Equals(row.Estado, "REPLACE", StringComparison.Ordinal))
                {
                    continue;
                }
                bool match = pattern != null
                    ? pattern.IsMatch(row.Pieza) || pattern.IsMatch(System.IO.Path.GetFileNameWithoutExtension(row.Origen ?? string.Empty))
                    : MatchesCohort(name, row);
                if (match)
                {
                    selected.Add(row);
                }
            }

            if (selected.Count == 0)
            {
                throw new InvalidOperationException(pattern != null
                    ? "`pieces` no matcheo ninguna pieza REPLACE: " + pieces
                    : "cohorte desconocido o vacio: " + cohort + " (validos: all, s2a, s2b, s2c)");
            }
            return selected;
        }

        static bool MatchesCohort(string cohort, Row row)
        {
            string pieza = row.Pieza ?? string.Empty;
            switch (cohort ?? string.Empty)
            {
                case "all":
                    return true;
                case "s2a":
                    return pieza.StartsWith("game_board_", StringComparison.Ordinal);
                case "s2b":
                    // avatares + heroes, menos el bg de dialogo (design §10: ese va con BG_V2 y sombra)
                    if (pieza.StartsWith("hero_dialog_bg_", StringComparison.Ordinal))
                    {
                        return false;
                    }
                    return pieza.StartsWith("hero_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_avatar_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_panel_profile_", StringComparison.Ordinal)
                        || pieza.StartsWith("ui_menu_picture_profile_", StringComparison.Ordinal);
                case "s2c":
                    return !MatchesCohort("s2a", row) && !MatchesCohort("s2b", row);
                default:
                    return false;
            }
        }

        static Regex BuildGlob(string pieces)
        {
            var builder = new StringBuilder("^(?:");
            foreach (string term in pieces.Split(','))
            {
                string trimmed = term.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }
                if (builder.Length > 4)
                {
                    builder.Append('|');
                }
                foreach (char character in trimmed)
                {
                    builder.Append(character == '*' ? ".*" : character == '?' ? "." : Regex.Escape(character.ToString()));
                }
            }
            builder.Append(")$");
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // -------------------------------------------------------------- manifiesto

        sealed class Row
        {
            public string Pieza;
            public string Estado;
            public string Origen;
            public string PxActual;
            public string ModeTarget;
            public string Border;
            public string MaxTex;
        }

        static List<Row> LoadManifest()
        {
            string manifest = System.IO.Path.Combine(ProjectRoot(), ManifestPath);
            if (!File.Exists(manifest))
            {
                throw new FileNotFoundException("falta el manifiesto", manifest);
            }

            string[] lines = File.ReadAllLines(manifest, Encoding.UTF8);
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd() == "```csv")
                {
                    start = i + 1;
                    break;
                }
            }
            if (start < 0)
            {
                throw new InvalidDataException("no hay bloque ```csv en " + ManifestPath);
            }

            var body = new List<string>();
            for (int i = start; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    break;
                }
                if (lines[i].Length > 0)
                {
                    body.Add(lines[i]);
                }
            }
            if (body.Count < 2)
            {
                throw new InvalidDataException("el bloque ```csv esta vacio");
            }

            string[] header = SplitCsv(body[0]);
            int iPieza = IndexOf(header, "pieza");
            int iEstado = IndexOf(header, "estado");
            int iOrigen = IndexOf(header, "origen");
            int iPxActual = IndexOf(header, "px_actual");
            int iModeTarget = IndexOf(header, "mode_target");
            int iBorder = IndexOf(header, "border");
            int iMaxTex = IndexOf(header, "max_tex");
            if (header.Length != 12 || iPieza < 0 || iEstado < 0 || iOrigen < 0 || iModeTarget < 0 ||
                iBorder < 0 || iMaxTex < 0 || iPxActual < 0)
            {
                throw new InvalidDataException("columnas inesperadas en sec.9: " + string.Join(",", header));
            }

            var rows = new List<Row>();
            for (int i = 1; i < body.Count; i++)
            {
                string[] cells = SplitCsv(body[i]);
                if (cells.Length != header.Length)
                {
                    throw new InvalidDataException("fila " + (i + 1) + " con " + cells.Length + " columnas");
                }
                rows.Add(new Row
                {
                    Pieza = cells[iPieza],
                    Estado = cells[iEstado],
                    Origen = cells[iOrigen],
                    PxActual = cells[iPxActual],
                    ModeTarget = cells[iModeTarget],
                    Border = cells[iBorder],
                    MaxTex = cells[iMaxTex],
                });
            }

            if (rows.Count != ExpectedRows)
            {
                throw new InvalidDataException("sec.9 tiene " + rows.Count + " filas, el contrato congela " + ExpectedRows);
            }
            return rows;
        }

        static string[] SplitCsv(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            foreach (char character in line)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (character == ',' && !quoted)
                {
                    cells.Add(current.ToString().Trim());
                    current.Length = 0;
                    continue;
                }
                current.Append(character);
            }
            cells.Add(current.ToString().Trim());
            return cells.ToArray();
        }

        static int IndexOf(string[] cells, string name)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (string.Equals(cells[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // ------------------------------------------------------------------ plan

        sealed class PlannedOp
        {
            public string Kind;
            public string Detail;
        }

        sealed class PiecePlan
        {
            public Row Row;
            public string AssetPath;
            public string MetaPath;
            public string Guid;
            public int ModeNow;
            public int ModeTarget;
            public int Width;
            public int Height;
            public bool Flip;
            public bool RectResync;
            public bool ApplyBorder;
            public Vector4 Border;
            public bool ApplyMaxTex;
            public int MaxTex;
            public HashSet<long> OldFileIds = new HashSet<long>();
            public HashSet<string> RefFiles = new HashSet<string>();
            public Dictionary<long, int> RefsById = new Dictionary<long, int>();
            public int RefCount;
            public List<PlannedOp> Ops = new List<PlannedOp>();
            public List<string> Notes = new List<string>();
        }

        static List<PiecePlan> BuildPlan(List<Row> rows)
        {
            var plans = new List<PiecePlan>();
            var seenAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byGuid = new Dictionary<string, PiecePlan>(StringComparer.Ordinal);

            foreach (Row row in rows)
            {
                if (!string.Equals(row.Estado, "REPLACE", StringComparison.Ordinal))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(row.Origen) || row.Origen == "-")
                {
                    LogError(row.Pieza + ": REPLACE sin origen");
                    continue;
                }
                if (!seenAssets.Add(row.Origen))
                {
                    Log("duplicado: " + row.Pieza + " comparte asset con otra fila (" + row.Origen + "), se planifica una vez");
                    continue;
                }

                var importer = AssetImporter.GetAtPath(row.Origen) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(row.Origen);
                if (importer == null || texture == null)
                {
                    LogError(row.Pieza + ": no se pudo abrir el importer de " + row.Origen);
                    continue;
                }

                int modeTarget;
                if (!int.TryParse(row.ModeTarget, NumberStyles.Integer, CultureInfo.InvariantCulture, out modeTarget))
                {
                    LogError(row.Pieza + ": mode_target '" + row.ModeTarget + "' no es numerico");
                    continue;
                }

                var plan = new PiecePlan
                {
                    Row = row,
                    AssetPath = row.Origen,
                    MetaPath = row.Origen + ".meta",
                    Width = texture.width,
                    Height = texture.height,
                    ModeNow = (int)importer.spriteImportMode,
                    ModeTarget = modeTarget,
                };

                Sheet sheet;
                try
                {
                    sheet = ParseSheet(File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), plan.MetaPath), Encoding.UTF8));
                }
                catch (Exception error)
                {
                    LogError(row.Pieza + ": no se pudo leer " + plan.MetaPath + " (" + error.Message + ")");
                    sheet = new Sheet();
                }
                plan.Guid = sheet.Guid;
                if (string.IsNullOrEmpty(plan.Guid))
                {
                    LogError(row.Pieza + ": el .meta no tiene guid");
                    continue;
                }

                bool staleRects = false;
                var staleDetail = new List<string>();
                foreach (SheetSprite sprite in sheet.Sprites)
                {
                    if (sprite.Rect.width <= 0f || sprite.Rect.height <= 0f)
                    {
                        continue; // entrada sin rect valido: no es un rect congelado
                    }
                    if (sprite.Rect != new Rect(0f, 0f, plan.Width, plan.Height))
                    {
                        staleRects = true;
                        staleDetail.Add(FormatSheetRect(sprite));
                    }
                }

                if (plan.ModeNow == plan.ModeTarget && staleRects && plan.ModeTarget == SpriteModeMultiple)
                {
                    plan.RectResync = true;
                    plan.Ops.Add(new PlannedOp
                    {
                        Kind = "rect-resync",
                        Detail = string.Join("; ", staleDetail) + " -> " + FormatRect(new Rect(0f, 0f, plan.Width, plan.Height)),
                    });
                }

                plan.Flip = plan.ModeNow != plan.ModeTarget;
                if (plan.Flip)
                {
                    plan.Ops.Add(new PlannedOp
                    {
                        Kind = "spriteMode",
                        Detail = plan.ModeNow + " -> " + plan.ModeTarget,
                    });
                    plan.OldFileIds = new HashSet<long>(sheet.InternalIds);
                    byGuid[plan.Guid] = plan;
                }

                if (staleRects && plan.Flip)
                {
                    foreach (string detail in staleDetail)
                    {
                        plan.Notes.Add(detail + " != lienzo " + plan.Width + "x" + plan.Height + ": se descarta con el flip");
                    }
                }

                Vector4 border;
                if (TryParseBorder(row.Border, out border) && !SameVector(importer.spriteBorder, border))
                {
                    plan.ApplyBorder = true;
                    plan.Border = border;
                    plan.Ops.Add(new PlannedOp
                    {
                        Kind = "border",
                        Detail = FormatVector(importer.spriteBorder) + " -> " + row.Border,
                    });
                }

                int maxTex;
                if (int.TryParse(row.MaxTex, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxTex) &&
                    importer.maxTextureSize != maxTex)
                {
                    plan.ApplyMaxTex = true;
                    plan.MaxTex = maxTex;
                    plan.Ops.Add(new PlannedOp
                    {
                        Kind = "maxTextureSize",
                        Detail = importer.maxTextureSize + " -> " + maxTex,
                    });
                }

                plans.Add(plan);
            }

            if (byGuid.Count > 0)
            {
                ScanRefs(byGuid);
                foreach (PiecePlan plan in byGuid.Values)
                {
                    if (plan.RefCount == 0)
                    {
                        plan.Notes.Add("sin referencias a sus fileIDs: el flip no deja nada colgado");
                        continue;
                    }
                    var ids = new List<long>(plan.RefsById.Keys);
                    ids.Sort();
                    foreach (long id in ids)
                    {
                        plan.Ops.Add(new PlannedOp
                        {
                            Kind = "fileID-remap",
                            Detail = FormatRemap(id, plan.Guid) + " (" + plan.RefsById[id] + " ocurrencias, " +
                                     plan.RefFiles.Count + " archivos)",
                        });
                    }
                }
            }

            // orden de impresion: modo/rect primero, remapeo despues, bordes al final
            foreach (PiecePlan plan in plans)
            {
                plan.Ops.Sort((left, right) => Rank(left.Kind).CompareTo(Rank(right.Kind)));
            }
            return plans;
        }

        static int Rank(string kind)
        {
            switch (kind)
            {
                case "rect-resync": return 0;
                case "spriteMode": return 1;
                case "fileID-remap": return 2;
                case "border": return 3;
                case "maxTextureSize": return 4;
                default: return 9;
            }
        }

        static void ScanRefs(IDictionary<string, PiecePlan> byGuid)
        {
            foreach (string file in ConsumerFiles())
            {
                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (Exception error)
                {
                    LogWarning("no se pudo leer " + file + ": " + error.Message);
                    continue;
                }
                if (text.IndexOf("guid:", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                foreach (Match match in RefRegex.Matches(text))
                {
                    PiecePlan plan;
                    if (!byGuid.TryGetValue(match.Groups[2].Value, out plan))
                    {
                        continue;
                    }
                    long fileId;
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId))
                    {
                        continue;
                    }
                    if (!plan.OldFileIds.Contains(fileId))
                    {
                        continue;
                    }
                    plan.RefCount++;
                    int seen;
                    plan.RefsById.TryGetValue(fileId, out seen);
                    plan.RefsById[fileId] = seen + 1;
                    plan.RefFiles.Add(ToProjectPath(file));
                }
            }
        }

        // ----------------------------------------------------------------- print

        static void PrintPlan(List<PiecePlan> plans, string gitState, string selection)
        {
            int planned = 0;
            int refs = 0;
            int unchanged = 0;

            var buffer = new StringBuilder();
            foreach (PiecePlan plan in plans)
            {
                if (plan.Ops.Count == 0)
                {
                    unchanged++;
                    continue;
                }
                planned++;
                refs += plan.RefCount;

                buffer.AppendLine("PLAN  " + plan.Row.Pieza);
                buffer.AppendLine("      asset   " + plan.AssetPath + "  (guid " + plan.Guid + ")");
                for (int i = 0; i < plan.Ops.Count; i++)
                {
                    buffer.AppendLine(string.Format(
                        CultureInfo.InvariantCulture, "      op{0,-2}   {1,-14} {2}", i + 1, plan.Ops[i].Kind, plan.Ops[i].Detail));
                }
                foreach (string note in plan.Notes)
                {
                    buffer.AppendLine("      nota  " + note);
                }
                buffer.AppendLine();
            }

            if (buffer.Length > 0)
            {
                Debug.Log("[ArtMetaMigrator]\n" + buffer.ToString().TrimEnd());
            }

            Log("guard Assets/: " + gitState);
            Log("seleccion: " + selection + " | piezas=" + planned + " | refs=" + refs + " | filas=" + plans.Count);
            Log("piezas seleccionadas: " + string.Join(", ", plans.ConvertAll(plan => plan.Row.Pieza).ToArray()));
            Log("TOTAL " + planned + " piezas con operaciones planificadas | " + refs + " ocurrencias de fileID a remapear");
            if (unchanged > 0)
            {
                Log(unchanged + " piezas ya conformes (sin operaciones)");
            }
        }

        // --------------------------------------------------------------- execute

        static int Execute(List<PiecePlan> plans)
        {
            var toApply = plans.FindAll(plan => plan.Ops.Count > 0);
            if (toApply.Count == 0)
            {
                Log("nada que aplicar.");
                return 0;
            }

            Log("apply: " + toApply.Count + " piezas (solo el subconjunto seleccionado). Escenas y prefabs abiertos se van a recargar; conviene cerrarlos.");
            int metaWrites = 0;
            int filesWritten = 0;
            int refsRemapped = 0;
            var failures = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (PiecePlan plan in toApply)
                {
                    try
                    {
                        if (ApplyImporter(plan))
                        {
                            metaWrites++;
                        }
                        int touched;
                        int remapped;
                        RemapRefs(plan, out touched, out remapped);
                        filesWritten += touched;
                        refsRemapped += remapped;
                    }
                    catch (Exception error)
                    {
                        failures.Add(plan.Row.Pieza + ": " + error.Message);
                        LogError(plan.Row.Pieza + ": " + error);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            foreach (PiecePlan plan in toApply)
            {
                if (CountRemaining(plan) > 0)
                {
                    failures.Add(plan.Row.Pieza + ": quedan referencias a fileIDs retirados");
                }
            }

            string report = WriteReport(toApply, metaWrites, filesWritten, refsRemapped, failures);
            Log("apply: resumen en " + report);
            Log("apply: " + metaWrites + " .meta reimportados, " + filesWritten + " archivos YAML reescritos, " +
                refsRemapped + " referencias remapeadas a " + SingleSpriteFileId);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    LogError(failure);
                }
                LogError("apply con " + failures.Count + " fallas: el contrato NO se cumple todavia");
                return 4;
            }

            Log("apply ok: correr tools/art/audit_art_manifest.py para confirmar (MODE-TARGET y RECT-STALE deben ir a 0)");
            return 0;
        }

        static bool ApplyImporter(PiecePlan plan)
        {
            if (!plan.Flip && !plan.RectResync && !plan.ApplyBorder && !plan.ApplyMaxTex)
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(plan.AssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("importer nulo para " + plan.AssetPath);
            }

            if (plan.RectResync)
            {
                SpriteMetaData[] sheet = importer.spritesheet;
                if (sheet != null)
                {
                    for (int i = 0; i < sheet.Length; i++)
                    {
                        SpriteMetaData sprite = sheet[i];
                        sprite.rect = new Rect(0f, 0f, plan.Width, plan.Height);
                        sheet[i] = sprite;
                    }
                    importer.spritesheet = sheet;
                }
            }

            if (plan.Flip)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }
            if (plan.ApplyBorder)
            {
                importer.spriteBorder = plan.Border;
            }
            if (plan.ApplyMaxTex)
            {
                importer.maxTextureSize = plan.MaxTex;
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Log("escrito " + plan.AssetPath + ".meta (" + string.Join(", ", plan.Ops.ConvertAll(op => op.Kind).ToArray()) + ")");
            return true;
        }

        static void RemapRefs(PiecePlan plan, out int filesWritten, out int refsRemapped)
        {
            filesWritten = 0;
            refsRemapped = 0;
            if (!plan.Flip || plan.OldFileIds.Count == 0 || plan.RefFiles.Count == 0)
            {
                return;
            }

            var regex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*" + plan.Guid);
            HashSet<long> oldIds = plan.OldFileIds;
            string guid = plan.Guid;
            int remapped = 0;
            MatchEvaluator evaluator = match =>
            {
                long fileId;
                if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId))
                {
                    return match.Value;
                }
                if (!oldIds.Contains(fileId))
                {
                    return match.Value;
                }
                remapped++;
                return "fileID: " + SingleSpriteFileId.ToString(CultureInfo.InvariantCulture) + ", guid: " + guid;
            };

            int written = 0;
            foreach (string projectPath in plan.RefFiles)
            {
                string absolute = System.IO.Path.Combine(ProjectRoot(), projectPath);
                string original = File.ReadAllText(absolute, Encoding.UTF8);
                string updated = regex.Replace(original, evaluator);
                if (string.Equals(updated, original, StringComparison.Ordinal))
                {
                    continue;
                }
                File.WriteAllText(absolute, updated, Utf8NoBom);
                written++;
                AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);
                Log("remapeado " + projectPath + " (" + plan.Row.Pieza + ")");
            }

            filesWritten = written;
            refsRemapped = remapped;
        }

        static int CountRemaining(PiecePlan plan)
        {
            int remaining = 0;
            if (plan.OldFileIds.Count == 0)
            {
                return 0;
            }
            var regex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*" + plan.Guid);
            foreach (string projectPath in plan.RefFiles)
            {
                string text = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), projectPath), Encoding.UTF8);
                foreach (Match match in regex.Matches(text))
                {
                    long fileId;
                    if (long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId) &&
                        plan.OldFileIds.Contains(fileId))
                    {
                        remaining++;
                    }
                }
            }
            return remaining;
        }

        static string WriteReport(List<PiecePlan> applied, int metaWrites, int filesWritten,
            int refsRemapped, List<string> failures)
        {
            string directory = System.IO.Path.Combine(ProjectRoot(), ReportDir);
            Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "art-meta-migrator-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".md");

            var text = new StringBuilder();
            text.AppendLine("# ArtMetaMigrator — " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("| Campo | Valor |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Manifiesto | `" + ManifestPath + "` |");
            text.AppendLine("| Piezas con operaciones | " + applied.Count + " |");
            text.AppendLine("| .meta reimportados | " + metaWrites + " |");
            text.AppendLine("| Archivos YAML reescritos | " + filesWritten + " |");
            text.AppendLine("| Referencias remapeadas | " + refsRemapped + " |");
            text.AppendLine("| Fallas | " + failures.Count + " |");
            text.AppendLine();
            text.AppendLine("## Operaciones");
            foreach (PiecePlan plan in applied)
            {
                text.AppendLine();
                text.AppendLine("### " + plan.Row.Pieza);
                foreach (PlannedOp op in plan.Ops)
                {
                    text.AppendLine("- `" + op.Kind + "` " + op.Detail);
                }
                foreach (string note in plan.Notes)
                {
                    text.AppendLine("- nota: " + note);
                }
            }
            if (failures.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Fallas");
                foreach (string failure in failures)
                {
                    text.AppendLine("- " + failure);
                }
            }

            File.WriteAllText(path, text.ToString(), Utf8NoBom);
            return ToProjectPath(path);
        }

        // ----------------------------------------------------------------- guard

        static bool TryReadDirtyAssets(out string porcelain, out string error)
        {
            porcelain = null;
            error = null;
            var start = new ProcessStartInfo("git", "status --porcelain -- Assets")
            {
                WorkingDirectory = ProjectRoot(),
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
                    porcelain = output.Trim();
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        // ---------------------------------------------------------------- util

        sealed class SheetSprite
        {
            public string Name;
            public Rect Rect;
        }

        sealed class Sheet
        {
            public string Guid;
            public List<SheetSprite> Sprites = new List<SheetSprite>();
            public List<long> InternalIds = new List<long>();
        }

        static Sheet ParseSheet(string meta)
        {
            var sheet = new Sheet();
            Match guid = Regex.Match(meta, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
            if (guid.Success)
            {
                sheet.Guid = guid.Groups[1].Value;
            }

            foreach (Match match in TableIdRegex.Matches(meta))
            {
                long id;
                if (long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) &&
                    !sheet.InternalIds.Contains(id))
                {
                    sheet.InternalIds.Add(id);
                }
            }

            string[] lines = meta.Replace("\r\n", "\n").Split('\n');
            int start = Array.IndexOf(lines, "  spriteSheet:");
            if (start < 0)
            {
                return sheet;
            }

            SheetSprite current = null;
            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length > 0 && !line.StartsWith(" ", StringComparison.Ordinal))
                {
                    break;
                }
                if (line.StartsWith("    - serializedVersion:", StringComparison.Ordinal))
                {
                    if (current != null)
                    {
                        sheet.Sprites.Add(current);
                    }
                    current = new SheetSprite();
                    continue;
                }
                if (current == null)
                {
                    continue;
                }
                if (line.StartsWith("      name:", StringComparison.Ordinal))
                {
                    current.Name = line.Substring("      name:".Length).Trim();
                    continue;
                }
                if (line.StartsWith("      internalID:", StringComparison.Ordinal))
                {
                    long internalId;
                    if (long.TryParse(line.Substring("      internalID:".Length).Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out internalId) && !sheet.InternalIds.Contains(internalId))
                    {
                        sheet.InternalIds.Add(internalId);
                    }
                    continue;
                }
                if (line.TrimEnd() == "rect:")
                {
                    int indent = line.Length - line.TrimStart(' ').Length;
                    float x = 0f, y = 0f, width = 0f, height = 0f;
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string inner = lines[j];
                        if (inner.Length == 0)
                        {
                            continue;
                        }
                        int innerIndent = inner.Length - inner.TrimStart(' ').Length;
                        if (innerIndent <= indent)
                        {
                            break;
                        }
                        string[] pair = inner.Trim().Split(new[] { ':' }, 2);
                        if (pair.Length != 2)
                        {
                            continue;
                        }
                        float value;
                        if (!float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        {
                            continue;
                        }
                        switch (pair[0])
                        {
                            case "x": x = value; break;
                            case "y": y = value; break;
                            case "width": width = value; break;
                            case "height": height = value; break;
                        }
                    }
                    current.Rect = new Rect(x, y, width, height);
                }
            }
            if (current != null)
            {
                sheet.Sprites.Add(current);
            }
            return sheet;
        }

        static IEnumerable<string> ConsumerFiles()
        {
            string assets = System.IO.Path.Combine(ProjectRoot(), "Assets");
            if (!Directory.Exists(assets))
            {
                yield break;
            }
            foreach (string file in Directory.GetFiles(assets, "*.*", SearchOption.AllDirectories))
            {
                string suffix = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(ConsumerSuffixes, suffix) < 0)
                {
                    continue;
                }
                if (file.Replace('\\', '/').IndexOf("/_Recovery/", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                yield return file;
            }
        }

        static bool TryParseBorder(string value, out Vector4 border)
        {
            border = Vector4.zero;
            if (string.IsNullOrEmpty(value) || value == "-")
            {
                return false;
            }
            string[] parts = value.Split('/');
            if (parts.Length != 4)
            {
                return false;
            }
            float[] numbers = new float[4];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    return false;
                }
            }
            border = new Vector4(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        static bool SameVector(Vector4 left, Vector4 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.01f && Mathf.Abs(left.y - right.y) < 0.01f &&
                   Mathf.Abs(left.z - right.z) < 0.01f && Mathf.Abs(left.w - right.w) < 0.01f;
        }

        static string FormatVector(Vector4 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0}/{1:0}/{2:0}/{3:0}", value.x, value.y, value.z, value.w);
        }

        static string FormatRect(Rect rect)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0},{1:0} {2:0}x{3:0}", rect.x, rect.y, rect.width, rect.height);
        }

        static string FormatSheetRect(SheetSprite sprite)
        {
            return "rect \"" + (string.IsNullOrEmpty(sprite.Name) ? "?" : sprite.Name) + "\" " + FormatRect(sprite.Rect);
        }

        static string FormatRemap(long fileId, string guid)
        {
            return "fileID " + fileId.ToString(CultureInfo.InvariantCulture) + ", guid " + guid + " -> " +
                   SingleSpriteFileId.ToString(CultureInfo.InvariantCulture) + ", guid " + guid;
        }

        static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        static string ToProjectPath(string absolute)
        {
            string root = ProjectRoot().Replace('\\', '/');
            return absolute.Replace('\\', '/').Replace(root + "/", string.Empty);
        }

        static string Indent(string text)
        {
            return "    " + text.Replace("\r", string.Empty).Replace("\n", "\n    ");
        }

        // ---------------------------------------------------------------- logging

        static void Log(string message)
        {
            Debug.Log("[ArtMetaMigrator] " + message);
        }

        static void LogWarning(string message)
        {
            Debug.LogWarning("[ArtMetaMigrator] " + message);
        }

        static void LogError(string message)
        {
            Debug.LogError("[ArtMetaMigrator] " + message);
        }
    }
}
#endif
