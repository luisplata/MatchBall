// Report: la salida del audit de UI en dos formatos. Tabla humana + resumen machine-readable.
//
// Por que dos: el humano revisa la tabla agrupada por requisito (ADC + UCC + ULN), y el CI de
// mas adelante necesita un JSON estable para comparar corridas sin parsear Markdown. Los dos
// salen de la misma estructura, asi que no pueden discrepar.
//
// Reglas duras:
//   * Escribe SOLO bajo Temp/art-contract/reports/ (fuera de Assets/, no versionado, mismo
//     directorio que ArtMetaMigrator). Nunca toca Docs/ ni Assets/.
//   * Orden determinista: requisito (orden fijo), asset, jerarquia, valor. Dos corridas sobre el
//     mismo arbol producen el mismo cuerpo (cambia solo el timestamp del nombre y del encabezado).
//   * Un hallazgo se imprime completo: requisito, asset, jerarquia, valor ofensivo y esperado.
//     Un reporte sin el valor esperado no sirve para arreglar nada.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkVoid.ArtContract
{
    public static class ArtContractReport
    {
        public const string ReportDir = UiContractAudit.ReportDir;

        /// <summary>Filas por requisito en el resumen que va a la consola (el .md las tiene todas).</summary>
        const int ConsoleRowsPerRequirement = 3;

        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // ------------------------------------------------------------------ write

        /// <summary>Escribe el .md y el .json. Deja los paths en el resultado.</summary>
        public static void Write(ArtContractAuditResult result)
        {
            string directory = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), ReportDir);
            Directory.CreateDirectory(directory);

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string markdown = System.IO.Path.Combine(directory, "ui-contract-audit-" + stamp + ".md");
            string json = System.IO.Path.Combine(directory, "ui-contract-audit-" + stamp + ".json");

            File.WriteAllText(markdown, BuildMarkdown(result), Utf8NoBom);
            File.WriteAllText(json, BuildJson(result), Utf8NoBom);

            result.ReportPath = ArtContractManifest.ToProjectPath(markdown);
            result.JsonPath = ArtContractManifest.ToProjectPath(json);
        }

        // -------------------------------------------------------------------- log

        /// <summary>Resumen legible en la consola del Editor: conteos, 3 filas por requisito y paths.</summary>
        public static void LogSummary(ArtContractAuditResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("[ArtContract] UI Audit (" + UiContractAudit.MenuItemPath + ")");
            text.AppendLine("[ArtContract] escenas " + result.Scenes.Count + " | canvas de escena " +
                result.SceneCanvases + " | prefabs " + result.Prefabs.Count + " | canvas de prefab " +
                result.PrefabCanvases + " | canvas de prefab vistos en escenas " + result.PrefabCanvasesInScenes);
            text.AppendLine("[ArtContract] sec.9 " + result.ManifestSummary);
            text.AppendLine("[ArtContract] git Assets/Scenes+Assets/Prefabs: " + result.GitSummary +
                (result.ReadOnlyViolated ? "  <<< EL AUDIT MODIFICO EL ARBOL" : string.Empty));
            text.AppendLine("[ArtContract] hallazgos " + result.Findings.Count + " en " +
                CountRequirements(result) + " requisitos");

            IList<string> requirements = OrderedRequirements(result);
            for (int i = 0; i < requirements.Count; i++)
            {
                string requirement = requirements[i];
                int count = result.Findings.CountFor(requirement);
                text.AppendLine("[ArtContract]   " + Pad(requirement, 14) + " " + PadLeft(count.ToString(CultureInfo.InvariantCulture), 4) +
                    "  " + ArtContractRules.DescribeRequirement(requirement));
            }

            text.AppendLine("[ArtContract] primeros hallazgos por requisito:");
            for (int i = 0; i < requirements.Count; i++)
            {
                string requirement = requirements[i];
                List<ArtContractFinding> findings = FindingsFor(result, requirement);
                for (int f = 0; f < findings.Count && f < ConsoleRowsPerRequirement; f++)
                {
                    ArtContractFinding finding = findings[f];
                    text.AppendLine("[ArtContract]   " + requirement + " " + finding.AssetPath + " :: " +
                        finding.HierarchyPath + " :: " + finding.Observed + "  ->  " + finding.Expected);
                }
                if (findings.Count > ConsoleRowsPerRequirement)
                {
                    text.AppendLine("[ArtContract]   " + requirement + " ... y " +
                        (findings.Count - ConsoleRowsPerRequirement) + " mas (ver el .md)");
                }
            }

            if (result.Errors.Count > 0)
            {
                text.AppendLine("[ArtContract] errores " + result.Errors.Count + ":");
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    text.AppendLine("[ArtContract]   " + result.Errors[i]);
                }
            }

            if (!string.IsNullOrEmpty(result.ReportPath))
            {
                text.AppendLine("[ArtContract] reporte " + result.ReportPath);
            }
            if (!string.IsNullOrEmpty(result.JsonPath))
            {
                text.AppendLine("[ArtContract] json    " + result.JsonPath);
            }
            text.AppendLine("[ArtContract] RESULTADO " + (result.ExitCode == 0 ? "PASS" : "FAIL") +
                " (exit " + result.ExitCode + ")");

            Debug.Log(text.ToString());
        }

        // --------------------------------------------------------------- markdown

        static string BuildMarkdown(ArtContractAuditResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("# UI Contract Audit — " + result.StartedUtc);
            text.AppendLine();
            text.AppendLine("Generado por `Assets/Editor/ArtContract/UiContractAudit.cs` "
                + "(menu `" + UiContractAudit.MenuItemPath + "`). Solo lectura: no guarda escenas ni assets.");
            text.AppendLine();
            text.AppendLine("| Campo | Valor |");
            text.AppendLine("|---|---|");
            text.AppendLine("| Unity | " + Application.unityVersion + " |");
            text.AppendLine("| sec.9 | `" + ArtContractManifest.ManifestPath + "` — " + result.ManifestSummary + " |");
            text.AppendLine("| Escenas | " + result.Scenes.Count + " (`" + result.SceneRoot + "`) |");
            text.AppendLine("| Canvas de escena | " + result.SceneCanvases + " |");
            text.AppendLine("| Prefabs | " + result.Prefabs.Count + " (`" + result.PrefabRoot + "`) |");
            text.AppendLine("| Canvas de prefab | " + result.PrefabCanvases + " |");
            text.AppendLine("| Hallazgos | " + result.Findings.Count + " |");
            text.AppendLine("| Errores | " + result.Errors.Count + " |");
            text.AppendLine("| git (`Assets/Scenes` + `Assets/Prefabs`) | " + result.GitSummary + " |");
            text.AppendLine("| Exit code | " + result.ExitCode + " |");
            text.AppendLine();

            text.AppendLine("## Conteo por requisito");
            text.AppendLine();
            text.AppendLine("| REQ | Que mide | Hallazgos |");
            text.AppendLine("|---|---|---|");
            IList<string> requirements = OrderedRequirements(result);
            for (int i = 0; i < requirements.Count; i++)
            {
                string requirement = requirements[i];
                text.AppendLine("| " + requirement + " | " + ArtContractRules.DescribeRequirement(requirement) +
                    " | " + result.Findings.CountFor(requirement) + " |");
            }
            text.AppendLine("| **TOTAL** | | **" + result.Findings.Count + "** |");
            text.AppendLine();

            text.AppendLine("## Hallazgos");
            for (int i = 0; i < requirements.Count; i++)
            {
                string requirement = requirements[i];
                List<ArtContractFinding> findings = FindingsFor(result, requirement);
                text.AppendLine();
                text.AppendLine("### " + requirement + " — " + ArtContractRules.DescribeRequirement(requirement) +
                    " — " + findings.Count);
                text.AppendLine();
                if (findings.Count == 0)
                {
                    text.AppendLine("Sin hallazgos.");
                    continue;
                }
                text.AppendLine("| # | Asset | Jerarquia | Valor | Esperado |");
                text.AppendLine("|---|---|---|---|---|");
                for (int f = 0; f < findings.Count; f++)
                {
                    ArtContractFinding finding = findings[f];
                    string observed = finding.Observed;
                    if (finding.Detail != null)
                    {
                        observed = observed + " (" + finding.Detail + ")";
                    }
                    if (finding.Repeats > 1)
                    {
                        observed = observed + " [x" + finding.Repeats + "]";
                    }
                    text.AppendLine("| " + (f + 1) + " | " + Cell(finding.AssetPath) + " | " +
                        Cell(finding.HierarchyPath) + " | " + Cell(observed) + " | " + Cell(finding.Expected) + " |");
                }
            }
            text.AppendLine();

            text.AppendLine("## Excepciones y omisiones declaradas");
            text.AppendLine();
            for (int i = 0; i < result.Declared.Count; i++)
            {
                text.AppendLine("- " + result.Declared[i]);
            }
            text.AppendLine();

            text.AppendLine("## Contadores");
            text.AppendLine();
            text.AppendLine("| Clave | Veces |");
            text.AppendLine("|---|---|");
            IList<KeyValuePair<string, int>> counters = result.Counters.Items;
            for (int i = 0; i < counters.Count; i++)
            {
                text.AppendLine("| " + counters[i].Key + " | " + counters[i].Value + " |");
            }
            text.AppendLine();

            text.AppendLine("## Alcance");
            text.AppendLine();
            text.AppendLine("Escenas auditadas: " + result.Scenes.Count);
            for (int i = 0; i < result.Scenes.Count; i++)
            {
                text.AppendLine("- `" + result.Scenes[i] + "`");
            }
            text.AppendLine();
            text.AppendLine("Prefabs auditados: " + result.Prefabs.Count);
            for (int i = 0; i < result.Prefabs.Count; i++)
            {
                text.AppendLine("- `" + result.Prefabs[i] + "`");
            }

            if (result.Errors.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Errores");
                text.AppendLine();
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    text.AppendLine("- " + result.Errors[i]);
                }
            }

            return text.ToString();
        }

        // ------------------------------------------------------------------- json

        static string BuildJson(ArtContractAuditResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("{");
            text.AppendLine("  \"tool\": \"Assets/Editor/ArtContract/UiContractAudit.cs\",");
            text.AppendLine("  \"menu\": " + Quote(UiContractAudit.MenuItemPath) + ",");
            text.AppendLine("  \"unity\": " + Quote(Application.unityVersion) + ",");
            text.AppendLine("  \"started_utc\": " + Quote(result.StartedUtc) + ",");
            text.AppendLine("  \"manifest\": {");
            text.AppendLine("    \"path\": " + Quote(ArtContractManifest.ManifestPath) + ",");
            text.AppendLine("    \"summary\": " + Quote(result.ManifestSummary));
            text.AppendLine("  },");
            text.AppendLine("  \"scope\": {");
            text.AppendLine("    \"scene_root\": " + Quote(result.SceneRoot) + ",");
            text.AppendLine("    \"prefab_root\": " + Quote(result.PrefabRoot) + ",");
            text.AppendLine("    \"scenes\": " + QuoteArray(result.Scenes) + ",");
            text.AppendLine("    \"prefabs\": " + QuoteArray(result.Prefabs));
            text.AppendLine("  },");
            text.AppendLine("  \"counts\": {");
            text.AppendLine("    \"scenes\": " + result.Scenes.Count + ",");
            text.AppendLine("    \"prefabs\": " + result.Prefabs.Count + ",");
            text.AppendLine("    \"scene_canvases\": " + result.SceneCanvases + ",");
            text.AppendLine("    \"prefab_canvases\": " + result.PrefabCanvases + ",");
            text.AppendLine("    \"prefab_canvases_in_scenes\": " + result.PrefabCanvasesInScenes + ",");
            text.AppendLine("    \"findings\": " + result.Findings.Count + ",");
            text.AppendLine("    \"errors\": " + result.Errors.Count + ",");
            text.AppendLine("    \"by_requirement\": {");
            IList<string> requirements = OrderedRequirements(result);
            for (int i = 0; i < requirements.Count; i++)
            {
                string requirement = requirements[i];
                string comma = i == requirements.Count - 1 ? string.Empty : ",";
                text.AppendLine("      " + Quote(requirement) + ": " +
                    result.Findings.CountFor(requirement) + comma);
            }
            text.AppendLine("    }");
            text.AppendLine("  },");
            text.AppendLine("  \"findings\": [");
            List<ArtContractFinding> findings = AllFindings(result);
            for (int i = 0; i < findings.Count; i++)
            {
                ArtContractFinding finding = findings[i];
                text.AppendLine("    {");
                text.AppendLine("      \"requirement\": " + Quote(finding.Requirement) + ",");
                text.AppendLine("      \"asset\": " + Quote(finding.AssetPath) + ",");
                text.AppendLine("      \"hierarchy\": " + Quote(finding.HierarchyPath) + ",");
                text.AppendLine("      \"observed\": " + Quote(finding.Observed) + ",");
                text.AppendLine("      \"expected\": " + Quote(finding.Expected) + ",");
                text.AppendLine("      \"detail\": " + Quote(finding.Detail) + ",");
                text.AppendLine("      \"repeats\": " + finding.Repeats);
                text.AppendLine(i == findings.Count - 1 ? "    }" : "    },");
            }
            text.AppendLine("  ],");
            text.AppendLine("  \"counters\": {");
            IList<KeyValuePair<string, int>> counters = result.Counters.Items;
            for (int i = 0; i < counters.Count; i++)
            {
                string comma = i == counters.Count - 1 ? string.Empty : ",";
                text.AppendLine("    " + Quote(counters[i].Key) + ": " + counters[i].Value + comma);
            }
            text.AppendLine("  },");
            text.AppendLine("  \"declared\": " + QuoteArray(result.Declared) + ",");
            text.AppendLine("  \"errors\": " + QuoteArray(result.Errors) + ",");
            text.AppendLine("  \"git\": {");
            text.AppendLine("    \"available\": " + (result.GitAvailable ? "true" : "false") + ",");
            text.AppendLine("    \"before\": " + Quote(result.GitBefore) + ",");
            text.AppendLine("    \"after\": " + Quote(result.GitAfter) + ",");
            text.AppendLine("    \"readonly_violated\": " + (result.ReadOnlyViolated ? "true" : "false"));
            text.AppendLine("  },");
            text.AppendLine("  \"exit_code\": " + result.ExitCode);
            text.AppendLine("}");
            return text.ToString();
        }

        // -------------------------------------------------------------- ordering

        static IList<string> OrderedRequirements(ArtContractAuditResult result)
        {
            var ordered = new List<string>();
            string[] known = ArtContractRules.RequirementOrder;
            for (int i = 0; i < known.Length; i++)
            {
                if (result.Findings.CountFor(known[i]) > 0)
                {
                    ordered.Add(known[i]);
                }
            }
            // Requisito que no este en el orden fijo (defensivo) va al final, no se pierde.
            for (int i = 0; i < result.Findings.Items.Count; i++)
            {
                string requirement = result.Findings.Items[i].Requirement;
                if (!ordered.Contains(requirement))
                {
                    ordered.Add(requirement);
                }
            }
            for (int i = 0; i < known.Length; i++)
            {
                if (!ordered.Contains(known[i]) && result.Findings.CountFor(known[i]) > 0)
                {
                    ordered.Add(known[i]);
                }
            }
            return ordered;
        }

        static int CountRequirements(ArtContractAuditResult result)
        {
            return OrderedRequirements(result).Count;
        }

        static List<ArtContractFinding> FindingsFor(ArtContractAuditResult result, string requirement)
        {
            var list = new List<ArtContractFinding>();
            for (int i = 0; i < result.Findings.Items.Count; i++)
            {
                ArtContractFinding finding = result.Findings.Items[i];
                if (string.Equals(finding.Requirement, requirement, StringComparison.Ordinal))
                {
                    list.Add(finding);
                }
            }
            list.Sort(CompareFindings);
            return list;
        }

        static List<ArtContractFinding> AllFindings(ArtContractAuditResult result)
        {
            var list = new List<ArtContractFinding>();
            IList<string> requirements = OrderedRequirements(result);
            for (int i = 0; i < requirements.Count; i++)
            {
                list.AddRange(FindingsFor(result, requirements[i]));
            }
            return list;
        }

        static int CompareFindings(ArtContractFinding left, ArtContractFinding right)
        {
            int byAsset = string.CompareOrdinal(left.AssetPath, right.AssetPath);
            if (byAsset != 0)
            {
                return byAsset;
            }
            int byHierarchy = string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath);
            if (byHierarchy != 0)
            {
                return byHierarchy;
            }
            int byObserved = string.CompareOrdinal(left.Observed, right.Observed);
            if (byObserved != 0)
            {
                return byObserved;
            }
            return string.CompareOrdinal(left.Detail ?? string.Empty, right.Detail ?? string.Empty);
        }

        // --------------------------------------------------------------- strings

        static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }
            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        static string Pad(string value, int width)
        {
            if (value.Length >= width)
            {
                return value;
            }
            return value + new string(' ', width - value.Length);
        }

        static string PadLeft(string value, int width)
        {
            if (value.Length >= width)
            {
                return value;
            }
            return new string(' ', width - value.Length) + value;
        }

        static string QuoteArray(IList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "[]";
            }
            var text = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }
                text.Append(Quote(values[i]));
            }
            text.Append("]");
            return text.ToString();
        }

        /// <summary>String JSON: comillas, backslash y controles. Sin dependencias.</summary>
        static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }
            var text = new StringBuilder(value.Length + 2);
            text.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        text.Append("\\r");
                        break;
                    case '\t':
                        text.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            text.Append("\\u");
                            text.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            text.Append(character);
                        }
                        break;
                }
            }
            text.Append('"');
            return text.ToString();
        }
    }
}
#endif
