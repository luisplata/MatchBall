// UiContractAudit: el orquestador de la capa 2 del design (§1). Abre escenas y prefabs, aplica
// las reglas de Rules.cs y devuelve el resultado. NO escribe nada bajo Assets/.
//
// Frontera con el auditor Python (tools/art/audit_art_manifest.py): ese lee bytes + .meta + YAML
// y cubre ASC-1..ASC-5/ASC-7 sin abrir Unity (ASC-8 literal). Este cubre lo que solo Unity ve:
// render mode efectivo, CanvasScaler, border resuelto por GUID, RectTransform y area segura. No
// se duplican reglas: aca no se mira un pixel ni un .meta.
//
// Reglas duras:
//   * Solo lectura. Nunca SaveScene, nunca AssetDatabase.SaveAssets, nunca SetDirty. Las escenas
//     se abren con OpenScene(..., Additive) y se cierran con CloseScene(scene, true): lo que
//     Unity haya tocado en memoria durante la lectura se descarta, no se guarda.
//   * Una escena que ya estaba abierta NO se cierra (seria secuestrarle el editor al usuario) y
//     no se le fuerza nada.
//   * Detecta su propio pecado: si `git status --porcelain -- Assets/Scenes Assets/Prefabs`
//     cambia durante la corrida, eso es un bug del tool y se reporta como AUDIT-READONLY.
//   * Lo que no se audita se declara (Counters + Declared). Una omision silenciosa es una mentira.
//
// Uso:
//   Tools > Art Contract > UI Audit
//   Unity -batchmode -quit -executeMethod DarkVoid.ArtContract.UiContractAudit.BatchAudit
//
// Exit codes:
//   0 = PASS (0 hallazgos)        1 = hay hallazgos
//   2 = sec.9 ilegible            3 = el audit ensucio Assets/ (bug del tool)
//   4 = play mode                 5 = error interno del audit
//   6 = errores de carga (assets) sin hallazgos que explicarlos

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace DarkVoid.ArtContract
{
    /// <summary>Todo lo que produce una corrida. Lo consume Report.cs y lo devuelve Run().</summary>
    public sealed class ArtContractAuditResult
    {
        public string StartedUtc;
        public string ManifestSummary = "no leido";
        public string SceneRoot = "Assets/Scenes";
        public string PrefabRoot = "Assets/Prefabs";

        public readonly ArtContractFindings Findings = new ArtContractFindings();
        public readonly ArtContractCounters Counters = new ArtContractCounters();
        public readonly List<string> Scenes = new List<string>();
        public readonly List<string> Prefabs = new List<string>();
        public readonly List<string> Declared = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public int SceneCanvases;
        public int PrefabCanvases;
        public int PrefabCanvasesInScenes;

        public bool GitAvailable;
        public string GitBefore = string.Empty;
        public string GitAfter = string.Empty;
        public bool ReadOnlyViolated;

        public int ExitCode;

        /// <summary>Paths de los reportes de esta corrida (los escribe ArtContractReport).</summary>
        public string ReportPath = string.Empty;
        public string JsonPath = string.Empty;

        public string GitSummary
        {
            get
            {
                if (!GitAvailable)
                {
                    return "no verificable (git no disponible)";
                }
                string before = string.IsNullOrEmpty(GitBefore) ? "limpio" : "sucio";
                string after = string.IsNullOrEmpty(GitAfter) ? "limpio" : "sucio";
                return before + " -> " + after;
            }
        }
    }

    public static class UiContractAudit
    {
        public const string MenuItemPath = "Tools/Art Contract/UI Audit";
        public const string ReportDir = "Temp/art-contract/reports";
        public const string SceneRoot = "Assets/Scenes";
        public const string PrefabRoot = "Assets/Prefabs";

        static readonly string[] GitPaths = { "--", "Assets/Scenes", "Assets/Prefabs" };

        // ------------------------------------------------------------------ menu

        [MenuItem(MenuItemPath, false, 2100)]
        public static void AuditMenu()
        {
            Run();
        }

        // ------------------------------------------------------------- entrypoints

        /// <summary>Entrada para -batchmode -executeMethod. En batch, sale con el exit code del run.</summary>
        public static void BatchAudit()
        {
            int code = Run();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        /// <summary>Corre el audit completo, escribe el reporte y loguea el resumen. Devuelve el exit code.</summary>
        public static int Run()
        {
            var result = new ArtContractAuditResult();
            result.StartedUtc = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                const string message = "El audit no corre en play mode: abrir escenas aditivas ahi "
                    + "serializa estado de juego. Salir del play mode y reintentar.";
                result.Errors.Add(message);
                result.ExitCode = 4;
                Debug.LogError("[ArtContract] " + message);
                return 4;
            }

            Debug.Log("[ArtContract] UI Audit: arranca. Unity " + Application.unityVersion +
                ", escenas " + SceneRoot + ", prefabs " + PrefabRoot);

            bool manifestOk = true;
            ArtContractManifest manifest = null;
            try
            {
                manifest = ArtContractManifest.Load();
                result.ManifestSummary = manifest.Summarize();
            }
            catch (Exception error)
            {
                manifestOk = false;
                result.Errors.Add("sec.9: " + error.Message);
            }

            result.GitBefore = ReadGitStatus(out result.GitAvailable);

            try
            {
                AuditPrefabs(manifest, result);
                AuditScenes(manifest, result);
            }
            catch (Exception error)
            {
                result.Errors.Add("audit: " + error.GetType().Name + ": " + error.Message);
            }

            bool ignoredAvailability;
            result.GitAfter = ReadGitStatus(out ignoredAvailability);
            result.ReadOnlyViolated = result.GitAvailable &&
                !string.Equals(result.GitBefore, result.GitAfter, StringComparison.Ordinal);
            if (result.ReadOnlyViolated)
            {
                result.Findings.Add("AUDIT-READONLY", SceneRoot + " + " + PrefabRoot, "-",
                    "git status cambio durante la corrida", "arbol identico antes y despues (el audit es de solo lectura)");
            }

            AddDeclaredNotes(result);

            // El codigo mas grave gana: un bug de lectura del tool no puede quedar escondido
            // detras de 200 hallazgos de layout.
            if (!manifestOk)
            {
                result.ExitCode = 2;
            }
            else if (result.ReadOnlyViolated)
            {
                result.ExitCode = 3;
            }
            else if (result.Errors.Count > 0 && result.Findings.Count == 0)
            {
                result.ExitCode = 6;
            }
            else if (result.Findings.Count > 0 || result.Errors.Count > 0)
            {
                result.ExitCode = 1;
            }
            else
            {
                result.ExitCode = 0;
            }

            try
            {
                ArtContractReport.Write(result);
            }
            catch (Exception error)
            {
                result.Errors.Add("reporte: " + error.Message);
                if (result.ExitCode == 0)
                {
                    result.ExitCode = 5;
                }
            }

            ArtContractReport.LogSummary(result);
            return result.ExitCode;
        }

        // -------------------------------------------------------------- prefabs

        static void AuditPrefabs(ArtContractManifest manifest, ArtContractAuditResult result)
        {
            List<string> paths = FindAssetPaths("t:Prefab", PrefabRoot);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    result.Errors.Add("no se pudo cargar el prefab " + path);
                    continue;
                }

                result.Prefabs.Add(path);

                RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
                for (int r = 0; r < rects.Length; r++)
                {
                    ArtContractRules.CheckRectTransform(rects[r], path, result.Findings, result.Counters);
                }

                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int m = 0; m < images.Length; m++)
                {
                    ArtContractRules.CheckImage(images[m], manifest, path, result.Findings);
                }

                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    Canvas canvas = canvases[c];
                    if (ArtContractRules.HasAncestorCanvas(canvas.transform))
                    {
                        // Canvas anidado: hereda el area del raiz y su contrato es el Page (F-S1c-6).
                        result.Counters.Bump("ucc3.canvasAnidado");
                        continue;
                    }
                    result.PrefabCanvases++;
                    ArtContractRules.CheckScaler(canvas, path, result.Findings);
                    ArtContractRules.CheckPrefabCanvas(canvas, path, result.Findings);
                }
            }
        }

        // --------------------------------------------------------------- escenas

        static void AuditScenes(ArtContractManifest manifest, ArtContractAuditResult result)
        {
            List<string> paths = FindAssetPaths("t:Scene", SceneRoot);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                bool openedByUs = false;
                bool wasDirty = alreadyOpen && scene.isDirty;

                if (!alreadyOpen)
                {
                    try
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        openedByUs = true;
                    }
                    catch (Exception error)
                    {
                        result.Errors.Add("no se pudo abrir " + path + ": " + error.Message);
                        continue;
                    }
                }

                result.Scenes.Add(path);
                try
                {
                    AuditScene(scene, path, manifest, result);
                }
                catch (Exception error)
                {
                    result.Errors.Add(path + ": " + error.GetType().Name + ": " + error.Message);
                }
                finally
                {
                    // Nunca se guarda. Si la abrimos nosotros, se descarta lo que Unity haya
                    // refluido en memoria (AspectRatioFitter es [ExecuteAlways]).
                    if (openedByUs)
                    {
                        if (scene.isDirty)
                        {
                            result.Counters.Bump("escenaSuciaAlCerrar");
                        }
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    else if (!wasDirty && scene.isDirty)
                    {
                        // Una escena que ya estaba abierta se ensucio durante la lectura: no la
                        // tocamos (no hay API publica para limpiarla) pero el run lo declara.
                        result.Findings.Add("AUDIT-READONLY", path, "-",
                            "la escena abierta paso de limpia a sucia durante la corrida",
                            "ninguna escritura sobre una escena abierta por el usuario");
                    }
                }
            }
        }

        static void AuditScene(Scene scene, string path, ArtContractManifest manifest,
            ArtContractAuditResult result)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var sceneCanvases = new List<Canvas>();

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];

                RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
                for (int r = 0; r < rects.Length; r++)
                {
                    string assetPath = ArtContractRules.ResolveSourceAsset(rects[r].gameObject, path);
                    ArtContractRules.CheckRectTransform(rects[r], assetPath, result.Findings, result.Counters);
                }

                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int m = 0; m < images.Length; m++)
                {
                    string assetPath = ArtContractRules.ResolveSourceAsset(images[m].gameObject, path);
                    ArtContractRules.CheckImage(images[m], manifest, assetPath, result.Findings);
                }

                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    Canvas canvas = canvases[c];
                    if (ArtContractRules.IsInPrefabInstance(canvas.gameObject))
                    {
                        // UCC-1/2/3 de los canvas de prefab se auditan una sola vez, en el pase de
                        // prefabs: en la escena solo se veria el valor heredado (F-S1c-6).
                        result.PrefabCanvasesInScenes++;
                        continue;
                    }
                    sceneCanvases.Add(canvas);
                }
            }

            for (int c = 0; c < sceneCanvases.Count; c++)
            {
                result.SceneCanvases++;
                ArtContractRules.CheckScaler(sceneCanvases[c], path, result.Findings);
                ArtContractRules.CheckSceneCanvasRenderMode(sceneCanvases[c], path, result.Findings);
            }

            ArtContractRules.CheckSceneCanvasUniformity(sceneCanvases, path, result.Findings);

            for (int c = 0; c < sceneCanvases.Count; c++)
            {
                ArtContractRules.CheckSafeArea(sceneCanvases[c], path, result.Findings, result.Counters);
            }
        }

        // ------------------------------------------------------------- inventory

        static List<string> FindAssetPaths(string filter, string root)
        {
            var paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets(filter, new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        // ------------------------------------------------------------------- git

        /// <summary>
        /// `git status --porcelain -- Assets/Scenes Assets/Prefabs`. Devuelve el texto crudo; si
        /// git no esta, devuelve vacio y available=false (el reporte dice "no verificable").
        /// </summary>
        static string ReadGitStatus(out bool available)
        {
            available = false;
            var start = new ProcessStartInfo("git", "status --porcelain -- Assets/Scenes Assets/Prefabs")
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
                        return string.Empty;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    available = process.ExitCode == 0;
                    return output.Replace("\r", string.Empty).Trim();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // -------------------------------------------------------------- declared

        static void AddDeclaredNotes(ArtContractAuditResult result)
        {
            result.Declared.Add("UCC-3: los 3 canvas de prefab siguen ScreenSpaceOverlay con root Page "
                + "(design D4: un prefab no puede serializar la camara de la escena). Se audita el Page, no el render mode.");
            result.Declared.Add("UCC-3: la coincidencia pixel a pixel del area 9:19 se verifica por captura "
                + "(V2 del design §9); aca se verifica render mode + camara + area uniforme por escena.");
            result.Declared.Add("UCC-5: no se mide en canvas de prefab; el layout de su contenido es S5-S10 (design D5).");
            result.Declared.Add("ULN-1: el subtree de HUD de Game ("
                + ArtContractRules.HudAuthoringWidth + " u a escala " + ArtContractRules.HudAuthoringScale
                + ") es excepcion declarada del design §5: no se re-refluye en este cambio.");
            result.Declared.Add("ULN-1: el RectTransform de un Canvas lo gobierna el Canvas y el de un root Page "
                + "lo escribe su AspectRatioFitter: no se auditan.");
            result.Declared.Add("UCC-5: solo se mide lo visible en el Editor. Un objeto inactivo no se "
                + "audita (" + result.Counters.Get("ucc5.inactivo") + " componentes): su layout se "
                + "verifica cuando S10 abra el panel (V2 del design §9).");
            result.Declared.Add("UCC-5: el contenido de un ScrollRect se recorta por diseno y no se audita.");
            result.Declared.Add("UCC-5: un canvas inactivo no tiene rect medible ("
                + (result.Counters.Get("ucc5.canvasSinRect") + result.Counters.Get("ucc5.sinRect"))
                + " canvas); se audita cuando la escena lo muestre.");
            result.Declared.Add("Alcance: escenas bajo " + SceneRoot + " y prefabs bajo " + PrefabRoot
                + ". Terceros y Assets/_Recovery quedan fuera (no son pantallas del juego).");

            result.Declared.Add("Omitidos por rect distinto de 900x1900 (poner el Game view en 900x1900 para medir UCC-5): "
                + result.Counters.Get("ucc5.canvasFueraDe919") + " canvas.");
            result.Declared.Add("ULN-1: se auditaron " + result.Counters.Get("uln1.rectTotal") +
                " RectTransform (escenas + prefabs, instancias incluidas); se omitieron " +
                (result.Counters.Get("uln1.layoutGroup") + result.Counters.Get("uln1.canvasRoot") +
                 result.Counters.Get("uln1.aspectFitter") + result.Counters.Get("uln1.hudAuthoring")) +
                " por excepcion declarada (LayoutGroup, Canvas, AspectRatioFitter, HUD).");
            result.Declared.Add("Canvas anidados de prefab (heredan el area del raiz): "
                + result.Counters.Get("ucc3.canvasAnidado") + ".");
            result.Declared.Add("Escenas que quedaron sucias al cerrarlas (descartadas, no guardadas): "
                + result.Counters.Get("escenaSuciaAlCerrar") + ".");
        }
    }
}
#endif
