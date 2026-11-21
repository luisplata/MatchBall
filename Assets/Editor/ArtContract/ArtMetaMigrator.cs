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
//   cohort = all | s2a | s2b | s2c | new
//   (s2a = game_board_*, s2b = avatares+heroes, s2c = el resto, new = los placeholders
//   GENERATED de Docs/art/placeholders-v1.csv). `pieces` (glob separado por comas)
//   reemplaza al cohorte, igual que ArtSyntheticBatch.SelectTargets.
//   NO hay default: sin cohorte ni pieces se aborta (2).
//   OJO: el cohorte `world` del harness sintetico NO es s2a — mete ball_shadow_01, que es s2c.
//   OJO 2: `new` es el unico cohorte que lee el LEDGER y no la sec.9: la lista de bloqueadas
//   vive en un solo lugar (el generador), y las NEW no tienen `origen` en la sec.9.
//
// Guard v2 (obligatorio para `new`): el guard original aborta si `git status --porcelain --
// Assets` no esta vacio, y los 33 PNG recien generados lo estan. Se reemplaza por: todo path
// sucio tiene que pertenecer al conjunto {PNG, PNG.meta} del ledger; cualquier otro path sucio
// sigue abortando. Mantiene la promesa de rollback (`git checkout -- Assets` + borrar los
// untracked del ledger).
//
// Orden de apply por pieza (una sola operacion, en este orden):
//   1. rect-resync (solo si la pieza queda en Multiple; ver BuildPlan)
//   2. sheet-clear (solo si la pieza YA esta en Single con un bloque residual; ver BuildPlan)
//   3. spriteImportMode = Single
//   4. fileID remap GUID-scoped -> 21300000 en los YAML consumidores
//   5. spriteBorder desde sec.9
//   6. maxTextureSize desde sec.9
// El paso 3 reimporta antes del 4 (es el orden que pide el contrato), pero la ventana
// sin referencias no existe en la practica: los dos pasos corren dentro del mismo
// apply de la misma pieza y no hay ningun import intermedio que lea esas referencias.
//
// Op sheet-clear (S1b, CTR-2): 9 piezas que ya estan en Single (mode_target 1) conservan un
// bloque `spriteSheet.sprites[]` residual. En Single ese bloque no dibuja (el rect vivo ES el
// lienzo), pero sigue serializado: si alguna vez se vuelve a Multiple, el rect muerto revive.
// En Single no hay data provider que lo escriba, asi que la unica via es el round-trip
// Multiple -> SetSpriteRects(empty) -> Single -> SaveAndReimport. La verificacion RE-PARSEA el
// .meta: si queda una sola entrada se emite RECT-CLEAR-FAILED (nunca un log verde) y el apply
// devuelve 4. Este op no remapea referencias: el modo no cambia y el fileID vivo (21300000) es
// el mismo antes y despues.
// OJO: `grep -c spriteSheet` NO sirve como prueba de cierre en Unity 6 — el serializer escribe
// siempre la clave `spriteSheet:` en Single, con `sprites: []`. La prueba es `sprites: []`
// (cero entradas) y `nameFileIdTable: {}`, que es lo que cuenta SheetSpriteCount().

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
        public const string LedgerPath = "Docs/art/placeholders-v1.csv";
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
                "Para un solo grupo usar DryRun/Apply con cohorte (s2a|s2b|s2c|new) o `pieces`.",
                "Aplicar",
                "Cancelar");
            if (confirmed)
            {
                Apply("all", null, false);
            }
        }

        [MenuItem(MenuRoot + "Print Plan — new placeholders (dry-run)", false, 2002)]
        public static void PrintPlanNewMenu()
        {
            DryRun("new", null);
        }

        // R6: fallback cuando no hay licencia ni batchmode. Hace exactamente lo mismo que
        // `-executeMethod ...BatchApply -artCohort new`.
        [MenuItem(MenuRoot + "Apply Plan — new placeholders (escribe)", false, 2011)]
        public static void ApplyPlanNewMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "ArtMetaMigrator",
                "Vas a escribir el .meta de las piezas GENERATED de " + LedgerPath + " (cohort=new).\n\n" +
                "Precondicion: los PNG ya estan en Assets/Sprites/ y el arbol sucio solo los toca a ellos\n" +
                "(guard v2). Despues: python tools/art/generate_placeholders.py --freeze-guid",
                "Aplicar",
                "Cancelar");
            if (confirmed)
            {
                Apply("new", null, false);
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
                // Guard v2: lo sucio tiene que ser del cohorte declarado (PNG + .meta del ledger).
                HashSet<string> allowed = LedgerAllowlist();
                string offending;
                if (OnlyAllowedDirty(porcelain, allowed, out offending))
                {
                    Log("guard v2 ok: lo sucio en Assets/ es del cohorte (" + allowed.Count / 2 + " piezas del ledger)");
                }
                else
                {
                    LogWarning("Assets/ tiene cambios que no son del cohorte:\n" + Indent(porcelain) +
                               "\n  -> el rollback (`git checkout -- Assets`) borraria ese trabajo; primer path ajeno: " +
                               offending);
                    if (apply && !allowDirty)
                    {
                        LogError("apply rechazado por el guard (allowDirty=false). Commitear o stashear primero.");
                        return 3;
                    }
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

            List<LedgerRow> ledger;
            try
            {
                ledger = LoadLedger();
            }
            catch (Exception error)
            {
                LogError("no se pudo leer el ledger " + LedgerPath + ": " + error.Message);
                return 2;
            }

            List<PiecePlan> plan;
            try
            {
                List<Row> selected = SelectRows(rows, ledger, cohort, pieces);
                if (UsesCohort(cohort, pieces, "new"))
                {
                    // Los PNG recien escritos tienen que existir como assets para que haya
                    // TextureImporter y GUID: el generador no escribe .meta (D1/D8).
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                plan = BuildPlan(selected, ledger);
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
        /// Filtra las filas antes de planificar. Es el unico punto donde se decide el
        /// alcance: BuildPlan y Execute no saben de cohortes, solo reciben menos filas. Un glob
        /// que no matchea nada aborta (no hay plan vacio "exitoso" por error de tipeo).
        ///
        /// `new` es el unico cohorte que sale del ledger: las NEW no tienen `origen` en la sec.9
        /// (su path lo deriva el generador) y las bloqueadas no se importan nunca.
        /// </summary>
        static List<Row> SelectRows(List<Row> rows, List<LedgerRow> ledger, string cohort, string pieces)
        {
            bool hasPieces = !string.IsNullOrEmpty(pieces) && pieces.Trim().Length > 0;
            string name = string.IsNullOrEmpty(cohort) ? null : cohort.Trim().ToLowerInvariant();
            if (!hasPieces && string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    "seleccion obligatoria: pasar cohorte (all|s2a|s2b|s2c|new) o `pieces` (glob). " +
                    "cohort=all aplica las 85 REPLACE.");
            }

            if (string.Equals(name, "new", StringComparison.Ordinal) && !hasPieces)
            {
                var generated = new Dictionary<string, LedgerRow>(StringComparer.Ordinal);
                foreach (LedgerRow entry in ledger)
                {
                    if (string.Equals(entry.Estado, "GENERATED", StringComparison.Ordinal))
                    {
                        generated[entry.Pieza] = entry;
                    }
                }
                if (generated.Count == 0)
                {
                    throw new InvalidOperationException(
                        "el ledger no tiene filas GENERATED: " + LedgerPath + " (correr el generador)");
                }
                var fromLedger = new List<Row>();
                foreach (Row row in rows)
                {
                    if (!string.Equals(row.Estado, "NEW", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    LedgerRow entry;
                    if (!generated.TryGetValue(row.Pieza, out entry))
                    {
                        continue; // BLOCKED_*: no hay PNG ni .meta que escribir
                    }
                    row.Origen = entry.Path;
                    fromLedger.Add(row);
                }
                if (fromLedger.Count != generated.Count)
                {
                    throw new InvalidOperationException(
                        "el ledger declara " + generated.Count + " GENERATED y la sec.9 nombra " + fromLedger.Count +
                        ": las dos tablas no coinciden");
                }
                return fromLedger;
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
                    : "cohorte desconocido o vacio: " + cohort + " (validos: all, s2a, s2b, s2c, new)");
            }
            return selected;
        }

        static bool UsesCohort(string cohort, string pieces, string wanted)
        {
            bool hasPieces = !string.IsNullOrEmpty(pieces) && pieces.Trim().Length > 0;
            return !hasPieces && string.Equals((cohort ?? string.Empty).Trim(), wanted, StringComparison.OrdinalIgnoreCase);
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
            public Vector2 Pivot;
            public float Ppu;
        }

        sealed class LedgerRow
        {
            public string Pieza;
            public string Estado;
            public string Path;
            public string Guid;
            public string Sha;
        }

        static List<LedgerRow> LoadLedger()
        {
            string ledger = System.IO.Path.Combine(ProjectRoot(), LedgerPath);
            if (!File.Exists(ledger))
            {
                return new List<LedgerRow>(); // sin ledger el cohorte `new` aborta al seleccionar
            }
            string[] lines = File.ReadAllLines(ledger, Encoding.UTF8);
            if (lines.Length < 2)
            {
                throw new InvalidDataException(LedgerPath + " esta vacio");
            }
            string[] header = SplitCsv(lines[0]);
            int iPieza = IndexOf(header, "pieza");
            int iEstado = IndexOf(header, "estado");
            int iPath = IndexOf(header, "path");
            int iGuid = IndexOf(header, "guid");
            int iSha = IndexOf(header, "sha256");
            if (iPieza < 0 || iEstado < 0 || iPath < 0 || iGuid < 0 || iSha < 0)
            {
                throw new InvalidDataException("columnas inesperadas en " + LedgerPath + ": " + string.Join(",", header));
            }
            var rows = new List<LedgerRow>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }
                string[] cells = SplitCsv(lines[i]);
                if (cells.Length != header.Length)
                {
                    throw new InvalidDataException("fila " + (i + 1) + " de " + LedgerPath + " con " + cells.Length + " columnas");
                }
                rows.Add(new LedgerRow
                {
                    Pieza = cells[iPieza],
                    Estado = cells[iEstado],
                    Path = cells[iPath],
                    Guid = cells[iGuid],
                    Sha = cells[iSha],
                });
            }
            return rows;
        }

        /// <summary>Paths que el guard v2 tolera sucios: PNG del ledger y su .meta.</summary>
        static HashSet<string> LedgerAllowlist()
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            List<LedgerRow> ledger;
            try
            {
                ledger = LoadLedger();
            }
            catch (Exception)
            {
                return allowed; // sin ledger legible no se tolera nada sucio
            }
            foreach (LedgerRow row in ledger)
            {
                if (!string.Equals(row.Estado, "GENERATED", StringComparison.Ordinal))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(row.Path) || row.Path == "-")
                {
                    continue;
                }
                allowed.Add(row.Path);
                allowed.Add(row.Path + ".meta");
            }
            return allowed;
        }

        static bool OnlyAllowedDirty(string porcelain, HashSet<string> allowed, out string offending)
        {
            offending = null;
            if (allowed.Count == 0)
            {
                offending = "(sin allowlist)";
                return false;
            }
            foreach (string raw in porcelain.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length < 4)
                {
                    continue;
                }
                string path = line.Substring(3).Trim().Trim('"').Replace('\\', '/');
                int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    path = path.Substring(arrow + 4).Trim();
                }
                if (!allowed.Contains(path))
                {
                    offending = path;
                    return false;
                }
            }
            return true;
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
            int iPivot = IndexOf(header, "pivot");
            int iPpu = IndexOf(header, "ppu");
            if (header.Length != 12 || iPieza < 0 || iEstado < 0 || iOrigen < 0 || iModeTarget < 0 ||
                iBorder < 0 || iMaxTex < 0 || iPxActual < 0 || iPivot < 0 || iPpu < 0)
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
                Vector2 pivot;
                float ppu;
                string[] pair = cells[iPivot].Split('/');
                if (pair.Length != 2 ||
                    !float.TryParse(pair[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out pivot.x) ||
                    !float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out pivot.y))
                {
                    throw new InvalidDataException("pivot '" + cells[iPivot] + "' no es x/y en " + cells[iPieza]);
                }
                if (!float.TryParse(cells[iPpu].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out ppu))
                {
                    throw new InvalidDataException("ppu '" + cells[iPpu] + "' no es numerico en " + cells[iPieza]);
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
                    Pivot = pivot,
                    Ppu = ppu,
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
            public string Kind = "replace";
            public int ModeNow;
            public int ModeTarget;
            public int Width;
            public int Height;
            public bool Flip;
            public bool RectResync;
            public bool SheetClear;
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

        static List<PiecePlan> BuildPlan(List<Row> rows, List<LedgerRow> ledger)
        {
            var plans = new List<PiecePlan>();
            var seenAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byGuid = new Dictionary<string, PiecePlan>(StringComparer.Ordinal);
            var ledgerByPiece = new Dictionary<string, LedgerRow>(StringComparer.Ordinal);
            foreach (LedgerRow entry in ledger)
            {
                ledgerByPiece[entry.Pieza] = entry;
            }

            foreach (Row row in rows)
            {
                if (string.Equals(row.Estado, "NEW", StringComparison.Ordinal))
                {
                    LedgerRow entry;
                    if (!ledgerByPiece.TryGetValue(row.Pieza, out entry))
                    {
                        LogError(row.Pieza + ": NEW sin fila GENERATED en " + LedgerPath);
                        continue;
                    }
                    PiecePlan fresh = BuildNewPlan(row, entry);
                    if (fresh != null)
                    {
                        plans.Add(fresh);
                    }
                    continue;
                }
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

                // CTR-2: la pieza ya esta en el modo objetivo (Single) y aun asi el .meta
                // serializa sub-sprites. Ese bloque no dibuja hoy, pero es el que hace que
                // RECT-STALE pueda volver con cualquier reimport a Multiple: se vacia.
                if (plan.ModeNow == plan.ModeTarget && staleRects && plan.ModeTarget == SpriteModeSingle)
                {
                    plan.SheetClear = true;
                    foreach (string detail in staleDetail)
                    {
                        plan.Ops.Add(new PlannedOp
                        {
                            Kind = "sheet-clear",
                            Detail = detail + " -> bloque vaciado (" + SpriteModeMultiple + " -> 0 rects -> " + SpriteModeSingle + ")",
                        });
                    }
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

        /// <summary>
        /// Plan de una pieza NEW: no hay flip que decidir, el .meta lo acuna Unity al importar
        /// y el contrato (textureType, mode, border, maxTex, pivot, ppu, alpha) se escribe de una.
        /// </summary>
        static PiecePlan BuildNewPlan(Row row, LedgerRow entry)
        {
            if (string.IsNullOrEmpty(entry.Path) || entry.Path == "-")
            {
                LogError(row.Pieza + ": la fila del ledger no tiene path");
                return null;
            }
            var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Path);
            if (importer == null || texture == null)
            {
                LogError(row.Pieza + ": " + entry.Path + " no esta importado (correr el generador y reimportar)");
                return null;
            }
            int modeTarget;
            int maxTex;
            Vector4 border;
            if (!int.TryParse(row.ModeTarget, NumberStyles.Integer, CultureInfo.InvariantCulture, out modeTarget) ||
                !int.TryParse(row.MaxTex, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxTex) ||
                !TryParseBorder(row.Border, out border))
            {
                LogError(row.Pieza + ": mode_target/max_tex/border ilegibles en sec.9");
                return null;
            }

            var plan = new PiecePlan
            {
                Row = row,
                Kind = "new",
                AssetPath = entry.Path,
                MetaPath = entry.Path + ".meta",
                Width = texture.width,
                Height = texture.height,
                ModeNow = (int)importer.spriteImportMode,
                ModeTarget = modeTarget,
                Border = border,
                MaxTex = maxTex,
            };
            try
            {
                string meta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), plan.MetaPath), Encoding.UTF8);
                Match guid = Regex.Match(meta, @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                if (guid.Success)
                {
                    plan.Guid = guid.Groups[1].Value;
                }
            }
            catch (Exception error)
            {
                LogWarning(row.Pieza + ": sin .meta todavia (" + error.Message + "): Unity lo va a acunar");
            }

            plan.Ops.Add(new PlannedOp { Kind = "textureType", Detail = importer.textureType + " -> 8 (Sprite)" });
            plan.Ops.Add(new PlannedOp
            {
                Kind = "spriteMode",
                Detail = plan.ModeNow + " -> " + modeTarget + " (Single)",
            });
            plan.Ops.Add(new PlannedOp
            {
                Kind = "spriteBorder",
                Detail = FormatVector(importer.spriteBorder) + " -> " + row.Border,
            });
            plan.Ops.Add(new PlannedOp
            {
                Kind = "maxTextureSize",
                Detail = importer.maxTextureSize + " -> " + maxTex,
            });
            plan.Ops.Add(new PlannedOp
            {
                Kind = "spritePixelsToUnits",
                Detail = importer.spritePixelsPerUnit.ToString(CultureInfo.InvariantCulture) + " -> " +
                         row.Ppu.ToString(CultureInfo.InvariantCulture),
            });
            plan.Ops.Add(new PlannedOp
            {
                Kind = "spritePivot",
                Detail = importer.spritePivot.x.ToString("0.##", CultureInfo.InvariantCulture) + "/" +
                         importer.spritePivot.y.ToString("0.##", CultureInfo.InvariantCulture) + " -> " +
                         row.Pivot.x.ToString("0.##", CultureInfo.InvariantCulture) + "/" +
                         row.Pivot.y.ToString("0.##", CultureInfo.InvariantCulture),
            });
            plan.Ops.Add(new PlannedOp { Kind = "alphaIsTransparency", Detail = importer.alphaIsTransparency + " -> True" });
            return plan;
        }

        static int Rank(string kind)
        {
            switch (kind)
            {
                case "rect-resync": return 0;
                case "sheet-clear": return 0;
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

            // El cohorte `new` no se agrupa: cada SaveAndReimport tiene que completarse para
            // que Unity acuñe el GUID antes del `--freeze-guid` (dentro de StartAssetEditing la
            // importacion queda diferida y el .meta no existe todavia al salir).
            // `sheet-clear` tampoco: su verificacion re-parsea el .meta EN EL ACTO y con la
            // importacion diferida leeria el bloque viejo y daria un RECT-CLEAR-FAILED falso.
            bool batched = toApply.TrueForAll(plan => plan.Kind != "new" && !plan.SheetClear);
            if (batched)
            {
                AssetDatabase.StartAssetEditing();
            }
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
                if (batched)
                {
                    AssetDatabase.StopAssetEditing();
                }
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
            if (plan.Kind == "new")
            {
                return ApplyNewImporter(plan);
            }
            if (plan.SheetClear)
            {
                return ApplySheetClear(plan);
            }
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

        /// <summary>
        /// CTR-2 / S1b: vacia el bloque `spriteSheet.sprites[]` de una pieza que YA esta en Single.
        /// En Single el importer no expone las rects y el data provider 2D solo escribe en Multiple,
        /// asi que el unico camino es el round-trip Multiple -> SetSpriteRects(vacio) -> Single.
        /// La verificacion RE-PARSEA el .meta: si sobrevive una sola entrada, RECT-CLEAR-FAILED
        /// (nunca un log verde) y el apply sale 4. Nunca se edita el .meta a mano.
        /// </summary>
        static bool ApplySheetClear(PiecePlan plan)
        {
            var importer = AssetImporter.GetAtPath(plan.AssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("importer nulo para " + plan.AssetPath);
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.SaveAndReimport(); // sincronico: el data provider necesita el modo ya escrito
            }
            WriteSpriteRects(importer, Array.Empty<SpriteRect>());

            importer.spriteImportMode = SpriteImportMode.Single;
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
            AssetDatabase.ImportAsset(plan.AssetPath, ImportAssetOptions.ForceUpdate);

            int surviving = SheetSpriteCount(plan.MetaPath);
            bool staleLeft = SheetHasStaleRect(plan.MetaPath, plan.Width, plan.Height);
            if (surviving != 0 || staleLeft)
            {
                throw new InvalidOperationException(
                    "RECT-CLEAR-FAILED " + plan.MetaPath + ": el bloque spriteSheet sobrevive con " +
                    (surviving < 0 ? "un .meta ilegible" : surviving + " entrada(s)") +
                    " tras SetSpriteRects(empty)" +
                    (staleLeft ? " y queda un rect distinto del lienzo" : string.Empty) +
                    ". No se edita el .meta a mano: `git checkout -- Assets` y reportar.");
            }

            Log("escrito " + plan.AssetPath + ".meta (" + string.Join(", ", plan.Ops.ConvertAll(op => op.Kind).ToArray()) +
                ") spriteSheet.sprites[] vaciado (" + plan.Row.Pieza + ")");
            return true;
        }

        /// <summary>Entradas serializadas bajo `spriteSheet.sprites[]`; -1 si el .meta no se puede parsear.</summary>
        static int SheetSpriteCount(string metaPath)
        {
            if (string.IsNullOrEmpty(metaPath) || !File.Exists(System.IO.Path.Combine(ProjectRoot(), metaPath)))
            {
                return 0;
            }
            try
            {
                Sheet sheet = ParseSheet(File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), metaPath), Encoding.UTF8));
                return sheet.Sprites.Count;
            }
            catch (Exception error)
            {
                LogWarning(metaPath + ": no se pudo re-parsear el bloque (" + error.Message + ")");
                return -1;
            }
        }

        static bool ApplyNewImporter(PiecePlan plan)
        {
            var importer = AssetImporter.GetAtPath(plan.AssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("importer nulo para " + plan.AssetPath);
            }
            // El rect del sub-sprite tiene que quedar en el lienzo completo ANTES de pasar a
            // Single: el primer import de un PNG lo hace en Multiple (default del proyecto) y
            // recorta el rect al alfa, y el flip a Single NO lo reescribe — el .meta queda con
            // un rect stale que RECT-STALE (auditor) marca por pieza. Resync en Multiple, y
            // recien despues el modo objetivo. En una segunda corrida el modo ya es Single y el
            // rect ya es el lienzo: no hay round-trip y el .meta no cambia (idempotente).
            int canvasWidth;
            int canvasHeight;
            importer.GetSourceTextureWidthAndHeight(out canvasWidth, out canvasHeight);
            bool staleRect = SheetHasStaleRect(plan.MetaPath, canvasWidth, canvasHeight);
            if (staleRect && importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.SaveAndReimport();
            }
            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                ResyncSpriteRects(importer, canvasWidth, canvasHeight);
            }

            // Orden: el modo Single primero; spritePivot y spriteBorder solo se aplican en Single.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = plan.Border;
            importer.spritePivot = plan.Row.Pivot;
            importer.spritePixelsPerUnit = plan.Row.Ppu;
            importer.maxTextureSize = plan.MaxTex;
            importer.alphaIsTransparency = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Log("escrito " + plan.AssetPath + ".meta (" + string.Join(", ", plan.Ops.ConvertAll(op => op.Kind).ToArray()) + ")" +
                (staleRect ? " [rect-resync al lienzo " + canvasWidth + "x" + canvasHeight + "]" : string.Empty));
            return true;
        }

        /// <summary>true si el .meta serializa algun sub-sprite con rect distinto del lienzo.</summary>
        static bool SheetHasStaleRect(string metaPath, int width, int height)
        {
            if (string.IsNullOrEmpty(metaPath) || !File.Exists(System.IO.Path.Combine(ProjectRoot(), metaPath)))
            {
                return false; // .meta todavia no existe: Unity lo acuna al importar, sin rect stale
            }
            try
            {
                Sheet sheet = ParseSheet(File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), metaPath), Encoding.UTF8));
                foreach (SheetSprite sprite in sheet.Sprites)
                {
                    if (sprite.Rect.width > 0f && sprite.Rect.height > 0f &&
                        sprite.Rect != new Rect(0f, 0f, width, height))
                    {
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                LogWarning(metaPath + ": no se pudo leer el rect serializado (" + error.Message + ")");
            }
            return false;
        }

        /// <summary>Lleva todos los rects al lienzo completo usando el data provider 2D (TextureImporter.spritesheet esta retirado en Unity 6).</summary>
        static void ResyncSpriteRects(TextureImporter importer, int width, int height)
        {
            var rects = importer.spriteImportMode == SpriteImportMode.Multiple ? ReadSpriteRects(importer) : null;
            if (rects == null || rects.Length == 0)
            {
                return;
            }
            var target = new Rect(0f, 0f, width, height);
            var changed = false;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].rect != target)
                {
                    rects[i].rect = target;
                    changed = true;
                }
            }
            if (!changed)
            {
                return;
            }
            WriteSpriteRects(importer, rects);
            Log("rect-resync " + importer.assetPath + " -> " + width + "x" + height);
        }

        static SpriteRect[] ReadSpriteRects(TextureImporter importer)
        {
            var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            return dataProvider.GetSpriteRects();
        }

        static void WriteSpriteRects(TextureImporter importer, SpriteRect[] rects)
        {
            var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
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
                if (line.Trim() == "rect:")
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
