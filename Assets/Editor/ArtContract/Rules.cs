// Rules: las reglas de escena y prefab del contrato de UI (UCC-*, ULN-1 y el borde 9-slice).
//
// Por que existe este archivo aparte de tools/art/audit_art_manifest.py: el auditor Python lee
// bytes, .meta y YAML y por eso cumple ASC-8 sin abrir el Editor. Hay cuatro cosas que no puede
// ver sin Unity y que son justamente las que rompen la pantalla:
//   1. el render mode real de un canvas (m_RenderMode + m_Camera + plano), que en un canvas
//      anidado de un prefab hereda del raiz y en YAML miente;
//   2. el CanvasScaler efectivo (referencia + match + modo);
//   3. el borde del sprite que un Image dibuja en Sliced/Tiled, resuelto por GUID;
//   4. el RectTransform como par (anchorMin, anchorMax, pivot, sizeDelta) y su caja final
//      dentro del area segura declarada.
//
// Reglas duras:
//   * Solo lectura. Ninguna funcion de aca toca un asset, un rect o un componente.
//   * Los valores esperados salen del spec o del design, nunca de una corazonada:
//       UCC-1 -> 900x1900 (spec, "Referencia de resolucion unica")
//       UCC-2 -> matchWidthOrHeight 0 (spec, "Politica unica")
//       UCC-3 -> 12 canvas de escena en ScreenSpaceCamera con la camara del LetterBoxer y los
//                3 de prefab en Overlay con root Page 9:19 (design D4 / §4)
//       UCC-5 -> margen fijo top 60 u / bottom 60 u / lados 0 (design D5 / §4)
//       ULN-1 -> las cuatro filas de la tabla de design §5
//       ASC-3 -> el borde declarado en sec.9 del manifiesto; si la pieza no esta en el
//                manifiesto, la spec solo exige "spriteBorder != 0/0/0/0"
//     Cuando un valor esperado no existe en ninguna de esas fuentes, la regla NO se implementa.
//   * Las excepciones declaradas (prefab Overlay, HUD de Game, hijos de LayoutGroup, contenido
//     de ScrollRect) se cuentan y se listan en el reporte: una omision silenciosa es una mentira.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarkVoid.ArtContract
{
    /// <summary>Un hallazgo con el formato que pide el contrato: requisito, asset, nodo, valor, esperado.</summary>
    public sealed class ArtContractFinding
    {
        public string Requirement;
        public string AssetPath;
        public string HierarchyPath;
        public string Observed;
        public string Expected;
        public string Detail;

        /// <summary>Cuantas veces se repitio el mismo hallazgo (instancias del mismo prefab, etc.).</summary>
        public int Repeats = 1;
    }

    /// <summary>Colector con deduplicacion por (requisito, asset, nodo, valor, detalle).</summary>
    public sealed class ArtContractFindings
    {
        readonly List<ArtContractFinding> items = new List<ArtContractFinding>();
        readonly Dictionary<string, ArtContractFinding> index =
            new Dictionary<string, ArtContractFinding>(StringComparer.Ordinal);

        public IList<ArtContractFinding> Items
        {
            get { return items; }
        }

        public int Count
        {
            get { return items.Count; }
        }

        public void Add(string requirement, string assetPath, string hierarchyPath,
            string observed, string expected, string detail = null)
        {
            string key = requirement + "\u0001" + assetPath + "\u0001" + hierarchyPath + "\u0001" +
                observed + "\u0001" + (detail ?? string.Empty);

            ArtContractFinding existing;
            if (index.TryGetValue(key, out existing))
            {
                existing.Repeats++;
                return;
            }

            var finding = new ArtContractFinding
            {
                Requirement = requirement,
                AssetPath = assetPath,
                HierarchyPath = hierarchyPath,
                Observed = observed,
                Expected = expected,
                Detail = detail,
            };
            index.Add(key, finding);
            items.Add(finding);
        }

        /// <summary>Cantidad de hallazgos por requisito (asc-3, ucc-3, ...).</summary>
        public int CountFor(string requirement)
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Requirement, requirement, StringComparison.Ordinal))
                {
                    total++;
                }
            }
            return total;
        }
    }

    /// <summary>Contadores de omisiones y excepciones declaradas. Se imprimen en el reporte.</summary>
    public sealed class ArtContractCounters
    {
        readonly Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.Ordinal);

        public void Bump(string key)
        {
            int value;
            map.TryGetValue(key, out value);
            map[key] = value + 1;
        }

        public int Get(string key)
        {
            int value;
            return map.TryGetValue(key, out value) ? value : 0;
        }

        public IList<KeyValuePair<string, int>> Items
        {
            get
            {
                var list = new List<KeyValuePair<string, int>>(map);
                list.Sort(delegate(KeyValuePair<string, int> left, KeyValuePair<string, int> right)
                {
                    return string.CompareOrdinal(left.Key, right.Key);
                });
                return list;
            }
        }
    }

    public static class ArtContractRules
    {
        // ----------------------------------------------------------------- valores

        /// <summary>UCC-1: referencia de resolucion unica de los 15 canvas.</summary>
        public const float ReferenceWidth = 900f;
        public const float ReferenceHeight = 1900f;

        /// <summary>UCC-2: politica unica de matchWidthOrHeight.</summary>
        public const float ReferenceMatchWidthOrHeight = 0f;

        /// <summary>UCC-5 / design D5: margen fijo declarado, en unidades del espacio 900x1900.</summary>
        public const float SafeMarginVertical = 60f;

        /// <summary>ULN-1 / design §5: excepcion declarada del HUD de Game (autoría a 39.822 u, escala 0,0221).</summary>
        public const float HudAuthoringWidth = 39822f;
        public const float HudAuthoringScale = 0.0221f;

        /// <summary>UCC-3 / design D4: 900/1900, el aspecto del LetterBoxer.</summary>
        public const float DesignAspectRatio = 0.47368422f;

        /// <summary>Design D4: la camara de un canvas de escena es la que tiene este componente.</summary>
        public const string LetterBoxerTypeName = "LetterBoxer";

        /// <summary>Orden de presentacion y de conteo del reporte.</summary>
        public static readonly string[] RequirementOrder =
        {
            "ASC-3", "UCC-1", "UCC-2", "UCC-3", "UCC-5", "ULN-1", "AUDIT-READONLY",
        };

        const float AnchorEpsilon = 0.01f;
        const float SafeTolerance = 0.5f;
        const float AspectTolerance = 0.01f;

        public static string DescribeRequirement(string requirement)
        {
            switch (requirement)
            {
                case "ASC-3":
                    return "Image Sliced/Tiled con borde declarado (sec.9)";
                case "UCC-1":
                    return "referencia de resolucion unica 900x1900";
                case "UCC-2":
                    return "politica unica de matchWidthOrHeight";
                case "UCC-3":
                    return "area 9:19: canvas de escena por camara, prefab Overlay + Page";
                case "UCC-5":
                    return "margen fijo top 60 u / bottom 60 u / lados 0";
                case "ULN-1":
                    return "anclaje, pivote y sizeDelta";
                case "AUDIT-READONLY":
                    return "el audit no puede ensuciar Assets/";
                default:
                    return string.Empty;
            }
        }

        // ------------------------------------------------------------ canvas (S1)

        /// <summary>UCC-1 + UCC-2 sobre un canvas con CanvasScaler.</summary>
        public static void CheckScaler(Canvas canvas, string assetPath, ArtContractFindings findings)
        {
            string path = HierarchyPath(canvas.transform);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                findings.Add("UCC-1", assetPath, path, "sin CanvasScaler",
                    "CanvasScaler con " + Fmt(ReferenceWidth) + "x" + Fmt(ReferenceHeight) +
                    " y match " + Fmt(ReferenceMatchWidthOrHeight));
                return;
            }

            Vector2 resolution = scaler.referenceResolution;
            if (Mathf.Abs(resolution.x - ReferenceWidth) > AnchorEpsilon ||
                Mathf.Abs(resolution.y - ReferenceHeight) > AnchorEpsilon)
            {
                findings.Add("UCC-1", assetPath, path,
                    "m_ReferenceResolution " + Fmt(resolution.x) + "x" + Fmt(resolution.y),
                    Fmt(ReferenceWidth) + "x" + Fmt(ReferenceHeight) + " (UCC-1)");
            }

            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            {
                // Precondicion del match: en Expand/Shrink el m_MatchWidthOrHeight no participa.
                findings.Add("UCC-2", assetPath, path,
                    "m_ScreenMatchMode " + scaler.screenMatchMode,
                    "MatchWidthOrHeight (el match solo existe en ese modo)");
            }

            if (Mathf.Abs(scaler.matchWidthOrHeight - ReferenceMatchWidthOrHeight) > 0.0001f)
            {
                findings.Add("UCC-2", assetPath, path,
                    "m_MatchWidthOrHeight " + Fmt(scaler.matchWidthOrHeight),
                    Fmt(ReferenceMatchWidthOrHeight) + " (UCC-2)");
            }
        }

        /// <summary>UCC-3 en un canvas que vive en la escena (no en un prefab instanciado).</summary>
        public static void CheckSceneCanvasRenderMode(Canvas canvas, string assetPath,
            ArtContractFindings findings)
        {
            string path = HierarchyPath(canvas.transform);

            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                findings.Add("UCC-3", assetPath, path, "renderMode " + canvas.renderMode,
                    "ScreenSpaceCamera con la camara del LetterBoxer (UCC-3 / design D4)");
                return;
            }

            Camera camera = canvas.worldCamera;
            if (camera == null)
            {
                findings.Add("UCC-3", assetPath, path, "m_Camera NULL",
                    "la Main Camera de la escena, con LetterBoxer (UCC-3)");
                return;
            }

            if (!CameraHasLetterBoxer(camera))
            {
                findings.Add("UCC-3", assetPath, path,
                    "camara '" + camera.gameObject.name + "' sin LetterBoxer",
                    "una camara con LetterBoxer (la unica que recorta a 9:19)");
            }
        }

        /// <summary>
        /// UCC-3, segunda mitad: dentro de una escena todos los canvas tienen que dibujar la misma
        /// area. Mecanicamente eso es la misma camara con el mismo plano.
        /// </summary>
        public static void CheckSceneCanvasUniformity(IList<Canvas> canvases, string assetPath,
            ArtContractFindings findings)
        {
            Canvas reference = null;
            for (int i = 0; i < canvases.Count; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate.renderMode != RenderMode.ScreenSpaceCamera || candidate.worldCamera == null)
                {
                    continue;
                }
                if (reference == null)
                {
                    reference = candidate;
                    continue;
                }
                Camera expectedCamera = reference.worldCamera;
                if (candidate.worldCamera != expectedCamera)
                {
                    findings.Add("UCC-3", assetPath, HierarchyPath(candidate.transform),
                        "camara '" + candidate.worldCamera.gameObject.name + "'",
                        "la misma que '" + HierarchyPath(reference.transform) + "' usa: '" +
                        expectedCamera.gameObject.name + "' (UCC-3: un area por escena)");
                }
                else if (Mathf.Abs(candidate.planeDistance - reference.planeDistance) > 0.01f)
                {
                    findings.Add("UCC-3", assetPath, HierarchyPath(candidate.transform),
                        "m_PlaneDistance " + Fmt(candidate.planeDistance),
                        "el mismo plano que '" + HierarchyPath(reference.transform) + "': " +
                        Fmt(reference.planeDistance) + " (UCC-3: un area por escena)");
                }
            }
        }

        /// <summary>
        /// UCC-3 en un canvas de prefab. El design D4 lo deja en Overlay a proposito: un prefab no
        /// puede serializar la camara de la escena. La contrapartida obligatoria es el root Page
        /// con AspectRatioFitter FitInParent 0,47368 (T-07b).
        /// </summary>
        public static void CheckPrefabCanvas(Canvas canvas, string assetPath,
            ArtContractFindings findings)
        {
            // Canvas anidado: su renderMode reportado sigue al canvas raiz (F-S1c-6). Su contrato
            // es el Page, que se audita desde el canvas raiz del mismo prefab.
            if (HasAncestorCanvas(canvas.transform))
            {
                return;
            }

            string path = HierarchyPath(canvas.transform);
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                findings.Add("UCC-3", assetPath, path, "renderMode " + canvas.renderMode,
                    "ScreenSpaceOverlay + root Page (design D4: un prefab no serializa la camara de la escena)");
            }

            AspectRatioFitter fitter = FindPageFitter(canvas.transform);
            if (fitter == null)
            {
                findings.Add("UCC-3", assetPath, path, "sin root Page con AspectRatioFitter",
                    "hijo 'Page' con AspectRatioFitter FitInParent " + FmtRatio(DesignAspectRatio) + " (design D4)");
                return;
            }

            string pagePath = HierarchyPath(fitter.transform);
            if (fitter.aspectMode != AspectRatioFitter.AspectMode.FitInParent)
            {
                findings.Add("UCC-3", assetPath, pagePath, "m_AspectMode " + fitter.aspectMode,
                    "FitInParent (design D4 / T-07b)");
            }
            if (Mathf.Abs(fitter.aspectRatio - DesignAspectRatio) > 0.001f)
            {
                findings.Add("UCC-3", assetPath, pagePath, "m_AspectRatio " + Fmt(fitter.aspectRatio),
                    FmtRatio(DesignAspectRatio) + " (900/1900, design D4)");
            }
        }

        // ----------------------------------------------------- 9-slice (ASC-3)

        /// <summary>
        /// ASC-3: toda Image en Sliced/Tiled usa un sprite con borde no nulo, y si la pieza esta en
        /// sec.9 el borde tiene que ser el declarado.
        /// </summary>
        public static void CheckImage(Image image, ArtContractManifest manifest, string assetPath,
            ArtContractFindings findings)
        {
            if (image.type != Image.Type.Sliced && image.type != Image.Type.Tiled)
            {
                return;
            }

            string path = HierarchyPath(image.transform);
            string mode = image.type == Image.Type.Sliced ? "Sliced" : "Tiled";
            Sprite sprite = image.sprite;

            if (sprite == null)
            {
                findings.Add("ASC-3", assetPath, path, "type " + mode + " con sprite NULL",
                    "un sprite con spriteBorder != 0/0/0/0 (ASC-3), o type Simple");
                return;
            }

            string spritePath = AssetDatabase.GetAssetPath(sprite.texture);
            Vector4 border = sprite.border;
            if (IsZero(border))
            {
                ArtContractRow row = manifest == null ? null : manifest.FindByOrigen(spritePath);
                string expected = row == null
                    ? "spriteBorder != 0/0/0/0 (ASC-3; la pieza no esta en sec.9)"
                    : "spriteBorder " + row.Border + " (sec.9: " + row.Pieza + ")";
                findings.Add("ASC-3", assetPath, path,
                    "type " + mode + " con '" + spritePath + "' border " + FmtBorder(border),
                    expected, spritePath);
                return;
            }

            if (manifest == null)
            {
                return;
            }

            ArtContractRow declared = manifest.FindByOrigen(spritePath);
            if (declared == null)
            {
                return;
            }

            Vector4 expectedBorder;
            if (!ArtContractManifest.TryParseBorder(declared.Border, out expectedBorder))
            {
                return;
            }
            if (!NearlyEqual(border, expectedBorder))
            {
                findings.Add("ASC-3", assetPath, path,
                    "type " + mode + " con '" + spritePath + "' border " + FmtBorder(border),
                    "spriteBorder " + declared.Border + " (sec.9: " + declared.Pieza + ")", spritePath);
            }
        }

        // ----------------------------------------------------------- ULN-1

        /// <summary>
        /// ULN-1: el par (anchors, pivot, sizeDelta) tiene que contar una sola historia. Las tres
        /// filas de design §5: inset, tamano fijo legitimo y pivote con correlato.
        /// </summary>
        public static void CheckRectTransform(RectTransform rect, string assetPath,
            ArtContractFindings findings, ArtContractCounters counters)
        {
            if (rect == null)
            {
                return;
            }

            counters.Bump("uln1.rectTotal");

            // El RectTransform de un Canvas lo gobierna el Canvas, no el autor.
            if (rect.GetComponent<Canvas>() != null)
            {
                counters.Bump("uln1.canvasRoot");
                return;
            }

            // Excepcion declarada: HUD de Game (design §5, HudAuthoringScale = 0,0221,
            // HudAuthoringWidth = 39822). No se normaliza en este cambio.
            if (Mathf.Abs(rect.sizeDelta.x - HudAuthoringWidth) <= 0.5f &&
                Mathf.Abs(rect.localScale.x - HudAuthoringScale) <= 0.0005f)
            {
                counters.Bump("uln1.hudAuthoring");
                return;
            }

            // El rect del root Page lo escribe el AspectRatioFitter cada frame.
            if (rect.GetComponent<AspectRatioFitter>() != null)
            {
                counters.Bump("uln1.aspectFitter");
                return;
            }

            Transform parent = rect.parent;
            bool underLayout = parent != null && parent.GetComponent<LayoutGroup>() != null;
            if (underLayout)
            {
                // El LayoutGroup del padre escribe anchors/sizeDelta de sus hijos.
                counters.Bump("uln1.layoutGroup");
            }

            float epsilon = AnchorEpsilon;
            Vector2 anchorMin = rect.anchorMin;
            Vector2 anchorMax = rect.anchorMax;
            Vector2 pivot = rect.pivot;
            Vector2 sizeDelta = rect.sizeDelta;

            bool stretchX = Mathf.Abs(anchorMin.x - anchorMax.x) > epsilon;
            bool stretchY = Mathf.Abs(anchorMin.y - anchorMax.y) > epsilon;

            string path = HierarchyPath(rect);
            string anchorText = "anchors " + Fmt(anchorMin.x) + ".." + Fmt(anchorMax.x) + " / " +
                Fmt(anchorMin.y) + ".." + Fmt(anchorMax.y);

            if (!underLayout)
            {
                if (stretchX && sizeDelta.x > epsilon)
                {
                    findings.Add("ULN-1", assetPath, path,
                        anchorText + " estirado en X con sizeDelta.x " + Fmt(sizeDelta.x),
                        "sizeDelta.x <= 0 (inset) cuando el ancla estira en X (ULN-1a)");
                }
                else if (!stretchX && sizeDelta.x < -epsilon)
                {
                    findings.Add("ULN-1", assetPath, path,
                        anchorText + " fijo en X con sizeDelta.x " + Fmt(sizeDelta.x),
                        "sizeDelta.x >= 0 (tamano pretendido) con ancla fija (ULN-1b)");
                }

                if (stretchY && sizeDelta.y > epsilon)
                {
                    findings.Add("ULN-1", assetPath, path,
                        anchorText + " estirado en Y con sizeDelta.y " + Fmt(sizeDelta.y),
                        "sizeDelta.y <= 0 (inset) cuando el ancla estira en Y (ULN-1a)");
                }
                else if (!stretchY && sizeDelta.y < -epsilon)
                {
                    findings.Add("ULN-1", assetPath, path,
                        anchorText + " fijo en Y con sizeDelta.y " + Fmt(sizeDelta.y),
                        "sizeDelta.y >= 0 (tamano pretendido) con ancla fija (ULN-1b)");
                }
            }

            bool pivotXOk = Mathf.Abs(pivot.x - 0.5f) <= epsilon ||
                (!stretchX && Mathf.Abs(anchorMin.x) <= epsilon && Mathf.Abs(pivot.x) <= epsilon) ||
                (!stretchX && Mathf.Abs(anchorMin.x - 1f) <= epsilon && Mathf.Abs(pivot.x - 1f) <= epsilon);
            bool pivotYOk = Mathf.Abs(pivot.y - 0.5f) <= epsilon ||
                (!stretchY && Mathf.Abs(anchorMin.y) <= epsilon && Mathf.Abs(pivot.y) <= epsilon) ||
                (!stretchY && Mathf.Abs(anchorMin.y - 1f) <= epsilon && Mathf.Abs(pivot.y - 1f) <= epsilon);

            if (!pivotXOk || !pivotYOk)
            {
                findings.Add("ULN-1", assetPath, path,
                    "pivot " + Fmt(pivot.x) + "/" + Fmt(pivot.y) + " con " + anchorText,
                    "0,5/0,5, o el borde del ancla en el eje fijo (ULN-1c)");
            }
        }

        // ------------------------------------------------------------ UCC-5

        /// <summary>
        /// UCC-5 con la politica declarada del design D5: margen fijo top 60 u / bottom 60 u /
        /// lados 0. Se mide en unidades del canvas, que con UCC-1..UCC-3 son 900x1900.
        /// </summary>
        public static void CheckSafeArea(Canvas canvas, string assetPath, ArtContractFindings findings,
            ArtContractCounters counters)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                counters.Bump("ucc5.sinRect");
                return;
            }

            Rect rect = canvasRect.rect;
            if (rect.width < 1f || rect.height < 1f)
            {
                counters.Bump("ucc5.canvasSinRect");
                return;
            }

            // UCC-4: el espacio de unidades es 900x1900. Si el Game view no esta en 9:19 el canvas
            // mide otra cosa y medir el margen de 60 u daria falsos positivos en bloque.
            float aspect = rect.width / rect.height;
            if (Mathf.Abs(aspect - DesignAspectRatio) > AspectTolerance * DesignAspectRatio)
            {
                counters.Bump("ucc5.canvasFueraDe919");
                return;
            }

            float left = rect.xMin;
            float right = rect.xMax;
            float bottom = rect.yMin + SafeMarginVertical;
            float top = rect.yMax - SafeMarginVertical;
            string expected = "x " + Fmt(left) + ".." + Fmt(right) + " / y " + Fmt(bottom) + ".." +
                Fmt(top) + " (margen fijo: top 60 / bottom 60 / lados 0)";

            var targets = new List<Component>();
            Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                targets.Add(selectables[i]);
            }
            TMPro.TMP_Text[] texts = canvas.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                targets.Add(texts[i]);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Component target = targets[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    counters.Bump("ucc5.inactivo");
                    continue;
                }
                if (IsInsideScrollContent(target.transform, canvas.transform))
                {
                    counters.Bump("ucc5.scrollContent");
                    continue;
                }

                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    canvasRect, target.transform);
                bool outX = bounds.min.x < left - SafeTolerance || bounds.max.x > right + SafeTolerance;
                bool outY = bounds.min.y < bottom - SafeTolerance || bounds.max.y > top + SafeTolerance;
                if (!outX && !outY)
                {
                    continue;
                }

                findings.Add("UCC-5", assetPath, HierarchyPath(target.transform),
                    target.GetType().Name + " en x " + Fmt(bounds.min.x) + ".." + Fmt(bounds.max.x) +
                    " / y " + Fmt(bounds.min.y) + ".." + Fmt(bounds.max.y),
                    expected);
            }
        }

        // ----------------------------------------------------------- helpers

        /// <summary>
        /// Path jerarquico del nodo dentro del asset que lo serializa: si el objeto es una instancia
        /// de prefab, se corta en la raiz de la instancia (asi la escena y el pase de prefabs
        /// producen el mismo texto y el hallazgo se deduplica en vez de contarse dos veces).
        /// </summary>
        public static string HierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "?";
            }
            Transform stop = null;
            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
            if (instanceRoot != null)
            {
                stop = instanceRoot.transform;
            }

            var builder = new StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                if (current == stop)
                {
                    break;
                }
                current = current.parent;
            }
            return builder.ToString();
        }

        /// <summary>El asset que serializa el valor: el prefab si el objeto es una instancia, si no la escena.</summary>
        public static string ResolveSourceAsset(GameObject gameObject, string fallbackPath)
        {
            if (gameObject == null)
            {
                return fallbackPath;
            }
            if (PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) != null)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    return prefabPath;
                }
            }
            return fallbackPath;
        }

        public static bool IsInPrefabInstance(GameObject gameObject)
        {
            return gameObject != null && PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) != null;
        }

        public static bool HasAncestorCanvas(Transform transform)
        {
            Transform current = transform == null ? null : transform.parent;
            while (current != null)
            {
                if (current.GetComponent<Canvas>() != null)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public static bool CameraHasLetterBoxer(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }
            MonoBehaviour[] behaviours = camera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null &&
                    string.Equals(behaviour.GetType().Name, LetterBoxerTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>El Page del prefab: el AspectRatioFitter del hijo directo, con preferencia por el nombre.</summary>
        public static AspectRatioFitter FindPageFitter(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return null;
            }
            AspectRatioFitter[] fitters = canvasTransform.GetComponentsInChildren<AspectRatioFitter>(true);
            if (fitters.Length == 0)
            {
                return null;
            }
            for (int i = 0; i < fitters.Length; i++)
            {
                if (string.Equals(fitters[i].gameObject.name, "Page", StringComparison.Ordinal))
                {
                    return fitters[i];
                }
            }
            return fitters[0];
        }

        /// <summary>Contenido de un ScrollRect: se recorta por diseno, no es una violacion de UCC-5.</summary>
        public static bool IsInsideScrollContent(Transform target, Transform stopAt)
        {
            Transform current = target;
            while (current != null && current != stopAt)
            {
                ScrollRect scroll = current.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null &&
                    (target == scroll.content.transform || target.IsChildOf(scroll.content)))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public static bool IsZero(Vector4 value)
        {
            return Mathf.Abs(value.x) < 0.001f && Mathf.Abs(value.y) < 0.001f &&
                Mathf.Abs(value.z) < 0.001f && Mathf.Abs(value.w) < 0.001f;
        }

        static bool NearlyEqual(Vector4 left, Vector4 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.001f && Mathf.Abs(left.y - right.y) < 0.001f &&
                Mathf.Abs(left.z - right.z) < 0.001f && Mathf.Abs(left.w - right.w) < 0.001f;
        }

        /// <summary>Numero corto y estable: el reporte no puede cambiar por el locale de la maquina.</summary>
        public static string Fmt(float value)
        {
            if (Mathf.Abs(value - Mathf.Round(value)) < 0.0001f)
            {
                return Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture);
            }
            return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Ratio con 5 decimales (0,47368 = 900/1900): se cita en el contrato.</summary>
        public static string FmtRatio(float value)
        {
            return value.ToString("0.#####", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        public static string FmtBorder(Vector4 border)
        {
            return Fmt(border.x) + "/" + Fmt(border.y) + "/" + Fmt(border.z) + "/" + Fmt(border.w);
        }
    }
}
#endif
