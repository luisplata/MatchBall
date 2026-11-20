// ArtFormatConverter: convierte un asset JPG en un PNG real, con GUID nuevo, y remapea
// TODAS las referencias del GUID viejo al nuevo en la misma operacion.
//
// Por que existe: ASC-5 (spec, "Formato PNG") y RT-10/§4 del manifiesto exigen PNG-24 + alfa.
// Pero cambiar la extension cambia el PATH, y un path nuevo es un asset nuevo: GUID nuevo.
// Sin remapeo eso deja cada `m_Sprite` apuntando a un asset que ya no existe. Es una puerta
// de un solo sentido y no se puede hacer "a mano y despues vemos".
//
// Por que NO se reusa `AssetDatabase.MoveAsset` (que conserva el GUID, design D3 / RT-9):
// el pedido de este slice elige el camino explicito y verificable: PNG nuevo + GUID nuevo +
// remapeo contado. Ver F-FMT-1 en el apply-progress (desvio declarado contra ASC-5 literal
// "conservando GUID"). El invariante observable que SI se mantiene es el que importa:
// 0 referencias colgadas, mismas dimensiones, mismos import settings.
//
// Reglas duras:
//   * Lista explicita de assets. Sin globs, sin cohortes, sin wildcards: un `*` se rechaza
//     con exit 2 para que un typo no arrastre assets que no estan en el alcance.
//   * Dry-run por defecto. `apply` es un parametro explicito.
//   * Guard: se niega a aplicar con `git status --porcelain -- Assets` no vacio (mismo guard
//     que ArtMetaMigrator y ArtSyntheticBatch).
//   * NUNCA borra el .jpg ni su .meta. El borrado es un paso separado y posterior, despues de
//     verificar (si el borrado rompe algo, `git checkout -- <path>` lo devuelve).
//   * Idempotente: si el .png ya existe con el GUID determinista, no reescribe bytes ni .meta.
//   * Los paths de System.IO van SIEMPRE calificados (choque con UnityEngine.Object.path).
//
// Uso:
//   Tools > Art Contract > Format Converter > Print Plan — S2c-format (dry-run)
//   Tools > Art Contract > Format Converter > Apply Plan — S2c-format (escribe)
//   DryRun(pieces) / Apply(pieces, allowDirty) / Verify(pieces) desde otro editor script
//   Unity -batchmode -quit -executeMethod DarkVoid.ArtContract.ArtFormatConverter.BatchDryRun
//
// Orden de apply por pieza (una sola pasada, este orden):
//   1. leer bytes JPG -> Texture2D.LoadImage -> EncodeToPNG (mismas dimensiones, sin resize)
//   2. escribir <name>.png
//   3. escribir <name>.png.meta copiando el .meta viejo (maxTex/compresion/PPU/pivot/border)
//      con el GUID nuevo, y reimportar
//   4. importer.spriteImportMode = Single (mode_target de §9) + SaveAndReimport
//   5. remapeo de referencias: fileID viejo + GUID viejo -> fileID nuevo + GUID nuevo
// El paso 5 va ultimo a proposito: el .jpg sigue en disco hasta el paso de borrado, asi que
// no existe ninguna ventana con referencias colgadas.

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

namespace DarkVoid.ArtContract
{
    public static class ArtFormatConverter
    {
        public const string ManifestPath = "Docs/art/manifest-refresh-visual-v1.md";
        public const string ReportDir = "Temp/art-contract/reports";

        /// <summary>Alcance de este slice: 2 JPG del cohorte S2c. Los otros 3 JPG quedan fuera.</summary>
        public const string S2cFormatPieces =
            "Assets/Sprites/BG_V2.jpg,Assets/Sprites/Heroes/conversacion_bg_1.jpg";

        const string MenuRoot = "Tools/Art Contract/Format Converter/";
        const long SingleSpriteFileId = 21300000;
        const long Texture2DFileId = 2800000;
        const int SpriteModeSingle = 1;

        static readonly string[] SourceSuffixes = { ".jpg", ".jpeg" };
        static readonly string[] ConsumerSuffixes = { ".unity", ".prefab", ".asset", ".mat", ".controller" };
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        static readonly Regex GuidLineRegex = new Regex(@"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
        static readonly Regex SpriteTableIdRegex = new Regex(@"^\s+213: (-?\d+)\s*$", RegexOptions.Multiline);

        // ------------------------------------------------------------------ menu

        [MenuItem(MenuRoot + "Print Plan — S2c-format (dry-run)", false, 2200)]
        public static void PrintPlanMenu()
        {
            DryRun(S2cFormatPieces);
        }

        [MenuItem(MenuRoot + "Apply Plan — S2c-format (escribe)", false, 2210)]
        public static void ApplyPlanMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "ArtFormatConverter",
                "Vas a crear el .png de:\n  Assets/Sprites/BG_V2.jpg\n  Assets/Sprites/Heroes/conversacion_bg_1.jpg\n\n" +
                "Con GUID nuevo y remapeando todas sus referencias. El .jpg NO se borra.\n" +
                "Precondicion: arbol limpio en Assets/ y dry-run ya revisado.",
                "Aplicar",
                "Cancelar");
            if (confirmed)
            {
                Apply(S2cFormatPieces, false);
            }
        }

        [MenuItem(MenuRoot + "Verify — S2c-format (solo lectura)", false, 2220)]
        public static void VerifyMenu()
        {
            Verify(S2cFormatPieces);
        }

        // ------------------------------------------------------------- entrypoints

        /// <summary>Imprime el plan y no escribe nada. Exit 0 si pudo planificar.</summary>
        public static int DryRun(string pieces)
        {
            return Run(false, false, pieces);
        }

        /// <summary>Aplica la conversion. allowDirty solo existe para corridas programaticas.</summary>
        public static int Apply(string pieces, bool allowDirty)
        {
            return Run(true, allowDirty, pieces);
        }

        /// <summary>Verificacion de solo lectura: dims, import settings, referencias.</summary>
        public static int Verify(string pieces)
        {
            return RunVerify(pieces);
        }

        public static void BatchDryRun()
        {
            int code = Run(false, false, BatchArg("-artFormatPieces"));
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        public static void BatchApply()
        {
            int code = Run(true, false, BatchArg("-artFormatPieces"));
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        public static void BatchVerify()
        {
            int code = RunVerify(BatchArg("-artFormatPieces"));
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

        public static int Run(bool apply, bool allowDirty, string pieces)
        {
            Log("<b>ArtFormatConverter</b> " + (apply ? "APPLY" : "DRY-RUN") + " · manifiesto " + ManifestPath);

            List<string> sources;
            try
            {
                sources = ParsePieces(pieces);
            }
            catch (Exception error)
            {
                LogError("seleccion invalida: " + error.Message);
                return 2;
            }

            List<Piece> plan;
            try
            {
                plan = BuildPlan(sources);
            }
            catch (Exception error)
            {
                LogError("no se pudo construir el plan: " + error.Message);
                return 2;
            }

            // El guard se evalua DESPUES del plan para poder eximir los paths que este mismo tool
            // escribe (fuente propia + PNG/meta destino + YAML consumidores). Si no, el tool se
            // bloquea a si mismo en la 2da corrida y la idempotencia es decorativa. Cualquier otro
            // cambio bajo Assets/ sigue abortando: el rollback documentado borraria trabajo ajeno.
            HashSet<string> owned = OwnedPaths(plan);
            string porcelain;
            string gitError;
            bool gitOk = TryReadDirtyAssets(out porcelain, out gitError);
            bool dirty = false;
            if (!gitOk)
            {
                LogWarning("no se pudo leer `git status --porcelain -- Assets`: " + gitError +
                           "\n  -> el guard no puede garantizar un rollback limpio" + (apply && !allowDirty ? ": se aborta" : ""));
                if (apply && !allowDirty)
                {
                    return 3;
                }
            }
            else
            {
                var foreign = new List<string>();
                var ownDirty = new List<string>();
                foreach (string path in ParsePorcelainPaths(porcelain))
                {
                    if (owned.Contains(path))
                    {
                        ownDirty.Add(path);
                    }
                    else
                    {
                        foreign.Add(path);
                    }
                }
                dirty = foreign.Count > 0;
                if (dirty)
                {
                    LogWarning("Assets/ tiene cambios sin commitear fuera del alcance de este tool:\n" +
                               Indent(string.Join("\n", foreign.ToArray())));
                    if (apply && !allowDirty)
                    {
                        LogError("apply rechazado por el guard (allowDirty=false). Commitear o stashear primero.");
                        return 3;
                    }
                }
                else
                {
                    Log("guard ok: ningun cambio ajeno en Assets/" +
                        (ownDirty.Count > 0 ? " (" + ownDirty.Count + " path(s) propios de este tool)" : " (`git status` vacio)"));
                }
            }

            PrintPlan(plan, dirty ? porcelain : "limpio");

            if (!apply)
            {
                Log("dry-run: 0 escrituras (ni PNG, ni .meta, ni YAML, ni reporte). Usar Apply para ejecutar.");
                return 0;
            }

            int pending = plan.FindAll(piece => piece.Ops.Count > 0).Count;
            if (pending == 0)
            {
                Log("nada que aplicar: la conversion ya esta hecha y las referencias ya apuntan al GUID nuevo.");
                return 0;
            }

            return Execute(plan);
        }

        // --------------------------------------------------------------- seleccion

        /// <summary>
        /// Lista explicita separada por comas. Sin wildcards: un glob podria arrastrar los otros
        /// 3 JPG del repo, que estan fuera de alcance por pedido (y por el design: S4 va aparte).
        /// </summary>
        static List<string> ParsePieces(string pieces, bool requireSource = true)
        {
            if (string.IsNullOrEmpty(pieces) || pieces.Trim().Length == 0)
            {
                throw new InvalidOperationException("seleccion obligatoria: pasar una lista explicita de assets .jpg/.jpeg");
            }

            var list = new List<string>();
            foreach (string raw in pieces.Split(','))
            {
                string term = raw.Trim();
                if (term.Length == 0)
                {
                    continue;
                }
                if (term.IndexOf('*') >= 0 || term.IndexOf('?') >= 0)
                {
                    throw new InvalidOperationException("`*` y `?` estan prohibidos en este tool (sobre-matchea): " + term);
                }
                string normalized = term.Replace('\\', '/');
                if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("el asset tiene que estar bajo Assets/: " + term);
                }
                string suffix = System.IO.Path.GetExtension(normalized).ToLowerInvariant();
                if (Array.IndexOf(SourceSuffixes, suffix) < 0)
                {
                    throw new InvalidOperationException("solo .jpg/.jpeg son convertibles, no '" + suffix + "': " + term);
                }
                if (requireSource && !File.Exists(System.IO.Path.Combine(ProjectRoot(), normalized)))
                {
                    throw new FileNotFoundException("no existe el asset", normalized);
                }
                if (!list.Contains(normalized))
                {
                    list.Add(normalized);
                }
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException("la lista de assets quedo vacia");
            }
            return list;
        }

        // ------------------------------------------------------------------ plan

        sealed class Reference
        {
            public string File;
            public int Line;
            public long FileId;
            public string Text;
            public string Replacement;
            public bool Backup;
        }

        sealed class Piece
        {
            public string Source;
            public string MetaPath;
            public string Guid;
            public string Target;
            public string TargetMeta;
            public string TargetGuid;
            public string Pieza;
            public string PxEntrega;
            public int SourceWidth;
            public int SourceHeight;
            public List<long> OldIds = new List<long>();
            public List<Reference> Refs = new List<Reference>();
            public Dictionary<string, int> RefsByFile = new Dictionary<string, int>(StringComparer.Ordinal);
            public bool TargetExists;
            public bool TargetGuidOk;
            public List<string> Ops = new List<string>();
            public List<string> Notes = new List<string>();

            public int PrimaryRefs
            {
                get { return Refs.FindAll(reference => !reference.Backup).Count; }
            }

            public int BackupRefs
            {
                get { return Refs.FindAll(reference => reference.Backup).Count; }
            }
        }

        static List<Piece> BuildPlan(List<string> sources)
        {
            Dictionary<string, ManifestRow> manifest = LoadManifest();
            var plan = new List<Piece>();

            foreach (string source in sources)
            {
                var piece = new Piece
                {
                    Source = source,
                    MetaPath = source + ".meta",
                    Target = System.IO.Path.ChangeExtension(source, ".png"),
                    TargetMeta = System.IO.Path.ChangeExtension(source, ".png") + ".meta",
                };

                if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), piece.MetaPath)))
                {
                    throw new FileNotFoundException("falta el .meta del origen", piece.MetaPath);
                }

                string meta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), piece.MetaPath), Encoding.UTF8);
                Match guid = GuidLineRegex.Match(meta);
                if (!guid.Success)
                {
                    throw new InvalidDataException("el .meta no tiene guid: " + piece.MetaPath);
                }
                piece.Guid = guid.Groups[1].Value;

                foreach (Match id in SpriteTableIdRegex.Matches(meta))
                {
                    long value;
                    if (long.TryParse(id.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                        !piece.OldIds.Contains(value))
                    {
                        piece.OldIds.Add(value);
                    }
                }

                piece.TargetGuid = DeterministicGuid(piece.Guid);
                piece.TargetExists = File.Exists(System.IO.Path.Combine(ProjectRoot(), piece.Target));

                int width;
                int height;
                if (!TryReadImageSize(System.IO.Path.Combine(ProjectRoot(), piece.Source), out width, out height))
                {
                    throw new InvalidDataException("no se pudieron leer las dimensiones de " + piece.Source);
                }
                piece.SourceWidth = width;
                piece.SourceHeight = height;

                ManifestRow row;
                if (!manifest.TryGetValue(source, out row))
                {
                    // §9 puede declarar ya el path entregado (.png) aunque el origen sea .jpg
                    manifest.TryGetValue(piece.Target, out row);
                }
                if (row != null)
                {
                    piece.Pieza = row.Pieza;
                    piece.PxEntrega = row.PxEntrega;
                    int px;
                    int py;
                    if (TryParseSize(row.PxEntrega, out px, out py) && (px != width || py != height))
                    {
                        piece.Notes.Add("px_entrega " + row.PxEntrega + " != px del archivo " + width + "x" + height +
                                        ": NO se redimensiona (ASC-4/RT-10 lo resuelve la entrega de arte, no este tool)");
                    }
                }
                else
                {
                    piece.Notes.Add("sin fila en §9 para " + source + ": no se puede validar px_entrega");
                }

                // estado del destino
                if (piece.TargetExists)
                {
                    if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), piece.TargetMeta)))
                    {
                        throw new InvalidDataException("el .png ya existe pero sin .meta: " + piece.TargetMeta +
                                                       " (resolver a mano: no se sobreescribe un asset ajeno)");
                    }
                    string targetMeta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), piece.TargetMeta), Encoding.UTF8);
                    Match targetGuid = GuidLineRegex.Match(targetMeta);
                    piece.TargetGuidOk = targetGuid.Success && targetGuid.Groups[1].Value == piece.TargetGuid;
                    if (!piece.TargetGuidOk)
                    {
                        throw new InvalidDataException("el .png ya existe con OTRO guid (" +
                                                       (targetGuid.Success ? targetGuid.Groups[1].Value : "?") + "): " + piece.Target);
                    }
                    piece.Notes.Add("el .png ya existe con el guid esperado: no se reescriben bytes ni .meta");
                }
                else
                {
                    piece.Ops.Add("png      " + piece.Source + " -> " + piece.Target + "  (" + width + "x" + height +
                                  ", sin resize)");
                    piece.Ops.Add("meta     copia de " + System.IO.Path.GetFileName(piece.MetaPath) + " con guid " +
                                  piece.TargetGuid + "; spriteMode 2 -> 1");
                }

                ScanRefs(piece);

                if (piece.Refs.Count > 0)
                {
                    var perFile = new StringBuilder();
                    foreach (KeyValuePair<string, int> pair in piece.RefsByFile)
                    {
                        if (perFile.Length > 0)
                        {
                            perFile.Append(", ");
                        }
                        perFile.Append(pair.Key + " x" + pair.Value);
                    }
                    piece.Ops.Add("refs     " + piece.Refs.Count + " (" + piece.PrimaryRefs + " primary + " +
                                  piece.BackupRefs + " backup): " + perFile);
                }

                plan.Add(piece);
            }

            // colisiones: dos piezas no pueden apuntar al mismo destino ni compartir guid nuevo
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Piece piece in plan)
            {
                if (!targets.Add(piece.Target))
                {
                    throw new InvalidDataException("dos piezas comparten destino: " + piece.Target);
                }
                if (!guids.Add(piece.TargetGuid))
                {
                    throw new InvalidDataException("dos piezas comparten el guid nuevo: " + piece.TargetGuid);
                }
            }

            return plan;
        }

        /// <summary>
        /// GUID nuevo determinista: mismo origen -> mismo destino en cualquier maquina, asi el
        /// dry-run puede imprimir el GUID exacto que va a quedar escrito y el tool es re-corrible.
        /// </summary>
        static string DeterministicGuid(string oldGuid)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("DarkVoid.ArtContract.ArtFormatConverter:v1:" + oldGuid));
                var builder = new StringBuilder(32);
                for (int i = 0; i < 16; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        static void ScanRefs(Piece piece)
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
                if (text.IndexOf(piece.Guid, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                string projectPath = ToProjectPath(file);
                bool backup = projectPath.IndexOf("/_Recovery/", StringComparison.Ordinal) >= 0;
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var regex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*" + piece.Guid);
                    Match match = regex.Match(lines[i]);
                    if (!match.Success)
                    {
                        continue;
                    }
                    long fileId;
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId))
                    {
                        continue;
                    }
                    long newId = MapFileId(piece, fileId);
                    string replacement = regex.Replace(lines[i], "fileID: " + newId.ToString(CultureInfo.InvariantCulture) +
                                                                  ", guid: " + piece.TargetGuid);
                    piece.Refs.Add(new Reference
                    {
                        File = projectPath,
                        Line = i + 1,
                        FileId = fileId,
                        Text = lines[i].TrimEnd(),
                        Replacement = replacement.TrimEnd(),
                        Backup = backup,
                    });
                    int seen;
                    piece.RefsByFile.TryGetValue(projectPath, out seen);
                    piece.RefsByFile[projectPath] = seen + 1;
                }
            }

            if (piece.Refs.Count > 0)
            {
                piece.Ops.Add("rewrite  " + piece.Refs.Count + " lineas en " + piece.RefsByFile.Count + " archivos");
            }
        }

        /// <summary>
        /// fileID viejo -> fileID nuevo. Los sub-sprites (hash del nombre) colapsan a 21300000;
        /// la Texture2D conserva su 2800000. Cualquier otro id es una referencia que este tool no
        /// entiende: se aborta en vez de adivinar.
        /// </summary>
        static long MapFileId(Piece piece, long fileId)
        {
            if (piece.OldIds.Contains(fileId))
            {
                return SingleSpriteFileId;
            }
            if (fileId == Texture2DFileId)
            {
                return Texture2DFileId;
            }
            throw new InvalidDataException(piece.Source + ": fileID " + fileId +
                                           " no es sub-sprite ni Texture2D (no se adivina el mapeo)");
        }

        // ----------------------------------------------------------------- print

        static void PrintPlan(List<Piece> plan, string gitState)
        {
            string text = BuildPlanText(plan);
            if (text.Length > 0)
            {
                Debug.Log("[ArtFormatConverter]\n" + text.TrimEnd());
            }

            Log("guard Assets/: " + gitState.Replace("\r", string.Empty).Replace("\n", "; "));
            Log(SummaryText(plan));
        }

        /// <summary>Texto del plan, sin efectos. Lo usa la evidencia (execute_code) y el apply-progress.</summary>
        public static string PlanText(string pieces)
        {
            List<Piece> plan = BuildPlan(ParsePieces(pieces));
            return BuildPlanText(plan) + SummaryText(plan);
        }

        static string SummaryText(List<Piece> plan)
        {
            int refs = 0;
            int pending = 0;
            foreach (Piece piece in plan)
            {
                if (piece.Ops.Count > 0)
                {
                    pending++;
                    refs += piece.Refs.Count;
                }
            }

            var text = new StringBuilder();
            text.AppendLine("seleccion: piezas=" + plan.Count + " | convertibles=" + pending + " | refs=" + refs);
            text.AppendLine("piezas seleccionadas: " +
                            string.Join(", ", plan.ConvertAll(piece => piece.Pieza ?? piece.Source).ToArray()));
            foreach (Piece piece in plan)
            {
                text.AppendLine("conteo " + (piece.Pieza ?? piece.Source) + ": refs=" + piece.Refs.Count +
                                " (primary=" + piece.PrimaryRefs + ", backup=" + piece.BackupRefs + ")" +
                                " archivos=" + piece.RefsByFile.Count);
            }
            text.AppendLine("TOTAL " + pending + " piezas con operaciones planificadas | " + refs +
                            " referencias a remapear");
            int conformes = plan.Count - pending;
            if (conformes > 0)
            {
                text.AppendLine(conformes + " piezas ya conformes (sin operaciones, idempotente)");
            }
            return text.ToString().TrimEnd();
        }

        static string BuildPlanText(List<Piece> plan)
        {
            var buffer = new StringBuilder();

            foreach (Piece piece in plan)
            {
                if (piece.Ops.Count == 0)
                {
                    continue;
                }
                buffer.AppendLine("PLAN  " + (string.IsNullOrEmpty(piece.Pieza) ? piece.Source : piece.Pieza));
                buffer.AppendLine("      origen   " + piece.Source + "  (guid " + piece.Guid + ", " +
                                  piece.SourceWidth + "x" + piece.SourceHeight + ")");
                buffer.AppendLine("      destino  " + piece.Target + "  (guid " + piece.TargetGuid + ")");
                foreach (string op in piece.Ops)
                {
                    buffer.AppendLine("      op       " + op);
                }
                foreach (Reference reference in piece.Refs)
                {
                    buffer.AppendLine("      linea    " + reference.File + ":" + reference.Line +
                                      (reference.Backup ? "  [backup _Recovery]" : string.Empty));
                    buffer.AppendLine("                 -  " + reference.Text);
                    buffer.AppendLine("                 +  " + reference.Replacement);
                }
                foreach (string note in piece.Notes)
                {
                    buffer.AppendLine("      nota     " + note);
                }
                buffer.AppendLine();
            }

            return buffer.ToString();
        }

        // --------------------------------------------------------------- execute

        static int Execute(List<Piece> plan)
        {
            var toApply = plan.FindAll(piece => piece.Ops.Count > 0);
            int pngWritten = 0;
            int metaWritten = 0;
            int filesWritten = 0;
            int refsRemapped = 0;
            var failures = new List<string>();

            Log("apply: " + toApply.Count + " piezas. Escenas y prefabs abiertos se van a recargar; conviene cerrarlos.");

            foreach (Piece piece in toApply)
            {
                try
                {
                    if (!piece.TargetExists)
                    {
                        WritePng(piece);
                        pngWritten++;
                        WriteMeta(piece);
                        metaWritten++;
                        AssetDatabase.ImportAsset(piece.Target, ImportAssetOptions.ForceUpdate);
                        ForceSingle(piece);
                    }

                    int touched;
                    int remapped;
                    RemapRefs(piece, out touched, out remapped);
                    filesWritten += touched;
                    refsRemapped += remapped;
                }
                catch (Exception error)
                {
                    failures.Add((piece.Pieza ?? piece.Source) + ": " + error.Message);
                    LogError((piece.Pieza ?? piece.Source) + ": " + error);
                }
            }

            AssetDatabase.Refresh();

            foreach (Piece piece in toApply)
            {
                if (CountRefs(piece.Guid) > 0)
                {
                    failures.Add((piece.Pieza ?? piece.Source) + ": quedan referencias al GUID viejo " + piece.Guid);
                }
            }

            string report = WriteReport(toApply, pngWritten, metaWritten, filesWritten, refsRemapped, failures);
            Log("apply: resumen en " + report);
            Log("apply: " + pngWritten + " PNG escritos, " + metaWritten + " .meta creados, " + filesWritten +
                " archivos YAML reescritos, " + refsRemapped + " referencias remapeadas");
            Log("apply: el .jpg y su .meta NO se borraron (paso separado, despues de verificar).");

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    LogError(failure);
                }
                LogError("apply con " + failures.Count + " fallas: el contrato NO se cumple");
                return 4;
            }

            Log("apply ok: correr Verify y tools/art/audit_art_manifest.py antes de borrar los .jpg");
            return 0;
        }

        /// <summary>
        /// Decodifica el JPG y lo re-encoda como PNG RGBA8, al MISMO px. Sin resize.
        /// OJO: un JPG decodifica a RGB24 y EncodeToPNG escribiria colorType 2 (sin alfa).
        /// ASC-5/RT-10 piden PNG con canal alfa, asi que se pasa por un RGBA32 explicito
        /// (alfa 255): los bytes de color no cambian, el header si.
        /// </summary>
        static void WritePng(Piece piece)
        {
            string sourceAbsolute = System.IO.Path.Combine(ProjectRoot(), piece.Source);
            string targetAbsolute = System.IO.Path.Combine(ProjectRoot(), piece.Target);

            byte[] source = File.ReadAllBytes(sourceAbsolute);
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D rgba = null;
            try
            {
                if (!decoded.LoadImage(source, false))
                {
                    throw new InvalidDataException("Texture2D.LoadImage fallo para " + piece.Source);
                }
                if (decoded.width != piece.SourceWidth || decoded.height != piece.SourceHeight)
                {
                    throw new InvalidDataException("el decodificado mide " + decoded.width + "x" + decoded.height +
                                                   " y el archivo declara " + piece.SourceWidth + "x" + piece.SourceHeight);
                }

                rgba = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
                rgba.SetPixels32(decoded.GetPixels32());
                rgba.Apply(false, false);

                byte[] png = rgba.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    throw new InvalidDataException("EncodeToPNG devolvio vacio para " + piece.Source);
                }

                string directory = System.IO.Path.GetDirectoryName(targetAbsolute);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllBytes(targetAbsolute, png);
                piece.TargetExists = true;
                Log("escrito " + piece.Target + " (" + png.Length + " B, " + rgba.width + "x" + rgba.height + ", RGBA32)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (rgba != null)
                {
                    UnityEngine.Object.DestroyImmediate(rgba);
                }
            }
        }

        /// <summary>
        /// Copia el .meta del origen tal cual y le cambia solo el guid. maxTextureSize,
        /// compresion, PPU, pivot, spriteBorder y platformSettings viajan con el texto, sin
        /// re-tipear numeros (un typo aca cambia el tamano en juego sin que nadie lo vea).
        /// </summary>
        static void WriteMeta(Piece piece)
        {
            string sourceMeta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), piece.MetaPath), Encoding.UTF8);
            Match guid = GuidLineRegex.Match(sourceMeta);
            if (!guid.Success)
            {
                throw new InvalidDataException("el .meta de origen no tiene guid: " + piece.MetaPath);
            }

            string updated = GuidLineRegex.Replace(sourceMeta,
                "guid: " + piece.TargetGuid, 1);
            if (updated.IndexOf(piece.Guid, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException("quedo el guid viejo en el .meta nuevo: " + piece.TargetMeta);
            }

            File.WriteAllText(System.IO.Path.Combine(ProjectRoot(), piece.TargetMeta), updated, Utf8NoBom);
            Log("escrito " + piece.TargetMeta + " (guid " + piece.TargetGuid + ", settings copiados del origen)");
        }

        /// <summary>mode_target de §9: Single. La API purga el spriteSheet (design D8).</summary>
        static void ForceSingle(Piece piece)
        {
            var importer = AssetImporter.GetAtPath(piece.Target) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("importer nulo para " + piece.Target);
            }
            if ((int)importer.spriteImportMode != SpriteModeSingle)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Log("reimportado " + piece.Target + " como Single (mode_target de §9)");
            }
            else
            {
                AssetDatabase.ImportAsset(piece.Target, ImportAssetOptions.ForceUpdate);
            }
        }

        static void RemapRefs(Piece piece, out int filesWritten, out int refsRemapped)
        {
            filesWritten = 0;
            refsRemapped = 0;
            if (piece.Refs.Count == 0)
            {
                return;
            }

            var byFile = new Dictionary<string, List<Reference>>(StringComparer.Ordinal);
            foreach (Reference reference in piece.Refs)
            {
                List<Reference> list;
                if (!byFile.TryGetValue(reference.File, out list))
                {
                    list = new List<Reference>();
                    byFile[reference.File] = list;
                }
                list.Add(reference);
            }

            var regex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*" + piece.Guid);
            foreach (KeyValuePair<string, List<Reference>> pair in byFile)
            {
                string absolute = System.IO.Path.Combine(ProjectRoot(), pair.Key);
                string original = File.ReadAllText(absolute, Encoding.UTF8);
                int remapped = 0;
                MatchEvaluator evaluator = match =>
                {
                    long fileId;
                    if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId))
                    {
                        return match.Value;
                    }
                    long newId = MapFileId(piece, fileId);
                    remapped++;
                    return "fileID: " + newId.ToString(CultureInfo.InvariantCulture) + ", guid: " + piece.TargetGuid;
                };

                string updated = regex.Replace(original, evaluator);
                if (string.Equals(updated, original, StringComparison.Ordinal))
                {
                    continue;
                }
                File.WriteAllText(absolute, updated, Utf8NoBom);
                AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
                filesWritten++;
                refsRemapped += remapped;
                Log("remapeado " + pair.Key + " (" + remapped + " linea(s), " + (piece.Pieza ?? piece.Source) + ")");
            }
        }

        // ---------------------------------------------------------------- verify

        static int RunVerify(string pieces)
        {
            List<string> sources;
            try
            {
                // el .jpg puede estar ya borrado (paso 5): en ese caso se verifica el estado entregado
                sources = ParsePieces(pieces, false);
            }
            catch (Exception error)
            {
                LogError("seleccion invalida: " + error.Message);
                return 2;
            }

            int failures = 0;
            foreach (string source in sources)
            {
                bool sourceAvailable = File.Exists(System.IO.Path.Combine(ProjectRoot(), source)) &&
                                       File.Exists(System.IO.Path.Combine(ProjectRoot(), source + ".meta"));
                failures += sourceAvailable ? VerifyPiece(source) : VerifyDelivered(source);
            }

            Log("verify: " + sources.Count + " piezas | fallas=" + failures);
            if (failures > 0)
            {
                LogError("verify FAIL: " + failures + " chequeos no pasan");
                return 5;
            }
            Log("verify PASS: dims + import settings + referencias");
            return 0;
        }

        /// <summary>
        /// Verificacion cuando el .jpg y su .meta ya se borraron (paso 5). Ya no hay baseline de
        /// import settings: se compara el .png contra §9 (mode_target, max_tex, pivot, ppu, border)
        /// y las dimensiones contra `px_entrega`. Es el chequeo que puede correr el verify.
        /// </summary>
        static int VerifyDelivered(string source)
        {
            string target = System.IO.Path.ChangeExtension(source, ".png");
            string targetMeta = target + ".meta";
            var lines = new StringBuilder();
            var problems = new List<string>();
            lines.AppendLine("VERIFY(delivered) " + target);

            if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), target)) ||
                !File.Exists(System.IO.Path.Combine(ProjectRoot(), targetMeta)))
            {
                LogError(lines.ToString() + "  RESULT   FAIL (falta el .png o su .meta)");
                return 1;
            }

            string meta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), targetMeta), Encoding.UTF8);
            Match guid = GuidLineRegex.Match(meta);
            string newGuid = guid.Success ? guid.Groups[1].Value : null;
            if (string.IsNullOrEmpty(newGuid))
            {
                problems.Add("el .meta no tiene guid");
            }

            int width;
            int height;
            int bitDepth = 0;
            int colorType = 0;
            bool headerOk = false;
            try
            {
                byte[] bytes = File.ReadAllBytes(System.IO.Path.Combine(ProjectRoot(), target));
                headerOk = TryReadPngHeader(bytes, out width, out height, out bitDepth, out colorType);
            }
            catch (Exception error)
            {
                LogWarning("no se pudo leer " + target + ": " + error.Message);
                width = height = 0;
            }
            bool rgba8 = headerOk && bitDepth == 8 && colorType == 6;
            if (!rgba8)
            {
                problems.Add("el PNG no es RGBA8 (bitDepth " + bitDepth + ", colorType " + colorType + ")");
            }

            ManifestRow row;
            var manifest = LoadManifest();
            if (!manifest.TryGetValue(source, out row))
            {
                manifest.TryGetValue(target, out row);
            }
            if (row != null)
            {
                int px;
                int py;
                if (TryParseSize(row.PxEntrega, out px, out py) && (px != width || py != height))
                {
                    problems.Add("dims " + width + "x" + height + " != px_entrega " + row.PxEntrega);
                }
                var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                string modeNow = importer == null
                    ? ReadMetaValue(meta, "spriteMode")
                    : ((int)importer.spriteImportMode).ToString(CultureInfo.InvariantCulture);
                var pairs = new List<string[]>
                {
                    new[] { "spriteMode", row.ModeTarget, modeNow },
                    new[] { "maxTextureSize", row.MaxTex, ReadMetaValue(meta, "maxTextureSize") },
                    new[] { "spritePivot", row.Pivot, ReadMetaValue(meta, "spritePivot") },
                    new[] { "spritePixelsToUnits", row.Ppu, ReadMetaValue(meta, "spritePixelsToUnits") },
                    new[] { "spriteBorder", row.Border, ReadMetaValue(meta, "spriteBorder") },
                };
                foreach (string[] pair in pairs)
                {
                    bool ok = NormalizeValue(pair[1]) == NormalizeValue(pair[2]) ||
                              NormalizeValue(pair[1]).Replace("/0.", "/0,") == NormalizeValue(pair[2]);
                    if (pair[0] == "spriteBorder" || pair[0] == "spritePivot")
                    {
                        ok = BorderLike(pair[1]) == BorderLike(pair[2]);
                    }
                    if (!ok)
                    {
                        problems.Add(pair[0] + " §9 " + pair[1] + " != meta " + pair[2]);
                    }
                    lines.AppendLine("  " + pair[0].PadRight(20) + " §9 " + (pair[1] ?? "?").PadRight(10) +
                                      " meta " + (pair[2] ?? "?") + (ok ? "  ok" : "  FAIL"));
                }
            }
            else
            {
                lines.AppendLine("  nota     sin fila en §9: solo se verifica el header");
            }

            var byFile = new Dictionary<string, int>(StringComparer.Ordinal);
            int refs = CountRefsByFile(newGuid, byFile);
            var perFile = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in byFile)
            {
                if (perFile.Length > 0)
                {
                    perFile.Append(", ");
                }
                perFile.Append(pair.Key + " x" + pair.Value);
            }
            lines.AppendLine("  dims     png " + width + "x" + height + " | ihdr bitDepth " + bitDepth + " colorType " +
                             colorType + " | guid " + newGuid);
            lines.AppendLine("  refs     guid nuevo " + refs + (perFile.Length > 0 ? " (" + perFile + ")" : string.Empty));
            lines.AppendLine("  origen   " + source + " ya no existe (paso 5 hecho)");
            if (refs == 0)
            {
                problems.Add("ninguna referencia al guid " + newGuid + ": el asset quedo huerfano");
            }

            if (problems.Count > 0)
            {
                lines.AppendLine("  RESULT   FAIL");
                foreach (string problem in problems)
                {
                    lines.AppendLine("    - " + problem);
                }
                LogError(lines.ToString().TrimEnd());
            }
            else
            {
                lines.AppendLine("  RESULT   PASS");
                Log(lines.ToString().TrimEnd());
            }
            return problems.Count;
        }

        /// <summary>Normaliza `0/0/0/0` y `{x: 0, y: 0, z: 0, w: 0}` al mismo texto comparable.</summary>
        static string BorderLike(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            MatchCollection numbers = Regex.Matches(value, @"-?\d+(\.\d+)?");
            var parts = new List<string>();
            foreach (Match number in numbers)
            {
                parts.Add(double.Parse(number.Value, CultureInfo.InvariantCulture)
                    .ToString("0.####", CultureInfo.InvariantCulture));
            }
            return string.Join("/", parts.ToArray());
        }

        static int VerifyPiece(string source)
        {
            string metaPath = source + ".meta";
            string target = System.IO.Path.ChangeExtension(source, ".png");
            string targetMeta = target + ".meta";

            if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), metaPath)))
            {
                LogError(source + ": falta el .meta de origen");
                return 1;
            }
            string sourceMeta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), metaPath), Encoding.UTF8);
            Match guidMatch = GuidLineRegex.Match(sourceMeta);
            string oldGuid = guidMatch.Success ? guidMatch.Groups[1].Value : null;

            var problems = new List<string>();
            var lines = new StringBuilder();
            lines.AppendLine("VERIFY " + source);

            if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), target)))
            {
                problems.Add("falta " + target);
                LogError(lines.ToString() + "  - " + problems[0]);
                return 1;
            }

            if (!File.Exists(System.IO.Path.Combine(ProjectRoot(), targetMeta)))
            {
                problems.Add("falta " + targetMeta);
                LogError(lines.ToString() + "  - " + problems[0]);
                return 1;
            }
            string newMeta = File.ReadAllText(System.IO.Path.Combine(ProjectRoot(), targetMeta), Encoding.UTF8);
            Match newGuidMatch = GuidLineRegex.Match(newMeta);
            string newGuid = newGuidMatch.Success ? newGuidMatch.Groups[1].Value : null;
            if (string.IsNullOrEmpty(newGuid))
            {
                problems.Add(targetMeta + " sin guid");
            }
            if (newGuid == oldGuid)
            {
                problems.Add("el guid nuevo es igual al viejo (" + newGuid + "): no hubo asset nuevo");
            }

            // (1) dimensiones: .jpg vs .png, leidas de los bytes (SOF / IHDR)
            int sx;
            int sy;
            int tx;
            int ty;
            bool hasSource = TryReadImageSize(System.IO.Path.Combine(ProjectRoot(), source), out sx, out sy);
            bool hasTarget = TryReadImageSize(System.IO.Path.Combine(ProjectRoot(), target), out tx, out ty);
            if (!hasSource || !hasTarget)
            {
                problems.Add("no se pudieron leer dimensiones (origen=" + hasSource + ", destino=" + hasTarget + ")");
                sx = sy = tx = ty = 0;
            }
            bool dimsOk = hasSource && hasTarget && sx == tx && sy == ty;
            if (!dimsOk)
            {
                problems.Add("dimensiones " + tx + "x" + ty + " != " + sx + "x" + sy);
            }

            // (2) formato del PNG: bitDepth / colorType del IHDR
            int headerWidth;
            int headerHeight;
            int bitDepth = 0;
            int colorType = 0;
            bool headerOk = false;
            try
            {
                headerOk = TryReadPngHeader(File.ReadAllBytes(System.IO.Path.Combine(ProjectRoot(), target)),
                    out headerWidth, out headerHeight, out bitDepth, out colorType);
            }
            catch (Exception error)
            {
                LogWarning("no se pudo leer el IHDR de " + target + ": " + error.Message);
            }
            bool rgba8 = headerOk && bitDepth == 8 && colorType == 6;

            // (3) import settings: importer vivo vs .meta viejo (texto)
            string[] keys = { "maxTextureSize", "spritePixelsToUnits", "spritePivot", "spriteMode", "spriteBorder" };
            var importer = AssetImporter.GetAtPath(target) as TextureImporter;
            if (importer == null)
            {
                problems.Add("importer nulo para " + target);
            }
            var settings = new StringBuilder();
            foreach (string key in keys)
            {
                string before = ReadMetaValue(sourceMeta, key);
                string after;
                if (key == "spriteMode")
                {
                    after = ((int)importer.spriteImportMode).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    after = ReadMetaValue(newMeta, key);
                }
                string verdict;
                if (key == "spriteMode")
                {
                    verdict = after == SpriteModeSingle.ToString(CultureInfo.InvariantCulture) ? "ok" : "FAIL";
                }
                else
                {
                    verdict = NormalizeValue(before) == NormalizeValue(after) ? "ok" : "FAIL";
                }
                if (verdict == "FAIL")
                {
                    problems.Add(key + " " + before + " -> " + after);
                }
                settings.AppendLine("  " + key.PadRight(20) + " " + (before ?? "?").PadRight(22) + " -> " +
                                    (after ?? "?").PadRight(12) + " " + verdict);
            }

            // (4) referencias
            int oldRefs = CountRefs(oldGuid);
            var newRefs = new Dictionary<string, int>(StringComparer.Ordinal);
            int newCount = CountRefsByFile(newGuid, newRefs);
            var perFile = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in newRefs)
            {
                if (perFile.Length > 0)
                {
                    perFile.Append(", ");
                }
                perFile.Append(pair.Key + " x" + pair.Value);
            }

            lines.AppendLine("  dims     jpg " + sx + "x" + sy + " -> png " + tx + "x" + ty + "  " + (dimsOk ? "ok" : "FAIL"));
            lines.AppendLine("  ihdr     bitDepth " + bitDepth + " colorType " + colorType +
                             (rgba8 ? "  ok (RGBA8)" : "  FAIL (se esperaba 8/6)"));
            lines.Append(settings.ToString());
            lines.AppendLine("  refs     guid viejo " + oldRefs + " (esperado 0) | guid nuevo " + newCount +
                             (perFile.Length > 0 ? " (" + perFile + ")" : string.Empty));
            lines.AppendLine("  jpg      " + (File.Exists(System.IO.Path.Combine(ProjectRoot(), source)) ? "sigue en disco (paso 5 pendiente)" : "ya borrado"));

            if (oldRefs != 0)
            {
                problems.Add("quedan " + oldRefs + " referencias al guid viejo");
            }
            if (!rgba8)
            {
                problems.Add("el PNG no es RGBA8");
            }

            if (problems.Count > 0)
            {
                lines.AppendLine("  RESULT   FAIL");
                foreach (string problem in problems)
                {
                    lines.AppendLine("    - " + problem);
                }
                LogError(lines.ToString().TrimEnd());
            }
            else
            {
                lines.AppendLine("  RESULT   PASS");
                Log(lines.ToString().TrimEnd());
            }
            return problems.Count;
        }

        static string ReadMetaValue(string meta, string key)
        {
            Match match = Regex.Match(meta, @"^  " + Regex.Escape(key) + @": (.+)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        static string NormalizeValue(string value)
        {
            return value == null ? string.Empty : value.Replace(" ", string.Empty);
        }

        // ----------------------------------------------------------------- refs

        static int CountRefs(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return 0;
            }
            var regex = new Regex(@"fileID:\s*-?\d+,\s*guid:\s*" + guid);
            int count = 0;
            foreach (string file in ConsumerFiles())
            {
                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (Exception)
                {
                    continue;
                }
                if (text.IndexOf(guid, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                count += regex.Matches(text).Count;
            }
            return count;
        }

        static int CountRefsByFile(string guid, IDictionary<string, int> byFile)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return 0;
            }
            var regex = new Regex(@"fileID:\s*(-?\d+),\s*guid:\s*" + guid);
            int count = 0;
            foreach (string file in ConsumerFiles())
            {
                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (Exception)
                {
                    continue;
                }
                if (text.IndexOf(guid, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                int hits = regex.Matches(text).Count;
                if (hits == 0)
                {
                    continue;
                }
                count += hits;
                byFile[ToProjectPath(file)] = hits;
            }
            return count;
        }

        // -------------------------------------------------------------- manifiesto

        sealed class ManifestRow
        {
            public string Pieza;
            public string Origen;
            public string PxEntrega;
            public string ModeTarget;
            public string MaxTex;
            public string Pivot;
            public string Ppu;
            public string Border;
        }

        /// <summary>Lee el bloque ```csv de §9 (fuente unica de verdad) indexado por `origen`.</summary>
        static Dictionary<string, ManifestRow> LoadManifest()
        {
            var index = new Dictionary<string, ManifestRow>(StringComparer.OrdinalIgnoreCase);
            string manifest = System.IO.Path.Combine(ProjectRoot(), ManifestPath);
            if (!File.Exists(manifest))
            {
                LogWarning("no hay manifiesto en " + ManifestPath + ": se planifica sin px_entrega ni mode_target");
                return index;
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
                LogWarning("el manifiesto no tiene bloque ```csv");
                return index;
            }

            string[] header = null;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    break;
                }
                if (line.Length == 0)
                {
                    continue;
                }
                string[] cells = SplitCsv(line);
                if (header == null)
                {
                    header = cells;
                    continue;
                }
                if (cells.Length != header.Length)
                {
                    continue;
                }
                int iOrigen = IndexOf(header, "origen");
                int iPieza = IndexOf(header, "pieza");
                int iPx = IndexOf(header, "px_entrega");
                int iMode = IndexOf(header, "mode_target");
                int iMaxTex = IndexOf(header, "max_tex");
                int iPivot = IndexOf(header, "pivot");
                int iPpu = IndexOf(header, "ppu");
                int iBorder = IndexOf(header, "border");
                if (iOrigen < 0 || iPieza < 0 || iPx < 0 || iMode < 0)
                {
                    break;
                }
                string origen = cells[iOrigen];
                if (string.IsNullOrEmpty(origen) || origen == "-")
                {
                    continue;
                }
                index[origen.Replace('\\', '/')] = new ManifestRow
                {
                    Pieza = cells[iPieza],
                    Origen = origen,
                    PxEntrega = cells[iPx],
                    ModeTarget = cells[iMode],
                    MaxTex = iMaxTex >= 0 ? cells[iMaxTex] : null,
                    Pivot = iPivot >= 0 ? cells[iPivot] : null,
                    Ppu = iPpu >= 0 ? cells[iPpu] : null,
                    Border = iBorder >= 0 ? cells[iBorder] : null,
                };
            }
            return index;
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

        static bool TryParseSize(string value, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(value) || value == "TBD" || value == "-")
            {
                return false;
            }
            Match match = Regex.Match(value, @"^(\d+)x(\d+)$");
            if (!match.Success)
            {
                return false;
            }
            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
                   int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
        }

        // ------------------------------------------------------------- image size

        /// <summary>Dimensiones reales del archivo: IHDR para PNG, marcador SOF para JPEG.</summary>
        static bool TryReadImageSize(string absolutePath, out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(absolutePath))
            {
                return false;
            }
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(absolutePath);
            }
            catch (Exception)
            {
                return false;
            }

            int pngWidth;
            int pngHeight;
            int bitDepth;
            int colorType;
            if (TryReadPngHeader(bytes, out pngWidth, out pngHeight, out bitDepth, out colorType))
            {
                width = pngWidth;
                height = pngHeight;
                return true;
            }

            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            {
                return false;
            }
            int at = 2;
            while (at + 9 < bytes.Length)
            {
                if (bytes[at] != 0xFF)
                {
                    at++;
                    continue;
                }
                int marker = bytes[at + 1];
                if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    at += 2;
                    continue;
                }
                int length = (bytes[at + 2] << 8) | bytes[at + 3];
                bool sof = marker == 0xC0 || marker == 0xC1 || marker == 0xC2 || marker == 0xC3 ||
                           marker == 0xC5 || marker == 0xC6 || marker == 0xC7 ||
                           marker == 0xC9 || marker == 0xCA || marker == 0xCB ||
                           marker == 0xCD || marker == 0xCE || marker == 0xCF;
                if (sof)
                {
                    height = (bytes[at + 5] << 8) | bytes[at + 6];
                    width = (bytes[at + 7] << 8) | bytes[at + 8];
                    return width > 0 && height > 0;
                }
                at += 2 + length;
            }
            return false;
        }

        static bool TryReadPngHeader(string absolutePath, out int width, out int height, out int bitDepth, out int colorType)
        {
            width = height = bitDepth = colorType = 0;
            if (!File.Exists(absolutePath))
            {
                return false;
            }
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(absolutePath);
            }
            catch (Exception)
            {
                return false;
            }
            return TryReadPngHeader(bytes, out width, out height, out bitDepth, out colorType);
        }

        static bool TryReadPngHeader(byte[] bytes, out int width, out int height, out int bitDepth, out int colorType)
        {
            width = height = bitDepth = colorType = 0;
            if (bytes.Length < 33)
            {
                return false;
            }
            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            {
                return false;
            }
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            bitDepth = bytes[24];
            colorType = bytes[25];
            return width > 0 && height > 0;
        }

        // ------------------------------------------------------------------ guard

        /// <summary>
        /// Paths que este tool posee: su propio fuente (sino el guard lo bloquea en la 1ra corrida,
        /// misma friccion que F-PR10-4 del migrador), los PNG/meta destino y los YAML consumidores.
        /// </summary>
        static HashSet<string> OwnedPaths(List<Piece> plan)
        {
            var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            owned.Add("Assets/Editor/ArtContract/ArtFormatConverter.cs");
            owned.Add("Assets/Editor/ArtContract/ArtFormatConverter.cs.meta");
            foreach (Piece piece in plan)
            {
                owned.Add(piece.Target);
                owned.Add(piece.TargetMeta);
                foreach (Reference reference in piece.Refs)
                {
                    owned.Add(reference.File);
                }

                // consumidores del GUID nuevo: son los mismos archivos ya reescritos por este tool,
                // asi que siguen siendo suyos en una 2da corrida (idempotencia sin romper el guard).
                var current = new Dictionary<string, int>(StringComparer.Ordinal);
                CountRefsByFile(piece.TargetGuid, current);
                foreach (string file in current.Keys)
                {
                    owned.Add(file);
                }
            }
            return owned;
        }

        static IEnumerable<string> ParsePorcelainPaths(string porcelain)
        {
            if (string.IsNullOrEmpty(porcelain))
            {
                yield break;
            }
            foreach (string raw in porcelain.Replace("\r", string.Empty).Split('\n'))
            {
                if (raw.Trim().Length < 4)
                {
                    continue;
                }
                string path = raw.Substring(3).Trim();
                int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    path = path.Substring(arrow + 4);
                }
                path = path.Trim('"').Replace('\\', '/');
                yield return path;
            }
        }

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
                    if (process.ExitCode < 0)
                    {
                        error = "git status salio con " + process.ExitCode + ": " + stderr.Trim();
                        return false;
                    }
                    // TrimEnd, NO Trim: `Trim()` se come el espacio inicial de la 1ra linea del
                    // porcelain (` M path` -> `M path`) y el parseo de despues corta un char del
                    // path (F-S2a-3, reaparecio aca: `ssets/Scenes/Login.unity`).
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

        // ---------------------------------------------------------------- report

        static string WriteReport(List<Piece> applied, int pngWritten, int metaWritten, int filesWritten,
            int refsRemapped, List<string> failures)
        {
            string directory = System.IO.Path.Combine(ProjectRoot(), ReportDir);
            Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "art-format-converter-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".md");

            var text = new StringBuilder();
            text.AppendLine("# ArtFormatConverter — " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("| Campo | Valor |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Manifiesto | `" + ManifestPath + "` |");
            text.AppendLine("| Piezas | " + applied.Count + " |");
            text.AppendLine("| PNG escritos | " + pngWritten + " |");
            text.AppendLine("| .meta creados | " + metaWritten + " |");
            text.AppendLine("| Archivos YAML reescritos | " + filesWritten + " |");
            text.AppendLine("| Referencias remapeadas | " + refsRemapped + " |");
            text.AppendLine("| .jpg borrados | 0 (paso separado) |");
            text.AppendLine("| Fallas | " + failures.Count + " |");
            text.AppendLine();
            text.AppendLine("## Piezas");
            foreach (Piece piece in applied)
            {
                text.AppendLine();
                text.AppendLine("### " + (piece.Pieza ?? piece.Source));
                text.AppendLine("- origen `" + piece.Source + "` (guid `" + piece.Guid + "`, " + piece.SourceWidth + "x" +
                                piece.SourceHeight + ")");
                text.AppendLine("- destino `" + piece.Target + "` (guid `" + piece.TargetGuid + "`)");
                foreach (string op in piece.Ops)
                {
                    text.AppendLine("- `" + op + "`");
                }
                foreach (Reference reference in piece.Refs)
                {
                    text.AppendLine("- " + reference.File + ":" + reference.Line +
                                    (reference.Backup ? " (backup)" : string.Empty));
                    text.AppendLine("  - `" + reference.Text + "`");
                    text.AppendLine("  - `" + reference.Replacement + "`");
                }
                foreach (string note in piece.Notes)
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

        // ------------------------------------------------------------------ util

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
                yield return file;
            }
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

        // --------------------------------------------------------------- logging

        static void Log(string message)
        {
            Debug.Log("[ArtFormatConverter] " + message);
        }

        static void LogWarning(string message)
        {
            Debug.LogWarning("[ArtFormatConverter] " + message);
        }

        static void LogError(string message)
        {
            Debug.LogError("[ArtFormatConverter] " + message);
        }
    }
}
#endif
