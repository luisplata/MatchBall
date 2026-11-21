// ArtCaptureRig: rig VERSIONADO de capturas de placeholders (DRL-3 / DRL-4).
//
// Por que existe: las 24 capturas de `Docs/art/captures/placeholders-v1/` se produjeron con un
// script efimero. "La captura existe" no probaba nada: sin el rig versionado la evidencia no se
// puede regenerar, y el diff acotado de DRL-4 no tiene con que comparar. Este archivo es el rig:
// un entrypoint (`BatchCapture`), una salida (`Docs/art/evidence/captures-v1.md`) y una captura
// por fila `cableado` del mapa que declara `captura`.
//
// Metodo (declarado, porque es lo que hace honesta la captura):
//   1. Agrupa las filas por el asset que declaran. Un `.unity` se abre tal cual; un `.prefab` sin
//      Canvas propio se captura sobre la instancia que el juego tiene de el en una escena (la
//      primera escena, ordenada, de la que el prefab es dependencia). Nunca se instancia nada.
//   2. Resuelve el `nodo` del mapa: relativo a la raiz del prefab, o el path completo en la escena.
//   3. Fuerza SOLO lo que la fila declara: cadena de ancestros activa, `m_Color` que APW-3 exige
//      (el mismo que mide `check_placeholder_wiring.py`), el estado del `Selectable` de su `campo`,
//      `TutorialStepDots.SetCurrent(1)` para los dots y la posicion abierta del panel de
//      `PanelFloatingUiRules`. Nada de play mode: el editor no bombea frames, y una captura en
//      play mode depende del frame.
//   4. Renderiza la camara del canvas (`Canvas.worldCamera`, ScreenSpaceCamera) a una
//      `RenderTexture` de 900x1900 (la resolucion de referencia de todos los `CanvasScaler`) con
//      `ReadPixels`, y recorta por la proyeccion del `RectTransform` del nodo.
//   5. Escribe el PNG en `Docs/art/captures/placeholders-v1/<captura>` y la fila (px, bbox en el
//      marco de 900x1900, colores) en el bloque csv de `Docs/art/evidence/captures-v1.md`.
//
// Reglas duras:
//   * Nunca `ScreenCapture`: la resolucion es la del contrato, no la del Game view.
//   * NO escribe `Assets/`: las escenas se abren desde disco y se recargan para descartar el estado
//     forzado. Ningun `.meta` se reescribe (15 piezas en Single serializan un bloque de tamano
//     lienzo: un cambio de px las volveria `RECT-STALE`).
//   * Nada de hashes de captura (DRL-4): la evidencia es px + bbox + colores medidos.
//   * Se niega a correr con una escena sucia: abrir otra escena tiraria esos cambios.
//   * Una fila cuyo nodo no proyecta dentro del canvas es `OFF-CANVAS`: se falla con el nombre, no
//     se escribe un PNG vacio (un bitmap limpio y falso es peor que un hueco declarado).
//
// Negativos de DRL-4: `CaptureSwapDrill(bboxPiece, nodePiece, spritePiece, outDir)` emite DOS
// marcos del MISMO estado (antes, y despues de asignar en memoria el `Sprite` de una pieza al nodo
// de otra) a un directorio explicito. No pisa bytes ni escribe paths versionados:
// `tools/art/check_capture_diff.py` compara esos dos marcos contra el bbox de `bboxPiece`.
//
// Uso:
//   Tools > Art Contract > Capture rig > Capture all (PNGs + evidencia)
//   Unity -batchmode -quit -executeMethod DarkVoid.ArtContract.ArtCaptureRig.BatchCapture
//   Tools > Art Contract > Capture rig > Swap drill (negativos de DRL-4, Temp/)

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace DarkVoid.ArtContract
{
    public static class ArtCaptureRig
    {
        public const string MapPath = "Docs/art/placeholders-v1.md";
        public const string CaptureDir = "Docs/art/captures/placeholders-v1";
        public const string EvidencePath = "Docs/art/evidence/captures-v1.md";
        public const int Width = 900;
        public const int Height = 1900;
        public const string FrameSize = "900x1900";
        public const string SwapDrillDir = "Temp/art-captures/swap-drill";

        const string MenuRoot = "Tools/Art Contract/Capture rig/";
        const string Cableado = "cableado";
        const string SceneExtension = ".unity";
        const string PrefabExtension = ".prefab";

        // ------------------------------------------------------------------- modelo

        sealed class MapRow
        {
            public string Pieza;
            public string SlotTipo;
            public string Asset;
            public string Nodo;
            public string Componente;
            public string Campo;
            public string Estado;
            public string Tint;
            public string Captura;

            public bool IsCableado
            {
                get { return string.Equals(Estado, Cableado, StringComparison.Ordinal); }
            }

            public bool HasCapture
            {
                get { return !string.IsNullOrEmpty(Captura); }
            }
        }

        sealed class Shot
        {
            public MapRow Row;
            public int W;
            public int H;
            public int Bx;
            public int By;
            public int Bw;
            public int Bh;
            public int Colours;
        }

        sealed class Group
        {
            public string ScenePath;
            public readonly List<MapRow> Rows = new List<MapRow>();
        }

        // -------------------------------------------------------------- entrypoints

        [MenuItem(MenuRoot + "Capture all (PNGs + evidencia)", false, 2200)]
        public static void CaptureAllMenu()
        {
            CaptureAll();
        }

        [MenuItem(MenuRoot + "Swap drill (negativos DRL-4, Temp/)", false, 2201)]
        public static void SwapDrillMenu()
        {
            int code = CaptureSwapDrill("ui_tutorial_btn_skip_normal_01.png", null, null, SwapDrillDir);
            Log("swap drill (sin swap) terminado · exit " + code);
        }

        /// <summary>Entrypoint de `-executeMethod`: capturas versionadas + evidencia.</summary>
        public static void BatchCapture()
        {
            int code = CaptureAll();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        /// <summary>
        /// Captura una fila por cada fila `cableado` del mapa con `captura` declarada y reescribe
        /// `Docs/art/evidence/captures-v1.md`. Devuelve 0 si todas se escribieron.
        /// </summary>
        public static int CaptureAll()
        {
            return Run();
        }

        /// <summary>
        /// DRL-4: dos marcos del MISMO estado en <paramref name="outDir"/> —`before.png` y
        /// `after.png`— donde el nodo de <paramref name="nodePiece"/> pasa a mostrar el `Sprite`
        /// de <paramref name="spritePiece"/> (todo en memoria). El bbox que el gate exige es el de
        /// <paramref name="bboxPiece"/>: si el cambio cae dentro, PASS; si cae fuera, DIFF-OUTSIDE.
        /// `nodePiece == null` no cambia nada: el diff debe salir vacio (DIFF-EMPTY).
        /// </summary>
        public static int CaptureSwapDrill(string bboxPiece, string nodePiece, string spritePiece,
            string outDir)
        {
            if (string.IsNullOrEmpty(bboxPiece))
            {
                Log("swap drill: falta la pieza del bbox");
                return 2;
            }
            if (string.IsNullOrEmpty(outDir))
            {
                Log("swap drill: falta el directorio de salida");
                return 2;
            }
            if (IsInsideCaptureDir(outDir))
            {
                Log("swap drill: la salida no puede caer en " + CaptureDir + " (solo Temp/)");
                return 2;
            }

            string root = ArtContractManifest.ProjectRoot();
            List<MapRow> rows;
            try
            {
                rows = ReadMap();
            }
            catch (Exception error)
            {
                Log("no se pudo leer el mapa: " + error.Message);
                return 2;
            }
            MapRow bbox = FindRow(rows, bboxPiece);
            if (bbox == null || !bbox.IsCableado || !bbox.HasCapture)
            {
                Log("swap drill: " + bboxPiece + " no es una fila cableada con captura");
                return 2;
            }
            MapRow swap = nodePiece == null ? null : FindRow(rows, nodePiece);
            if (nodePiece != null && swap == null)
            {
                Log("swap drill: " + nodePiece + " no esta en el mapa");
                return 2;
            }
            MapRow source = null;
            if (swap != null)
            {
                source = FindRow(rows, spritePiece);
                if (source == null)
                {
                    Log("swap drill: falta la pieza del Sprite a asignar (" + spritePiece + ")");
                    return 2;
                }
            }

            int dirty = DirtySceneCode();
            if (dirty != 0)
            {
                return dirty;
            }

            string origin = SceneManager.GetActiveScene().path;
            string scenePath = HostSceneFor(bbox.Asset);
            if (string.IsNullOrEmpty(scenePath))
            {
                Log("swap drill: no se encontro escena para " + bbox.Asset);
                return 2;
            }

            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var snapshot = Snapshot(scene);
                string problem;
                Transform bboxNode = ResolveNode(scene, bbox, out problem);
                if (bboxNode == null)
                {
                    Log("swap drill: " + bbox.Pieza + ": " + problem);
                    return 1;
                }
                Transform swapNode = swap == null || swap == bbox
                    ? bboxNode
                    : ResolveNode(scene, swap, out problem);
                if (swapNode == null)
                {
                    Log("swap drill: " + swap.Pieza + ": " + problem);
                    return 1;
                }

                Prepare(bbox, bboxNode, true);
                if (swap != null && swapNode != bboxNode)
                {
                    Prepare(swap, swapNode, true);
                }

                string absoluteOut = System.IO.Path.Combine(root, outDir);
                Directory.CreateDirectory(absoluteOut);

                Texture2D before;
                int bx, by, bw, bh;
                if (!Render(bbox, bboxNode, out before, out bx, out by, out bw, out bh, out problem))
                {
                    Log("swap drill: " + bbox.Pieza + ": " + problem);
                    return 1;
                }

                Texture2D after;
                if (swap != null)
                {
                    // Un solo cambio: el `Sprite` de `source` en el nodo de `swap`. Cuando el nodo es
                    // el de `bbox` (normal/pressed de la misma pieza) la diferencia es la que produce
                    // el estado pressed; cuando es otro nodo, la diferencia cae donde ese nodo vive.
                    if (!AssignSwapSprite(source, swapNode, out problem))
                    {
                        Log("swap drill: " + source.Pieza + ": " + problem);
                        return 1;
                    }
                    if (!Render(bbox, bboxNode, out after, out bx, out by, out bw, out bh, out problem))
                    {
                        Log("swap drill: " + bbox.Pieza + ": " + problem);
                        return 1;
                    }
                }
                else
                {
                    after = Copy(before);
                }

                string beforePath = System.IO.Path.Combine(absoluteOut, "before.png");
                string afterPath = System.IO.Path.Combine(absoluteOut, "after.png");
                File.WriteAllBytes(beforePath, before.EncodeToPNG());
                File.WriteAllBytes(afterPath, after.EncodeToPNG());

                Log("swap drill · " + bbox.Pieza + " bbox " + bx + "," + by + "," + bw + "," + bh +
                    " · before " + Show(beforePath) + " · after " + Show(afterPath));
                Restore(snapshot);
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(after);
                return 0;
            }
            catch (Exception error)
            {
                Log("swap drill: " + error.GetType().Name + ": " + error.Message);
                return 5;
            }
            finally
            {
                Reload(origin);
            }
        }

        // -------------------------------------------------------------------- run

        static int Run()
        {
            List<MapRow> rows;
            try
            {
                rows = ReadMap();
            }
            catch (Exception error)
            {
                Log("no se pudo leer el mapa: " + error.Message);
                return 2;
            }
            var todo = new List<MapRow>();
            var declared = new List<MapRow>();
            foreach (MapRow row in rows)
            {
                if (!row.IsCableado)
                {
                    continue;
                }
                if (!row.HasCapture)
                {
                    declared.Add(row);
                    continue;
                }
                todo.Add(row);
            }

            Log("mapa " + MapPath + ": " + rows.Count + " filas · " + todo.Count +
                " cableadas con captura · " + declared.Count + " cableadas sin captura (declaradas)");
            foreach (MapRow row in declared)
            {
                Log("DECLARADA " + row.Pieza + " (" + row.Estado + ", asset " + row.Asset +
                    "): sin captura declarada en el mapa; el rig no inventa un bitmap");
            }

            int dirty = DirtySceneCode();
            if (dirty != 0)
            {
                return dirty;
            }

            string origin = SceneManager.GetActiveScene().path;
            List<Group> groups;
            try
            {
                groups = GroupByScene(todo);
            }
            catch (Exception error)
            {
                Log("no se pudo agrupar el mapa por escena: " + error.Message);
                return 2;
            }
            var shots = new List<Shot>();
            int failures = 0;

            try
            {
                foreach (Group group in groups)
                {
                    Log("escena " + group.ScenePath + ": " + group.Rows.Count + " capturas");
                    Scene scene;
                    try
                    {
                        scene = EditorSceneManager.OpenScene(group.ScenePath, OpenSceneMode.Single);
                    }
                    catch (Exception error)
                    {
                        Log("no se pudo abrir " + group.ScenePath + ": " + error.Message);
                        failures += group.Rows.Count;
                        continue;
                    }

                    var snapshot = Snapshot(scene);
                    foreach (MapRow row in group.Rows)
                    {
                        Shot shot = CaptureRow(scene, row, snapshot);
                        if (shot == null)
                        {
                            failures++;
                        }
                        else
                        {
                            shots.Add(shot);
                        }
                        Restore(snapshot);
                    }
                }
            }
            finally
            {
                Reload(origin);
            }

            // Evidencia: se reescribe entera para que el archivo sea reproducible por el rig.
            WriteEvidence(shots, declared);

            foreach (Shot shot in shots)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "CAPTURA {0} · {1}x{2} · bbox {3},{4},{5},{6} · {7} colores · sha-no-usado",
                    shot.Row.Pieza, shot.W, shot.H, shot.Bx, shot.By, shot.Bw, shot.Bh, shot.Colours));
            }
            Log("RESULTADO " + (failures == 0 ? "PASS" : "FAIL") + " (" + shots.Count + " capturas, " +
                failures + " fallos) · evidencia " + EvidencePath);
            return failures == 0 ? 0 : 1;
        }

        static Shot CaptureRow(Scene scene, MapRow row, List<KeyValuePair<GameObject, bool>> snapshot)
        {
            if (string.IsNullOrEmpty(row.Captura) || row.Captura.IndexOf('/') >= 0 ||
                row.Captura.IndexOf('\\') >= 0)
            {
                Log("FALLA " + row.Pieza + ": `captura` vacia o con separadores de directorio");
                return null;
            }
            string problem;
            Transform node = ResolveNode(scene, row, out problem);
            if (node == null)
            {
                Log("FALLA " + row.Pieza + ": " + problem);
                return null;
            }

            Texture2D frame;
            int bx, by, bw, bh;
            if (!Render(row, node, out frame, out bx, out by, out bw, out bh, out problem))
            {
                Log("FALLA " + row.Pieza + ": " + problem);
                return null;
            }

            Texture2D crop = null;
            try
            {
                // El recorte baja en coordenadas de textura (origen abajo): el bbox declarado va en
                // coordenadas de PNG (origen arriba), asi que la banda arranca en Height-(by+bh).
                int bandTop = Height - (by + bh);
                if (bandTop < 0 || bw <= 0 || bh <= 0 || bx + bw > Width || bandTop + bh > Height)
                {
                    Log("FALLA " + row.Pieza + ": OFF-CANVAS bbox " + bx + "," + by + "," + bw + "," + bh);
                    return null;
                }
                crop = new Texture2D(bw, bh, TextureFormat.RGBA32, false);
                crop.SetPixels(frame.GetPixels(bx, bandTop, bw, bh));
                crop.Apply();

                int colours = CountColours(crop);
                var shot = new Shot
                {
                    Row = row, W = bw, H = bh, Bx = bx, By = by, Bw = bw, Bh = bh, Colours = colours,
                };

                string absolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(),
                    CaptureDir, row.Captura);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute));
                File.WriteAllBytes(absolute, crop.EncodeToPNG());

                if (colours < 2)
                {
                    // DRL-3: un PNG de un solo color no es una captura. Se escribe igual (es la
                    // prueba), pero la corrida falla con el nombre y el numero medido.
                    Log("FALLA " + row.Pieza + ": PNG uniforme (" + colours + " color): no es captura");
                    return null;
                }
                return shot;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                if (crop != null)
                {
                    UnityEngine.Object.DestroyImmediate(crop);
                }
            }
        }

        /// <summary>
        /// Aplica el estado que la fila declara y renderiza el canvas. Devuelve el marco completo;
        /// el bbox del nodo se calcula con la misma proyeccion con la que se recorta.
        /// </summary>
        static bool Render(MapRow row, Transform node, out Texture2D frame, out int bx, out int by,
            out int bw, out int bh, out string problem)
        {
            frame = null;
            bx = by = bw = bh = 0;
            problem = null;

            Prepare(row, node, true);

            Canvas canvas = node.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                problem = "el nodo no esta bajo un Canvas";
                return false;
            }
            Camera camera = canvas.worldCamera;
            if (camera == null)
            {
                problem = "el Canvas no tiene worldCamera (ScreenSpaceCamera): no hay camara propia";
                return false;
            }

            RectTransform target = CropTarget(row, node, out problem);
            if (target == null)
            {
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            RenderMode previousMode = canvas.renderMode;
            RenderTexture buffer = null;
            try
            {
                buffer = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = buffer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;

                // La proyeccion se mide con la RenderTexture ya enlazada: la camara mide 900x1900
                // igual que el marco, y el bbox no depende del tamano del Game view.
                if (!TryBounds(target, camera, out bx, out by, out bw, out bh))
                {
                    problem = "el RectTransform del nodo no proyecta un area dentro de " + FrameSize;
                    return false;
                }
                HideOccluders(node, camera, bx, by, bw, bh);

                // El layout no corre solo en el editor: sin el rebuild, un grupo con posiciones
                // serializadas viejas dibuja fuera de lugar (y el recorte mediria otra cosa).
                var canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = buffer;
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                frame = texture;
                return true;
            }
            catch (Exception error)
            {
                problem = error.GetType().Name + ": " + error.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                if (buffer != null)
                {
                    RenderTexture.ReleaseTemporary(buffer);
                }
            }
        }

        // ------------------------------------------------------------- estado forzado

        static void Prepare(MapRow row, Transform node, bool activate)
        {
            if (activate)
            {
                for (Transform cursor = node; cursor != null; cursor = cursor.parent)
                {
                    if (!cursor.gameObject.activeSelf)
                    {
                        cursor.gameObject.SetActive(true);
                    }
                }
            }

            OpenPanels(node);
            ApplyTint(row, node);
            ApplyState(row, node);
        }

        /// <summary>
        /// `PanelFloatingUiRules.Show()` mueve el panel a `localPosition = 0` con un tween de 0.5 s;
        /// en el editor ese tween no corre, asi que se escribe directo el destino. Sin play mode y
        /// sin guardar la escena: la posicion abierta del panel de `Configuration` NO esta
        /// confirmada por el dueno de la UI (riesgo declarado en el design).
        /// </summary>
        static void OpenPanels(Transform node)
        {
            for (Transform cursor = node; cursor != null; cursor = cursor.parent)
            {
                Component[] components = cursor.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null ||
                        !string.Equals(component.GetType().Name, "PanelFloatingUiRules",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    FieldInfo field = component.GetType().GetField("panel",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    GameObject panel = field == null ? null : field.GetValue(component) as GameObject;
                    if (panel != null)
                    {
                        panel.transform.localPosition = Vector3.zero;
                    }
                }
            }
        }

        /// <summary>APW-3: el `m_Color` vivo que la fila exige (la misma regla que el gate).</summary>
        static void ApplyTint(MapRow row, Transform node)
        {
            Color wanted;
            if (!TryExpectedTint(row, out wanted))
            {
                return;
            }
            Image image = CropTargetImage(row, node);
            if (image != null)
            {
                image.color = wanted;
            }
        }

        static void ApplyState(MapRow row, Transform node)
        {
            if (string.Equals(row.Componente, "TutorialStepDots", StringComparison.Ordinal))
            {
                var dots = node.GetComponent<Presentation.Tutorial.TutorialStepDots>();
                if (dots != null)
                {
                    dots.SetCurrent(1);
                }
                return;
            }
            if (!string.Equals(row.SlotTipo, "selectable_state", StringComparison.Ordinal))
            {
                return;
            }
            Selectable selectable = node.GetComponent<Selectable>();
            if (selectable == null)
            {
                return;
            }
            MethodInfo method = typeof(Selectable).GetMethod("DoStateTransition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return;
            }
            // `Selectable.SelectionState` es `protected`: se resuelve por reflexion desde la firma
            // del propio metodo, para no copiar el enum ni depender de su orden.
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return;
            }
            object state = Enum.Parse(parameters[0].ParameterType, StateName(row.Campo));
            // Es exactamente lo que hace el input: el sprite del estado que la fila nombra.
            method.Invoke(selectable, new object[] { state, true });
        }

        static string StateName(string campo)
        {
            switch (campo ?? string.Empty)
            {
                case "m_SpriteState.m_PressedSprite":
                    return "Pressed";
                case "m_SpriteState.m_DisabledSprite":
                    return "Disabled";
                case "m_SpriteState.m_HighlightedSprite":
                    return "Highlighted";
                case "m_SpriteState.m_SelectedSprite":
                    return "Selected";
                default:
                    return "Normal";
            }
        }

        /// <summary>
        /// DRL-4: asigna en memoria el `Sprite` de la pieza `swap` al `Image` de su nodo. No pisa
        /// bytes ni escribe nada: el efecto sobre el nodo es el mismo que un swap de bytes cuando
        /// las dos piezas miden lo mismo, y no hay riesgo de dejar un asset a medio cambiar.
        /// </summary>
        static bool AssignSwapSprite(MapRow swap, Transform node, out string problem)
        {
            problem = null;
            Image image = CropTargetImage(swap, node);
            if (image == null)
            {
                problem = "el nodo no tiene Image sobre la que asignar el Sprite";
                return false;
            }
            string spritePath = "Assets/Sprites/" + swap.Pieza;
            Sprite sprite = null;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite)
                {
                    sprite = assets[i] as Sprite;
                    break;
                }
            }
            if (sprite == null)
            {
                problem = "no se pudo cargar el Sprite de " + spritePath +
                    " (las piezas de una REPLACE viven en su `origen`, no en Assets/Sprites)";
                return false;
            }
            image.sprite = sprite;
            image.overrideSprite = null;
            return true;
        }

        // ------------------------------------------------------------- recorte/bbox

        /// <summary>El RectTransform que se recorta: el nodo, o el dot que nombra el `campo`.</summary>
        static RectTransform CropTarget(MapRow row, Transform node, out string problem)
        {
            problem = null;
            if (string.Equals(row.Componente, "TutorialStepDots", StringComparison.Ordinal))
            {
                Image dot = DotImage(row, node);
                if (dot == null)
                {
                    problem = "el `TutorialStepDots` no tiene el dot de `" + row.Campo + "`";
                    return null;
                }
                return dot.transform as RectTransform;
            }
            var rect = node as RectTransform;
            if (rect == null)
            {
                problem = "el nodo no es un RectTransform: no hay nada que proyectar (un slot de "
                    + "mundo no lo captura este rig)";
                return null;
            }
            return rect;
        }

        static Image DotImage(MapRow row, Transform node)
        {
            var dots = node.GetComponent<Presentation.Tutorial.TutorialStepDots>();
            if (dots == null)
            {
                return null;
            }
            FieldInfo field = typeof(Presentation.Tutorial.TutorialStepDots).GetField("dots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var images = field == null ? null : field.GetValue(dots) as Image[];
            if (images == null)
            {
                return null;
            }
            int index;
            switch (row.Campo ?? string.Empty)
            {
                case "doneSprite":
                    index = 0;
                    break;
                case "activeSprite":
                    index = 1;
                    break;
                case "normalSprite":
                    index = 2;
                    break;
                default:
                    return null;
            }
            if (index >= images.Length)
            {
                return null;
            }
            // El dot 0 esta `done`, el 1 `active` y el 2 `normal` tras `SetCurrent(1)`.
            return images[index];
        }

        static Image CropTargetImage(MapRow row, Transform node)
        {
            if (string.Equals(row.Componente, "TutorialStepDots", StringComparison.Ordinal))
            {
                return DotImage(row, node);
            }
            return node.GetComponent<Image>();
        }

        /// <summary>Proyeccion del rect a pixeles del marco (x,y = origen arriba-izquierda).</summary>
        static bool TryBounds(RectTransform rect, Camera camera, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            // `Floor` en el minimo y `Ceil` en el maximo: redondear hacia afuera nunca recorta un
            // borde del nodo (un recorte de 1 px mentiria sobre el tamano del slot).
            int left = Mathf.Clamp(Mathf.FloorToInt(min.x), 0, Width);
            int right = Mathf.Clamp(Mathf.CeilToInt(max.x), 0, Width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(min.y), 0, Height);
            int top = Mathf.Clamp(Mathf.CeilToInt(max.y), 0, Height);
            w = right - left;
            h = top - bottom;
            if (w <= 0 || h <= 0)
            {
                return false;
            }
            x = left;
            y = Height - top;
            return true;
        }

        /// <summary>
        /// Apaga los hermanos POSTERIORES que cubren por completo al nodo. En una escena de UI los
        /// paneles de nivel canvas estan todos `active` en el asset y el codigo prende uno por vez:
        /// sin esto, el recorte de un boton de `Configuration` devolveria el panel que lo tapa. Solo
        /// se apaga lo que se DIBUJA ENCIMA (indice mayor) y TAPA ENTERO: el fondo se respeta, y un
        /// vecino parcial (el boton de al lado) queda en el recorte.
        /// </summary>
        static void HideOccluders(Transform node, Camera camera, int bx, int by, int bw, int bh)
        {
            for (Transform level = node; level != null; level = level.parent)
            {
                Transform parent = level.parent;
                if (parent == null)
                {
                    break;
                }
                int index = level.GetSiblingIndex();
                for (int i = parent.childCount - 1; i > index; i--)
                {
                    Transform sibling = parent.GetChild(i);
                    if (!sibling.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (!Covers(sibling, camera, bx, by, bw, bh))
                    {
                        continue;
                    }
                    sibling.gameObject.SetActive(false);
                }
            }
        }

        static bool Covers(Transform sibling, Camera camera, int bx, int by, int bw, int bh)
        {
            var rect = sibling as RectTransform;
            if (rect == null)
            {
                return false;
            }
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }
            // El bbox viene en coordenadas de PNG (origen arriba); el screen point va al reves.
            float nodeLeft = bx;
            float nodeRight = bx + bw;
            float nodeBottom = Height - (by + bh);
            float nodeTop = Height - by;
            return minX <= nodeLeft + 0.5f && maxX >= nodeRight - 0.5f &&
                   minY <= nodeBottom + 0.5f && maxY >= nodeTop - 0.5f;
        }

        static int CountColours(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            var seen = new HashSet<Color32>();
            for (int i = 0; i < pixels.Length; i++)
            {
                seen.Add(pixels[i]);
            }
            return seen.Count;
        }

        static Texture2D Copy(Texture2D source)
        {
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.SetPixels(source.GetPixels());
            copy.Apply();
            return copy;
        }

        // ------------------------------------------------------------------- mapa

        static List<MapRow> ReadMap()
        {
            string absolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), MapPath);
            if (!File.Exists(absolute))
            {
                throw new InvalidOperationException("falta el mapa: " + MapPath);
            }
            string[] lines = File.ReadAllLines(absolute, Encoding.UTF8);
            int start = -1;
            int blocks = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i].Trim(), "```csv", StringComparison.Ordinal))
                {
                    blocks++;
                    if (start < 0)
                    {
                        start = i + 1;
                    }
                }
            }
            if (blocks != 1)
            {
                throw new InvalidOperationException(MapPath + " tiene " + blocks + " bloques ```csv: "
                    + "el mapa tiene que ser uno");
            }
            var body = new List<string>();
            for (int i = start; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("```", StringComparison.Ordinal))
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
                throw new InvalidOperationException("el bloque ```csv de " + MapPath + " esta vacio");
            }

            string[] header = ArtContractManifest.SplitCsv(body[0]);
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                index[header[i]] = i;
            }
            string[] required = { "pieza", "slot_tipo", "asset", "nodo", "componente", "campo",
                "estado", "tint_original", "captura" };
            for (int i = 0; i < required.Length; i++)
            {
                if (!index.ContainsKey(required[i]))
                {
                    throw new InvalidOperationException("el mapa no tiene la columna `"
                        + required[i] + "`");
                }
            }

            var rows = new List<MapRow>(body.Count - 1);
            for (int i = 1; i < body.Count; i++)
            {
                string[] cells = ArtContractManifest.SplitCsv(body[i]);
                if (cells.Length != header.Length)
                {
                    throw new InvalidOperationException("fila " + (i + 1) + " del mapa con "
                        + cells.Length + " columnas (se esperan " + header.Length + ")");
                }
                rows.Add(new MapRow
                {
                    Pieza = Cell(cells, index["pieza"]),
                    SlotTipo = Cell(cells, index["slot_tipo"]),
                    Asset = ArtContractManifest.NormalizePath(Cell(cells, index["asset"])),
                    Nodo = ArtContractManifest.NormalizePath(Cell(cells, index["nodo"])),
                    Componente = Cell(cells, index["componente"]),
                    Campo = Cell(cells, index["campo"]),
                    Estado = Cell(cells, index["estado"]),
                    Tint = Cell(cells, index["tint_original"]),
                    Captura = Cell(cells, index["captura"]),
                });
            }
            return rows;
        }

        static string Cell(string[] cells, int index)
        {
            return index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        static MapRow FindRow(List<MapRow> rows, string pieza)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Pieza, pieza, StringComparison.Ordinal))
                {
                    return rows[i];
                }
            }
            return null;
        }

        // -------------------------------------------------------------------- nodo

        static Transform ResolveNode(Scene scene, MapRow row, out string problem)
        {
            problem = null;
            string path = row.Nodo;
            if (string.IsNullOrEmpty(path) || path == "-")
            {
                problem = "la fila no declara `nodo`";
                return null;
            }
            string[] parts = path.Split('/');
            Transform current;
            int first;

            if (row.Asset.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
            {
                GameObject instance = FindPrefabInstance(scene, row.Asset);
                if (instance == null)
                {
                    problem = "no hay instancia de " + row.Asset + " en " + scene.path
                        + " (el rig no instancia prefabs: cambiaria el host)";
                    return null;
                }
                if (!string.Equals(instance.name, parts[0], StringComparison.Ordinal))
                {
                    problem = "la instancia se llama '" + instance.name + "' y el nodo arranca en '"
                        + parts[0] + "'";
                    return null;
                }
                current = instance.transform;
                first = 1;
            }
            else
            {
                current = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (string.Equals(roots[i].name, parts[0], StringComparison.Ordinal))
                    {
                        current = roots[i].transform;
                        break;
                    }
                }
                if (current == null)
                {
                    problem = "'" + parts[0] + "' no es un root de " + scene.path;
                    return null;
                }
                first = 1;
            }

            for (int i = first; i < parts.Length; i++)
            {
                Transform next = current.Find(parts[i]);
                if (next == null)
                {
                    problem = "'" + path + "' no resuelve: falta '" + parts[i] + "' bajo '"
                        + current.name + "'";
                    return null;
                }
                current = next;
            }
            return current;
        }

        static GameObject FindPrefabInstance(Scene scene, string prefabPath)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(all[j].gameObject);
                    if (source == null)
                    {
                        continue;
                    }
                    string asset = AssetDatabase.GetAssetPath(source);
                    if (string.Equals(ArtContractManifest.NormalizePath(asset), prefabPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return all[j].gameObject;
                    }
                }
            }
            return null;
        }

        // ----------------------------------------------------------------- escenas

        static List<Group> GroupByScene(List<MapRow> rows)
        {
            var groups = new List<Group>();
            for (int i = 0; i < rows.Count; i++)
            {
                string scene = HostSceneFor(rows[i].Asset);
                if (string.IsNullOrEmpty(scene))
                {
                    throw new InvalidOperationException("no se encontro escena para "
                        + rows[i].Asset + " (" + rows[i].Pieza + ")");
                }
                Group group = null;
                for (int j = 0; j < groups.Count; j++)
                {
                    if (string.Equals(groups[j].ScenePath, scene, StringComparison.Ordinal))
                    {
                        group = groups[j];
                        break;
                    }
                }
                if (group == null)
                {
                    group = new Group { ScenePath = scene };
                    groups.Add(group);
                }
                group.Rows.Add(rows[i]);
            }
            return groups;
        }

        /// <summary>
        /// La escena que hospeda la fila. Un `.unity` es su propio host; un `.prefab` se captura
        /// sobre la instancia que el juego tiene de el, en la PRIMERA escena (ordenada) que lo
        /// declara como dependencia. Determinista: no depende de cual este abierta.
        /// </summary>
        static string HostSceneFor(string asset)
        {
            if (string.IsNullOrEmpty(asset) || asset == "-")
            {
                throw new InvalidOperationException("la fila no declara `asset`");
            }
            if (asset.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
            if (!asset.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("el rig solo captura escenas y prefabs: " + asset);
            }
            string root = ArtContractManifest.ProjectRoot();
            string[] scenes = Directory.GetFiles(System.IO.Path.Combine(root, "Assets"), "*.unity",
                SearchOption.AllDirectories);
            Array.Sort(scenes, StringComparer.Ordinal);
            for (int i = 0; i < scenes.Length; i++)
            {
                string projectPath = ArtContractManifest.ToProjectPath(scenes[i]);
                string[] dependencies = AssetDatabase.GetDependencies(projectPath, true);
                for (int j = 0; j < dependencies.Length; j++)
                {
                    if (string.Equals(ArtContractManifest.NormalizePath(dependencies[j]), asset,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return projectPath;
                    }
                }
            }
            return null;
        }

        static int DirtySceneCode()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                {
                    Log("hay cambios sin guardar en " + scene.path +
                        ": abrir otra escena los tiraria. Guardar o descartar y reintentar.");
                    return 3;
                }
            }
            return 0;
        }

        /// <summary>Reabre la escena de origen desde disco: descarta el estado forzado sin guardar.</summary>
        static void Reload(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception error)
            {
                Log("no se pudo volver a " + scenePath + ": " + error.Message);
            }
        }

        static List<KeyValuePair<GameObject, bool>> Snapshot(Scene scene)
        {
            var snapshot = new List<KeyValuePair<GameObject, bool>>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    snapshot.Add(new KeyValuePair<GameObject, bool>(all[j].gameObject,
                        all[j].gameObject.activeSelf));
                }
            }
            return snapshot;
        }

        static void Restore(List<KeyValuePair<GameObject, bool>> snapshot)
        {
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                GameObject go = snapshot[i].Key;
                if (go != null && go.activeSelf != snapshot[i].Value)
                {
                    go.SetActive(snapshot[i].Value);
                }
            }
        }

        // ------------------------------------------------------------------- color

        static bool TryExpectedTint(MapRow row, out Color color)
        {
            color = Color.white;
            string text = row.Tint;
            if (string.IsNullOrEmpty(text) || text == "-")
            {
                return false;
            }
            string[] cells = text.Split('/');
            if (cells.Length != 4)
            {
                return false;
            }
            var values = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(cells[i].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out values[i]))
                {
                    return false;
                }
            }
            var rgb = new Color(values[0], values[1], values[2], values[3]);
            if (string.Equals(row.SlotTipo, "image+tinte_design", StringComparison.Ordinal))
            {
                color = rgb;
                return true;
            }
            if (Luminance(values[0], values[1], values[2]) < 0.25f)
            {
                color = new Color(1f, 1f, 1f, values[3]);
                return true;
            }
            color = rgb;
            return true;
        }

        static float Luminance(float red, float green, float blue)
        {
            float[] linear = new float[3];
            float[] channels = { red, green, blue };
            for (int i = 0; i < 3; i++)
            {
                float srgb = Mathf.Round(Mathf.Clamp01(channels[i]) * 255f) / 255f;
                linear[i] = srgb <= 0.04045f ? srgb / 12.92f
                    : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
            }
            return 0.2126f * linear[0] + 0.7152f * linear[1] + 0.0722f * linear[2];
        }

        // ---------------------------------------------------------------- evidencia

        static void WriteEvidence(List<Shot> shots, List<MapRow> declared)
        {
            var text = new StringBuilder();
            text.AppendLine("# Capturas v1 — rig `ArtCaptureRig` (DRL-3 / DRL-4)");
            text.AppendLine();
            text.AppendLine("Generado por `Assets/Editor/ArtContract/ArtCaptureRig.cs` (" +
                "`BatchCapture`, entrypoint unico). **No editar a mano**: re-ejecutar el rig " +
                "reescribe este archivo y los PNG que declara.");
            text.AppendLine();
            text.AppendLine("Metodo: camara del canvas (`Canvas.worldCamera`, ScreenSpaceCamera) -> " +
                "`RenderTexture` " + FrameSize + " (la resolucion de referencia de todos los " +
                "`CanvasScaler`) -> `ReadPixels` -> recorte por la proyeccion del `RectTransform` " +
                "del nodo. **Nada de `ScreenCapture`**: la resolucion es la del contrato, no la " +
                "del Game view.");
            text.AppendLine();
            text.AppendLine("Forzado por fila, declarado (no es maquillaje):");
            text.AppendLine();
            text.AppendLine("- cadena de ancestros activa: un nodo inactivo no dibuja.");
            text.AppendLine("- `m_Color` = el que APW-3 exige para la fila (la misma regla que mide " +
                "`check_placeholder_wiring.py`), para que la captura no dependa de un estado previo.");
            text.AppendLine("- `selectable_state`: `Selectable.DoStateTransition` (por reflexion) al " +
                "estado de su `campo`; es exactamente lo que hace el input, y con `m_Transition: 2` " +
                "no hay tinte de color (el costo de APW-4b se ve en la captura).");
            text.AppendLine("- `serialized_sprite_field` sobre `TutorialStepDots`: " +
                "`SetCurrent(1)` (dot 0 `done`, dot 1 `active`, dot 2 `normal`) y el recorte va al " +
                "dot que nombra el `campo`, no a la tira completa.");
            text.AppendLine("- panel con `PanelFloatingUiRules` (`Configuration` en `MainMenu`): se " +
                "escribe el destino de su tween `Show()`, `localPosition = 0`. Sin play mode; la " +
                "posicion abierta del panel **no** esta confirmada por el dueno de la UI.");
            text.AppendLine();
            text.AppendLine("Sin play mode y sin escribir `Assets/`: cada escena se abre desde disco " +
                "y se recarga para descartar el estado forzado. Ningun `.meta` se reescribe (15 " +
                "piezas en Single serializan un bloque de tamano lienzo: moverles un px las " +
                "volveria `RECT-STALE`).");
            text.AppendLine();
            text.AppendLine("## Columnas");
            text.AppendLine();
            text.AppendLine("| Columna | Significado |");
            text.AppendLine("|---|---|");
            text.AppendLine("| `pieza` | nombre verbatim; la captura es `<pieza>` en " + CaptureDir +
                " |");
            text.AppendLine("| `px` | tamano del recorte (el nodo, no el marco) |");
            text.AppendLine("| `marco` | tamano del marco del que se recorta (constante: " +
                FrameSize + ") |");
            text.AppendLine("| `bbox` | `x,y,w,h` del nodo proyectado al marco, en pixeles del PNG " +
                "(origen arriba-izquierda): la region que un swap puede tocar (DRL-4) |");
            text.AppendLine("| `colores` | colores distintos del PNG: una captura uniforme no es " +
                "una captura (DRL-3) |");
            text.AppendLine();
            text.AppendLine("El marco completo no se versiona: `CaptureSwapDrill` emite `before.png` " +
                "y `after.png` del mismo estado en `" + SwapDrillDir + "` (ignorado) y " +
                "`tools/art/check_capture_diff.py` compara `changed ⊆ bbox` y `changed >= 1`. " +
                "No hay hashes de captura: dependen de Unity y del frame.");
            text.AppendLine();
            if (declared.Count > 0)
            {
                text.AppendLine("## Filas `cableado` sin captura (declaradas)");
                text.AppendLine();
                text.AppendLine("`ball_state_frozen_01.png` es la unica: su slot es un `Sprite` " +
                    "serializado en un `MonoBehaviour` (`FreezerMono.freezeSprite`) que el juego " +
                    "asigna a un `SpriteRenderer` de mundo en runtime. No tiene `RectTransform` ni " +
                    "Canvas, asi que este rig no lo proyecta, y no se inventa un bitmap. La lista " +
                    "cerrada vive en `tools/art/art_ledger.py` (`CAPTURE_EXCEPTIONS`) y los dos " +
                    "gates la imprimen por nombre.");
                text.AppendLine();
                foreach (MapRow row in declared)
                {
                    text.AppendLine("- `" + row.Pieza + "` (" + row.Estado + " · " + row.Asset +
                        " · " + row.Nodo + "." + row.Campo + ")");
                }
                text.AppendLine();
            }
            text.AppendLine("```csv");
            text.AppendLine("pieza,px,marco,bbox,colores");
            for (int i = 0; i < shots.Count; i++)
            {
                Shot shot = shots[i];
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1}x{2},{3},\"{4},{5},{6},{7}\",{8}",
                    shot.Row.Pieza, shot.W, shot.H, FrameSize, shot.Bx, shot.By, shot.Bw, shot.Bh,
                    shot.Colours));
            }
            text.AppendLine("```");
            text.AppendLine();

            string absolute = System.IO.Path.Combine(ArtContractManifest.ProjectRoot(), EvidencePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n", "\n")));
        }

        // ------------------------------------------------------------------ helpers

        static bool IsInsideCaptureDir(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.Replace('\\', '/').StartsWith(CaptureDir, StringComparison.OrdinalIgnoreCase);
        }

        static string Show(string absolute)
        {
            return ArtContractManifest.ToProjectPath(absolute);
        }

        static void Log(string message)
        {
            Debug.Log("[ArtCaptureRig] " + message);
        }
    }
}
#endif
