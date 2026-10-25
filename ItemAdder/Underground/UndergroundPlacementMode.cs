using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Rewired;
using StarterAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ItemAdder.Underground
{
    /// <summary>
    /// Mode placement de la trappe (outil de mise au point).
    ///
    /// F9 ouvre / ferme le mode : un fantôme translucide du sous-sol (cadre de
    /// trappe, salle, rampe, parcelles, rayon) suit le point visé par le
    /// joueur. Le fantôme est dessiné par une caméra de superposition qui vide
    /// le tampon de profondeur, il reste donc visible **à travers les murs**,
    /// ce qui permet de voir comment la salle se place sous le magasin.
    ///
    /// F10 relève la position courante. Quand le réglage temporaire
    /// <c>Placement.PauseAvantLog</c> est actif, le premier F10 met le jeu en pause
    /// pour vérifier l'emplacement à l'écran ; le second écrit les coordonnées dans
    /// le log sous une forme directement recopiable dans
    /// <see cref="UndergroundManager.HatchOverride"/>. Le sous-sol réel n'est pas
    /// déplacé : la position devient définitive quand l'override est renseigné.
    /// </summary>
    internal sealed class UndergroundPlacementMode : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F9;
        private const KeyCode ConfirmKey = KeyCode.F10;

        // Cotes du sous-sol, identiques à UndergroundBasement.
        private const float FloorY = -7f;
        private const float RoomWidth = 18f;
        private const float RoomDepth = 16f;
        private const float RoomHeight = 6.1f;
        private const float RoomCenterOffsetZ = -3.5f;

        private GameObject _ghostRoot;
        private GameObject _hatchGroup;
        private GameObject _roomGroup;
        private readonly List<GameObject> _ghostParts = new List<GameObject>();

        private Material _structureMaterial;
        private Material _hatchMaterial;
        private Material _plotMaterial;

        private Camera _previewCamera;
        private int _previewLayer = -1;

        // Caméra principale dont le culling mask a été élargi au layer du
        // fantôme : si la caméra de superposition n'est pas rendue par le
        // pipeline, le fantôme reste au moins visible normalement.
        private Camera _maskCamera;
        private int _maskOriginal;

        private bool _active;
        private bool _hasAim;
        private bool _forcedConfirm;
        private Vector3 _hatch;

        // TEMPORAIRE (mise au point du placement) : le jeu est gelé entre le
        // premier et le second F10 pour laisser le temps de vérifier le fantôme.
        private bool _frozen;
        private float _savedTimeScale = 1f;

        private string _notice = "";
        private float _noticeUntil;

        private GUIStyle _textStyle;

        private void Awake()
        {
            Plugin.Log.LogInfo(
                $"[Placement] Module chargé ({Plugin.PluginVersion}) — F9 place la trappe, F10 relève les coordonnées.");
        }

        private void Update()
        {
            if (KeyPressed(ToggleKey))
            {
                Plugin.Log.LogInfo("[Placement] F9 détecté.");
                if (_active)
                    StopPlacement();
                else
                    StartPlacement();
                return;
            }

            if (!_active)
                return;

            if (KeyPressed(ConfirmKey))
            {
                Plugin.Log.LogInfo("[Placement] F10 détecté.");

                if (PauseBeforeLog && !_frozen)
                {
                    FreezeForReview();
                    return;
                }

                Confirm();
                return;
            }

            UpdateAim();
            UpdateGhost();
        }

        /// <summary>
        /// Lecture de la touche via l'Input legacy, puis via le clavier Rewired :
        /// selon la configuration du jeu, l'un des deux peut être indisponible.
        /// </summary>
        private static bool KeyPressed(KeyCode key)
        {
            try
            {
                if (Input.GetKeyDown(key))
                    return true;
            }
            catch
            {
                // Input legacy désactivé dans les options du jeu.
            }

            try
            {
                Player player = ReInput.players.GetPlayer(0);
                if (player != null && player.controllers != null &&
                    player.controllers.Keyboard != null)
                    return player.controllers.Keyboard.GetKeyDown(key);
            }
            catch
            {
                // Rewired pas encore initialisé.
            }

            return false;
        }

        private void LateUpdate()
        {
            if (_active)
                SyncPreviewCamera();
        }

        private void OnDisable()
        {
            if (_active)
                StopPlacement();

            RestoreTime();
        }

        private void OnDestroy()
        {
            RestoreTime();
        }

        private static bool PauseBeforeLog
        {
            get
            {
                try
                {
                    return Plugin.PlacementPauseBeforeLog != null &&
                        Plugin.PlacementPauseBeforeLog.Value;
                }
                catch
                {
                    return false;
                }
            }
        }

        private void StartPlacement()
        {
            UndergroundManager manager = UndergroundManager.Instance;
            if (manager == null || !manager.HasLayout)
            {
                Notify("Sous-sol pas encore construit : attends que le magasin soit chargé.");
                return;
            }

            if (manager.IsLocalPlayerInBasement)
            {
                Notify("Rentre dans le magasin avant de placer la trappe.");
                return;
            }

            EnsureAssets();
            AddGhostLayerToMainCamera();

            _active = true;
            _hasAim = false;

            // L'ancienne trappe est masquée pour ne pas se superposer au fantôme.
            manager.SetDoorVisible(false);

            UpdateAim();
            UpdateGhost();

            Plugin.Log.LogInfo(PauseBeforeLog
                ? "[Placement] Mode placement activé : vise le sol, F10 fige le jeu pour vérifier " +
                  "l'emplacement puis écrit les coordonnées au second F10, F9 annule."
                : "[Placement] Mode placement activé : vise le sol, F10 relève les coordonnées, F9 annule.");
        }

        private void StopPlacement()
        {
            _active = false;
            _hasAim = false;
            RestoreTime();

            if (_previewCamera != null)
                _previewCamera.enabled = false;

            DestroyGhost();
            RestoreMainCameraMask();

            UndergroundManager manager = UndergroundManager.Instance;
            if (manager != null)
                manager.SetDoorVisible(true);

            Plugin.Log.LogInfo("[Placement] Mode placement désactivé.");
        }

        /// <summary>
        /// TEMPORAIRE : gèle le jeu pour inspecter le fantôme et les coordonnées
        /// affichées avant qu'elles ne partent dans la console.
        /// </summary>
        private void FreezeForReview()
        {
            if (_frozen)
                return;

            _frozen = true;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            Notify("Jeu en pause : vérifie l'emplacement. " +
                "F10 écrit les coordonnées dans le log, F9 annule.");
        }

        /// <summary>
        /// TEMPORAIRE : rend au jeu sa vitesse d'avant le gel.
        /// </summary>
        private void RestoreTime()
        {
            if (!_frozen)
                return;

            _frozen = false;
            Time.timeScale = _savedTimeScale;
            Plugin.Log.LogInfo("[Placement] Jeu relancé.");
        }

        private void Confirm()
        {
            if (!_hasAim)
            {
                Notify("Aucun point visé : regarde le sol puis retente F10.");
                return;
            }

            RestoreTime();

            float overflow = GetRoomOverflow();

            // Une salle qui dépasse du magasin finit enterrée dans le décor
            // (rampe comprise) : on demande une seconde confirmation.
            if (overflow <= 0f)
            {
                _forcedConfirm = false;
            }
            else if (!_forcedConfirm)
            {
                _forcedConfirm = true;
                Notify($"La salle dépasse de {Format(overflow)} m hors du magasin : elle sera " +
                       "enterrée. Rappuie sur F10 pour forcer.");
                return;
            }

            string x = Format(_hatch.x);
            string y = Format(_hatch.y);
            string z = Format(_hatch.z);

            Plugin.Log.LogInfo("[Placement] ================= COORDONNÉES DE LA TRAPPE =================");
            Plugin.Log.LogInfo($"[Placement] Trappe X = {x}   Y = {y}   Z = {z}");
            Plugin.Log.LogInfo(
                $"[Placement] HatchOverride = new Vector3({x}f, {y}f, {z}f);");
            Plugin.Log.LogInfo(
                overflow <= 0f
                    ? "[Placement] Salle entièrement sous le magasin : OK."
                    : $"[Placement] Attention : la salle dépasse du magasin de {Format(overflow)} m.");
            Plugin.Log.LogInfo(
                "[Placement] Recopie la ligne HatchOverride à l'agent pour qu'elle devienne définitive.");
            Plugin.Log.LogInfo("[Placement] ========================================================");

            Notify("Coordonnées écrites dans le log (BepInEx\\LogOutput.log).");
            StopPlacement();
        }

        private void UpdateAim()
        {
            Camera camera = FindMainCamera();
            bool found = false;
            Vector3 point = Vector3.zero;

            if (camera != null &&
                Physics.Raycast(camera.transform.position, camera.transform.forward,
                    out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                found = true;

                // Visée sur un mur : on cherche le sol juste derrière.
                if (hit.normal.y < 0.5f &&
                    Physics.Raycast(hit.point + Vector3.up * 2.5f, Vector3.down,
                        out RaycastHit floor, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    point = floor.point;
                }
            }

            if (!found)
            {
                FirstPersonController player = FirstPersonController.Instance;
                if (player == null)
                    return;

                point = player.transform.position;
            }

            _hatch = point;
            _hasAim = true;
        }

        private void UpdateGhost()
        {
            if (!_hasAim)
                return;

            EnsureGhost();

            _ghostRoot.transform.position = _hatch;
            _hatchGroup.transform.localPosition = new Vector3(0f, 0.05f - _hatch.y, 0f);
            _roomGroup.transform.localPosition =
                new Vector3(0f, FloorY - _hatch.y + 0.02f, RoomCenterOffsetZ);
        }

        private void EnsureGhost()
        {
            if (_ghostRoot != null)
                return;

            EnsureAssets();

            _ghostRoot = new GameObject("ItemAdder_PlacementGhost");

            _hatchGroup = new GameObject("Hatch");
            _hatchGroup.transform.SetParent(_ghostRoot.transform, false);

            _roomGroup = new GameObject("Room");
            _roomGroup.transform.SetParent(_ghostRoot.transform, false);

            // Cadre de la trappe : mêmes cotes que BasementDoor.
            CreatePart(_hatchGroup.transform, "FrameNorth",
                new Vector3(0f, 0f, 1.55f), new Vector3(3.3f, 0.12f, 0.18f), _hatchMaterial);
            CreatePart(_hatchGroup.transform, "FrameSouth",
                new Vector3(0f, 0f, -1.55f), new Vector3(3.3f, 0.12f, 0.18f), _hatchMaterial);
            CreatePart(_hatchGroup.transform, "FrameWest",
                new Vector3(-1.55f, 0f, 0f), new Vector3(0.18f, 0.12f, 3.3f), _hatchMaterial);
            CreatePart(_hatchGroup.transform, "FrameEast",
                new Vector3(1.55f, 0f, 0f), new Vector3(0.18f, 0.12f, 3.3f), _hatchMaterial);
            CreatePart(_hatchGroup.transform, "Hole",
                new Vector3(0f, -0.03f, 0f), new Vector3(2.9f, 0.02f, 2.9f), _hatchMaterial);

            // Coque de la salle : y local 0 = niveau du sol du sous-sol.
            float wallY = RoomHeight * 0.5f;
            CreatePart(_roomGroup.transform, "Floor",
                new Vector3(0f, -0.2f, 0f), new Vector3(RoomWidth, 0.4f, RoomDepth), _structureMaterial);
            CreatePart(_roomGroup.transform, "WallNorth",
                new Vector3(0f, wallY, RoomDepth * 0.5f),
                new Vector3(RoomWidth, RoomHeight, 0.45f), _structureMaterial);
            CreatePart(_roomGroup.transform, "WallSouth",
                new Vector3(0f, wallY, -RoomDepth * 0.5f),
                new Vector3(RoomWidth, RoomHeight, 0.45f), _structureMaterial);
            CreatePart(_roomGroup.transform, "WallWest",
                new Vector3(-RoomWidth * 0.5f, wallY, 0f),
                new Vector3(0.45f, RoomHeight, RoomDepth), _structureMaterial);
            CreatePart(_roomGroup.transform, "WallEast",
                new Vector3(RoomWidth * 0.5f, wallY, 0f),
                new Vector3(0.45f, RoomHeight, RoomDepth), _structureMaterial);

            // Rampe d'accès (même pente que CreateStaircase).
            Vector3 rampTop = new Vector3(0f, -0.25f - FloorY, 2.5f);
            Vector3 rampBottom = new Vector3(0f, 0f, -4.1f);
            float horizontal = Mathf.Abs(rampTop.z - rampBottom.z);
            float drop = Mathf.Abs(rampTop.y - rampBottom.y);
            float length = Mathf.Sqrt(horizontal * horizontal + drop * drop);
            float angle = Mathf.Atan2(drop, horizontal) * Mathf.Rad2Deg;

            GameObject ramp = CreatePart(_roomGroup.transform, "Ramp",
                new Vector3(0f, (rampTop.y + rampBottom.y) * 0.5f - 0.1f,
                    (rampTop.z + rampBottom.z) * 0.5f),
                new Vector3(3.2f, 0.2f, length), _structureMaterial);
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);

            // Parcelles.
            float[] plotX = { -6.3f, -3.6f };
            float[] plotZ = { -3.4f, -0.4f, 2.6f };
            for (int row = 0; row < plotZ.Length; row++)
            {
                for (int column = 0; column < plotX.Length; column++)
                {
                    CreatePart(_roomGroup.transform, $"Plot{row}{column}",
                        new Vector3(plotX[column], 0.25f, plotZ[row]),
                        new Vector3(1f, 0.55f, 1f), _plotMaterial);
                }
            }

            // Emplacement du rayon de vente.
            CreatePart(_roomGroup.transform, "Shelf",
                new Vector3(5.7f, 0.7f, -2.2f), new Vector3(2.6f, 1.4f, 0.9f), _structureMaterial);
        }

        private GameObject CreatePart(
            Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (_previewLayer >= 0)
                part.layer = _previewLayer;

            _ghostParts.Add(part);
            return part;
        }

        private void DestroyGhost()
        {
            _ghostParts.Clear();

            if (_ghostRoot != null)
            {
                Object.Destroy(_ghostRoot);
                _ghostRoot = null;
            }

            _hatchGroup = null;
            _roomGroup = null;
        }

        private void EnsureAssets()
        {
            if (_previewLayer < 0)
                _previewLayer = FindFreeLayer();

            if (_structureMaterial == null)
                _structureMaterial = CreateGhostMaterial(new Color(0.25f, 0.85f, 1f, 0.30f));
            if (_hatchMaterial == null)
                _hatchMaterial = CreateGhostMaterial(new Color(1f, 0.82f, 0.15f, 0.55f));
            if (_plotMaterial == null)
                _plotMaterial = CreateGhostMaterial(new Color(0.35f, 1f, 0.35f, 0.45f));

            EnsurePreviewCamera();
        }

        private static int FindFreeLayer()
        {
            for (int layer = 27; layer <= 31; layer++)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                    return layer;
            }

            return 31;
        }

        private static Material CreateGhostMaterial(Color color)
        {
            Shader shader = null;
            string[] names =
            {
                "Universal Render Pipeline/Unlit",
                "Sprites/Default",
                "Unlit/Color",
                "Standard"
            };

            for (int i = 0; i < names.Length && shader == null; i++)
                shader = Shader.Find(names[i]);

            if (shader == null)
            {
                Plugin.Log.LogWarning("[Placement] Aucun shader de prévisualisation trouvé.");
                return null;
            }

            var material = new Material(shader)
            {
                name = "ItemAdder_PlacementGhost",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Surface", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        private void AddGhostLayerToMainCamera()
        {
            Camera main = FindMainCamera();
            if (main == null || _previewLayer < 0)
                return;

            _maskCamera = main;
            _maskOriginal = main.cullingMask;
            main.cullingMask = _maskOriginal | (1 << _previewLayer);
        }

        private void RestoreMainCameraMask()
        {
            if (_maskCamera == null)
                return;

            _maskCamera.cullingMask = _maskOriginal;
            _maskCamera = null;
        }

        private void EnsurePreviewCamera()
        {
            if (_previewCamera != null)
                return;

            if (_previewLayer < 0)
                _previewLayer = FindFreeLayer();

            var cameraObject = new GameObject("ItemAdder_PlacementCamera");
            cameraObject.transform.SetParent(transform, false);

            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.Depth;
            _previewCamera.cullingMask = 1 << _previewLayer;
            _previewCamera.useOcclusionCulling = false;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = false;
            _previewCamera.depthTextureMode = DepthTextureMode.None;
            _previewCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _previewCamera.enabled = false;

            ConfigureUrpCamera(_previewCamera);

            Plugin.Log.LogInfo(
                $"[Placement] Caméra de prévisualisation prête (layer {_previewLayer}).");
        }

        /// <summary>
        /// Règle la caméra de superposition côté URP sans dépendre de
        /// l'assembly Universal RP : caméra de base, sans post-traitement.
        /// </summary>
        private static void ConfigureUrpCamera(Camera camera)
        {
            try
            {
                Type dataType = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length && dataType == null; i++)
                {
                    dataType = assemblies[i].GetType(
                        "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData", false);
                }

                if (dataType == null)
                    return;

                Component data = camera.GetComponent(dataType);
                if (data == null)
                    data = camera.gameObject.AddComponent(dataType);
                if (data == null)
                    return;

                SetMember(data, "renderType", 0);
                SetMember(data, "renderPostProcessing", false);
                SetMember(data, "requiresDepthOption", 0);
                SetMember(data, "requiresColorOption", 0);
                SetMember(data, "volumeLayerMask", 0);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning(
                    $"[Placement] Caméra de prévisualisation non configurée : {exception.Message}");
            }
        }

        private static void SetMember(object target, string name, object value)
        {
            try
            {
                PropertyInfo property = target.GetType().GetProperty(name);
                if (property == null || !property.CanWrite || property.GetIndexParameters().Length > 0)
                    return;

                Type type = property.PropertyType;
                object converted = value;

                if (type.IsEnum)
                    converted = Enum.ToObject(type, value);
                else if (value != null && type != value.GetType())
                    converted = type.IsValueType ? Activator.CreateInstance(type) : null;

                property.SetValue(target, converted, null);
            }
            catch
            {
                // Détail cosmétique : la prévisualisation reste utilisable.
            }
        }

        private void SyncPreviewCamera()
        {
            Camera main = FindMainCamera();
            if (main == null)
                return;

            if (_previewCamera == null)
                EnsurePreviewCamera();
            if (_previewCamera == null)
                return;

            Transform cameraTransform = _previewCamera.transform;
            cameraTransform.position = main.transform.position;
            cameraTransform.rotation = main.transform.rotation;

            _previewCamera.fieldOfView = main.fieldOfView;
            _previewCamera.orthographic = main.orthographic;
            _previewCamera.orthographicSize = main.orthographicSize;
            _previewCamera.nearClipPlane = main.nearClipPlane;
            _previewCamera.farClipPlane = main.farClipPlane;
            _previewCamera.aspect = main.aspect;
            _previewCamera.rect = main.rect;
            _previewCamera.depth = main.depth + 1f;

            if (!_previewCamera.enabled)
                _previewCamera.enabled = true;
        }

        private Camera FindMainCamera()
        {
            Camera camera = Camera.main;
            if (camera != null && camera != _previewCamera)
                return camera;

            FirstPersonController player = FirstPersonController.Instance;
            if (player != null)
            {
                camera = player.GetComponentInChildren<Camera>(true);
                if (camera != null && camera != _previewCamera)
                    return camera;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i] != _previewCamera && cameras[i].enabled)
                    return cameras[i];
            }

            return null;
        }

        /// <summary>
        /// Dépassement de la salle hors des limites de construction du
        /// magasin, en mètres (0 = la salle tient entièrement dessous).
        /// </summary>
        private float GetRoomOverflow()
        {
            UndergroundManager manager = UndergroundManager.Instance;
            if (manager == null)
                return 0f;

            Vector2 shopX = manager.ShopX;
            Vector2 shopZ = manager.ShopZ;
            if (shopX.y <= shopX.x || shopZ.y <= shopZ.x)
                return 0f;

            float centerX = _hatch.x;
            float centerZ = _hatch.z + RoomCenterOffsetZ;

            float overflow = 0f;
            overflow += Mathf.Max(0f, shopX.x - (centerX - RoomWidth * 0.5f));
            overflow += Mathf.Max(0f, (centerX + RoomWidth * 0.5f) - shopX.y);
            overflow += Mathf.Max(0f, shopZ.x - (centerZ - RoomDepth * 0.5f));
            overflow += Mathf.Max(0f, (centerZ + RoomDepth * 0.5f) - shopZ.y);
            return overflow;
        }

        private void Notify(string message)
        {
            _notice = message;
            // Horloge non mise à l'échelle : le message doit aussi expirer quand le jeu est gelé.
            _noticeUntil = Time.unscaledTime + 5f;
            Plugin.Log.LogInfo($"[Placement] {message}");
        }

        private static string Format(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void OnGUI()
        {
            if (_active)
            {
                DrawPanel();
            }
            else if (!string.IsNullOrEmpty(_notice) && Time.unscaledTime < _noticeUntil)
            {
                EnsureStyles();
                GUI.Box(new Rect(20f, 140f, 430f, 34f), _notice, _textStyle);
            }
        }

        private void DrawPanel()
        {
            EnsureStyles();

            float overflow = GetRoomOverflow();
            string roomState = overflow <= 0f
                ? "OK : la salle tient sous le magasin"
                : $"! la salle dépasse de {Format(overflow)} m";

            string hint = _frozen
                ? "JEU EN PAUSE — F10 : écrire les coordonnées dans le log   F9 : annuler"
                : PauseBeforeLog
                    ? "F10 : figer le jeu et vérifier   F9 : annuler"
                    : "F10 : relever les coordonnées (log)   F9 : annuler";

            string text =
                "MODE PLACEMENT DE LA TRAPPE\n" +
                hint + "\n" +
                $"Trappe : X {Format(_hatch.x)}   Y {Format(_hatch.y)}   Z {Format(_hatch.z)}\n" +
                $"Salle 18 x 16 m : {roomState}";

            GUI.Box(new Rect(20f, 140f, 500f, 104f), text, _textStyle);
        }

        private void EnsureStyles()
        {
            if (_textStyle != null)
                return;

            _textStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
        }
    }
}
