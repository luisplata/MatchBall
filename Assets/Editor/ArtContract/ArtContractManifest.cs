// ArtContractManifest: parser del bloque ```csv cercado de la sec.9 del manifiesto v2.
//
// Por que existe: la sec.9 ("tabla de verdad") es la unica fuente del contrato. La prosa de
// §3/§4 es para personas y no se parsea. tools/art/audit_art_manifest.py ya la lee para las
// reglas de archivo (bytes, .meta, YAML); esta clase la lee para las reglas que SOLO Unity
// puede ver: que borde declara la pieza que un Image dibuja en Sliced/Tiled (ASC-3).
//
// Reglas duras de este archivo:
//   * Solo lectura. Nunca escribe el manifiesto ni ningun asset.
//   * Un unico bloque ```csv: si hay 0 o 2, el contrato deja de ser verificable y se lanza
//     ArtContractManifestException. Mismo criterio que el auditor Python (ToolError).
//   * No hay validacion semantica de la tabla aca: el auditor Python ya falla por filas,
//     duplicados, regex de nombres, etc. Aca solo se parsea y se indexa por path de origen.
//
// Uso:
//   ArtContractManifest manifest = ArtContractManifest.Load();
//   ArtContractRow row = manifest.FindByOrigen("Assets/Sprites/Button_UI.png");

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DarkVoid.ArtContract
{
    /// <summary>Una fila de la sec.9, con las 12 columnas del encabezado.</summary>
    public sealed class ArtContractRow
    {
        public string Pieza;
        public string Estado;
        public string Origen;
        public string PxActual;
        public string PxEntrega;
        public string ModeTarget;
        public string ModeActual;
        public string Border;
        public string MaxTex;
        public string Pivot;
        public string Ppu;
        public string Address;

        /// <summary>REPLACE pisa bytes de un archivo que ya existe; NEW nace con GUID nuevo.</summary>
        public bool EsReplace
        {
            get { return string.Equals(Estado, "REPLACE", StringComparison.Ordinal); }
        }
    }

    /// <summary>El manifiesto no se pudo leer: sin tabla de verdad el audit no puede opinar.</summary>
    public sealed class ArtContractManifestException : Exception
    {
        public ArtContractManifestException(string message) : base(message) { }
    }

    public sealed class ArtContractManifest
    {
        public const string ManifestPath = "Docs/art/manifest-refresh-visual-v1.md";
        public const int ExpectedRows = 122;
        public const int ExpectedColumns = 12;
        public const string CsvFence = "```csv";
        public const string Fence = "```";

        static readonly string[] Columns =
        {
            "pieza", "estado", "origen", "px_actual", "px_entrega", "mode_target",
            "mode_actual", "border", "max_tex", "pivot", "ppu", "address",
        };

        readonly List<ArtContractRow> rows = new List<ArtContractRow>();
        readonly Dictionary<string, ArtContractRow> byOrigen =
            new Dictionary<string, ArtContractRow>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Filas en el orden del CSV (no se reordena: el reporte cita la fila).</summary>
        public IList<ArtContractRow> Rows
        {
            get { return rows; }
        }

        /// <summary>Path absoluto del manifiesto que se leyo.</summary>
        public string SourcePath { get; private set; }

        // ------------------------------------------------------------------ carga

        /// <summary>Lee el manifiesto del proyecto actual.</summary>
        public static ArtContractManifest Load()
        {
            return Load(ProjectRoot());
        }

        /// <summary>Lee el manifiesto bajo <paramref name="projectRoot"/>.</summary>
        public static ArtContractManifest Load(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new ArtContractManifestException("projectRoot vacio");
            }

            string absolute = System.IO.Path.Combine(projectRoot, ManifestPath);
            if (!File.Exists(absolute))
            {
                throw new ArtContractManifestException("falta el manifiesto: " + ManifestPath);
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(absolute, Encoding.UTF8);
            }
            catch (IOException error)
            {
                throw new ArtContractManifestException("no se pudo leer " + ManifestPath + ": " + error.Message);
            }

            int start = -1;
            int blocks = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i].Trim(), CsvFence, StringComparison.Ordinal))
                {
                    blocks++;
                    if (start < 0)
                    {
                        start = i + 1;
                    }
                }
            }
            if (blocks == 0)
            {
                throw new ArtContractManifestException(ManifestPath + " no tiene un bloque " + CsvFence);
            }
            if (blocks > 1)
            {
                throw new ArtContractManifestException(ManifestPath + " tiene " + blocks +
                    " bloques " + CsvFence + ": la verdad tiene que ser una");
            }

            var body = new List<string>();
            for (int i = start; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(Fence, StringComparison.Ordinal))
                {
                    break;
                }
                if (lines[i].Trim().Length > 0)
                {
                    body.Add(lines[i]);
                }
            }
            if (body.Count < 2)
            {
                throw new ArtContractManifestException("el bloque " + CsvFence + " esta vacio");
            }

            var manifest = new ArtContractManifest();
            manifest.SourcePath = absolute;

            string[] header = SplitCsv(body[0]);
            if (header.Length != ExpectedColumns)
            {
                throw new ArtContractManifestException("columnas inesperadas en sec.9: " +
                    string.Join(",", header));
            }
            for (int i = 0; i < Columns.Length; i++)
            {
                if (!string.Equals(header[i], Columns[i], StringComparison.Ordinal))
                {
                    throw new ArtContractManifestException("columna " + (i + 1) + " es '" + header[i] +
                        "', el contrato espera '" + Columns[i] + "'");
                }
            }

            for (int i = 1; i < body.Count; i++)
            {
                string[] cells = SplitCsv(body[i]);
                if (cells.Length != ExpectedColumns)
                {
                    throw new ArtContractManifestException("fila " + (i + 1) + " con " + cells.Length +
                        " columnas (se esperan " + ExpectedColumns + ")");
                }

                var row = new ArtContractRow
                {
                    Pieza = cells[0],
                    Estado = cells[1],
                    Origen = cells[2],
                    PxActual = cells[3],
                    PxEntrega = cells[4],
                    ModeTarget = cells[5],
                    ModeActual = cells[6],
                    Border = cells[7],
                    MaxTex = cells[8],
                    Pivot = cells[9],
                    Ppu = cells[10],
                    Address = cells[11],
                };
                manifest.rows.Add(row);

                if (row.EsReplace && !string.IsNullOrEmpty(row.Origen) && row.Origen != "-")
                {
                    string key = NormalizePath(row.Origen);
                    if (!manifest.byOrigen.ContainsKey(key))
                    {
                        manifest.byOrigen.Add(key, row);
                    }
                }
            }

            if (manifest.rows.Count != ExpectedRows)
            {
                throw new ArtContractManifestException("sec.9 tiene " + manifest.rows.Count +
                    " filas, el contrato congela " + ExpectedRows);
            }

            return manifest;
        }

        // ------------------------------------------------------------- consultas

        /// <summary>Fila REPLACE cuyo `origen` es este asset, o null si la pieza no esta en sec.9.</summary>
        public ArtContractRow FindByOrigen(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }
            ArtContractRow row;
            return byOrigen.TryGetValue(NormalizePath(assetPath), out row) ? row : null;
        }

        /// <summary>Resumen de una linea para el encabezado del reporte.</summary>
        public string Summarize()
        {
            int replace = 0;
            int added = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].EsReplace)
                {
                    replace++;
                }
                else if (string.Equals(rows[i].Estado, "NEW", StringComparison.Ordinal))
                {
                    added++;
                }
            }
            return rows.Count + " filas (REPLACE " + replace + " | NEW " + added + ")";
        }

        // -------------------------------------------------------------- helpers

        /// <summary>"0/0/0/0" (l/b/r/t) -> Vector4 con el mismo orden que Sprite.border.</summary>
        public static bool TryParseBorder(string value, out Vector4 border)
        {
            border = Vector4.zero;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string[] parts = value.Trim().Split('/');
            if (parts.Length != 4)
            {
                return false;
            }
            float[] parsed = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed[i]))
                {
                    return false;
                }
            }
            border = new Vector4(parsed[0], parsed[1], parsed[2], parsed[3]);
            return true;
        }

        /// <summary>"WxH" -> Vector2. Devuelve false en "TBD" y en cualquier otra cosa.</summary>
        public static bool TryParseSize(string value, out Vector2 size)
        {
            size = Vector2.zero;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string text = value.Trim().ToLowerInvariant();
            int x = text.IndexOf('x');
            if (x <= 0 || x >= text.Length - 1)
            {
                return false;
            }
            float width;
            float height;
            if (!float.TryParse(text.Substring(0, x), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out width))
            {
                return false;
            }
            if (!float.TryParse(text.Substring(x + 1), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out height))
            {
                return false;
            }
            size = new Vector2(width, height);
            return true;
        }

        /// <summary>Raiz del proyecto Unity (el padre de Assets/).</summary>
        public static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        /// <summary>Path absoluto -> path del proyecto con "/" (como lo escribe sec.9).</summary>
        public static string ToProjectPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }
            string root = ProjectRoot().Replace('\\', '/');
            return absolutePath.Replace('\\', '/').Replace(root + "/", string.Empty);
        }

        /// <summary>Normaliza separadores para comparar paths de sec.9 contra AssetDatabase.</summary>
        public static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        /// <summary>Separador CSV con soporte de comillas dobles (misma regla que el auditor Python).</summary>
        public static string[] SplitCsv(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
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
    }
}
#endif
