// ============================================================================
// ETD.EditorTools - TurretIconFootageWindow.cs
// Put in: Assets/Scripts/Editor/TurretIconFootageWindow.cs
//
// Shoots turret icons through a HAND-DRESSED SCENE (Assets/Scenes/Photoage.unity)
// rather than a rig this tool builds itself. The camera framing, lighting and
// post-processing in that scene are the art direction; this tool only places one
// prefab at a time in front of the camera, spins its upper body so the set does
// not all read identically, and captures the frame.
//
// Everything about the look lives in the scene. If an icon comes out wrong, move
// the camera or retune the volume in Photoage and shoot again — no code change.
//
// Usage:  Tools > ETD > Art > Turret Icon Footage
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ETD.Data;

namespace ETD.EditorTools
{
    public class TurretIconFootageWindow : EditorWindow
    {
        private const string ScenePathKey = "ETD_IconFootage_Scene";
        private const string OutputKey = "ETD_IconFootage_Output";
        private const string SizeKey = "ETD_IconFootage_Size";
        private const string SupersampleKey = "ETD_IconFootage_Supersample";
        private const string AnchorKey = "ETD_IconFootage_Anchor";
        private const string YawRangeKey = "ETD_IconFootage_YawRange";
        private const string YawOffsetKey = "ETD_IconFootage_YawOffset";
        private const string SeedKey = "ETD_IconFootage_Seed";
        private const string BackgroundKey = "ETD_IconFootage_Background";
        private const string RebuildAlphaKey = "ETD_IconFootage_RebuildAlpha";
        private const string AssignKey = "ETD_IconFootage_Assign";
        private const string OverwriteKey = "ETD_IconFootage_Overwrite";

        private enum BackgroundOverride
        {
            /// <summary>Whatever the scene's camera is already set to. Default, because
            /// the scene is the art direction.</summary>
            UseScene = 0,
            Black,
            Transparent
        }

        private struct IconJob
        {
            public TurretData Turret;
            public string SlotName;      // "", "PathA", "PathB", "Tier2"
            public GameObject Prefab;
            public string FileName;
            public bool HasIconAlready;
        }

        private string _scenePath = "Assets/Scenes/Photoage.unity";
        private string _outputFolder = "Assets/Art/Textures/Icons/Turrets";
        private string _anchorName = "IconAnchor";
        private int _size = 512;
        private int _supersample = 3;

        [Tooltip("Random yaw applied to the turret's upper body, in degrees either side of the offset.")]
        private float _yawRange = 75f;
        private float _yawOffset = 0f;
        private int _seed = 12345;

        private BackgroundOverride _background = BackgroundOverride.UseScene;
        private bool _rebuildAlphaFromLuminance;
        private bool _assignToTurretData = true;
        private bool _overwriteExisting;

        private Vector2 _scroll;
        private readonly List<IconJob> _jobs = new();
        private string _lastResult;

        [MenuItem("Tools/ETD/Art/Turret Icon Footage")]
        public static void Open()
        {
            GetWindow<TurretIconFootageWindow>("Icon Footage");
        }

        private void OnEnable()
        {
            _scenePath = EditorPrefs.GetString(ScenePathKey, _scenePath);
            _outputFolder = EditorPrefs.GetString(OutputKey, _outputFolder);
            _size = EditorPrefs.GetInt(SizeKey, _size);
            _supersample = EditorPrefs.GetInt(SupersampleKey, _supersample);
            _anchorName = EditorPrefs.GetString(AnchorKey, _anchorName);
            _yawRange = EditorPrefs.GetFloat(YawRangeKey, _yawRange);
            _yawOffset = EditorPrefs.GetFloat(YawOffsetKey, _yawOffset);
            _seed = EditorPrefs.GetInt(SeedKey, _seed);
            _background = (BackgroundOverride)EditorPrefs.GetInt(BackgroundKey, (int)_background);
            _rebuildAlphaFromLuminance = EditorPrefs.GetBool(RebuildAlphaKey, _rebuildAlphaFromLuminance);
            _assignToTurretData = EditorPrefs.GetBool(AssignKey, _assignToTurretData);
            _overwriteExisting = EditorPrefs.GetBool(OverwriteKey, _overwriteExisting);
            RebuildJobs();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(ScenePathKey, _scenePath);
            EditorPrefs.SetString(OutputKey, _outputFolder);
            EditorPrefs.SetInt(SizeKey, _size);
            EditorPrefs.SetInt(SupersampleKey, _supersample);
            EditorPrefs.SetString(AnchorKey, _anchorName);
            EditorPrefs.SetFloat(YawRangeKey, _yawRange);
            EditorPrefs.SetFloat(YawOffsetKey, _yawOffset);
            EditorPrefs.SetInt(SeedKey, _seed);
            EditorPrefs.SetInt(BackgroundKey, (int)_background);
            EditorPrefs.SetBool(RebuildAlphaKey, _rebuildAlphaFromLuminance);
            EditorPrefs.SetBool(AssignKey, _assignToTurretData);
            EditorPrefs.SetBool(OverwriteKey, _overwriteExisting);
        }

        // =================================================================
        // GUI
        // =================================================================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Shot scene", EditorStyles.boldLabel);
            _scenePath = EditorGUILayout.TextField("Scene", _scenePath);
            _anchorName = EditorGUILayout.TextField("Anchor object", _anchorName);
            EditorGUILayout.HelpBox(
                "Camera position, lighting and post-processing all come from this scene — " +
                "nothing here overrides them. Add an empty GameObject named '" + _anchorName +
                "' to control where each turret is placed and how it faces; without one, " +
                "prefabs are placed at the world origin.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
            _size = EditorGUILayout.IntPopup("Icon size", _size,
                new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            _supersample = EditorGUILayout.IntSlider("Supersample", _supersample, 1, 4);
            EditorGUILayout.LabelField(" ",
                $"Renders at {_size * _supersample}px, downsamples to {_size}px",
                EditorStyles.miniLabel);

            _background = (BackgroundOverride)EditorGUILayout.EnumPopup("Background", _background);
            _rebuildAlphaFromLuminance = EditorGUILayout.Toggle(
                "Rebuild alpha from luminance", _rebuildAlphaFromLuminance);
            if (_rebuildAlphaFromLuminance)
            {
                EditorGUILayout.HelpBox(
                    "Post-processing usually flattens alpha to opaque. This turns a black " +
                    "background back into transparency using pixel brightness, which keeps " +
                    "bloom halos instead of clipping them at the silhouette. Only use it " +
                    "with a black background.", MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upper-body rotation", EditorStyles.boldLabel);
            _yawOffset = EditorGUILayout.Slider("Yaw offset", _yawOffset, -180f, 180f);
            _yawRange = EditorGUILayout.Slider("Random yaw +/-", _yawRange, 0f, 180f);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            EditorGUILayout.HelpBox(
                "The turret's rotating part (TurretController's rotation transform) is spun " +
                "per prefab so the set does not read as one repeated pose. The seed is " +
                "hashed with the prefab name, so re-running produces the same icons — " +
                "change the seed to reshuffle every pose at once.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField("Folder", _outputFolder);
            _assignToTurretData = EditorGUILayout.Toggle("Assign to TurretData", _assignToTurretData);
            _overwriteExisting = EditorGUILayout.Toggle("Include slots that already have an icon", _overwriteExisting);

            EditorGUILayout.Space();
            if (GUILayout.Button("Rescan turrets"))
                RebuildJobs();

            int pending = 0;
            for (int i = 0; i < _jobs.Count; i++)
                if (_overwriteExisting || !_jobs[i].HasIconAlready) pending++;

            EditorGUILayout.LabelField($"{pending} icon(s) queued", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(pending == 0))
            {
                if (GUILayout.Button(_overwriteExisting ? "Shoot ALL icons" : "Shoot missing icons",
                        GUILayout.Height(30)))
                {
                    SavePrefs();
                    ShootAll();
                }
            }

            if (!string.IsNullOrEmpty(_lastResult))
                EditorGUILayout.HelpBox(_lastResult, MessageType.Info);

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _jobs.Count; i++)
            {
                IconJob job = _jobs[i];
                bool willShoot = _overwriteExisting || !job.HasIconAlready;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    (willShoot ? "• " : "   ") + job.Turret.name +
                    (string.IsNullOrEmpty(job.SlotName) ? "" : " / " + job.SlotName),
                    GUILayout.Width(230));
                EditorGUILayout.LabelField(job.FileName + ".png" + (job.HasIconAlready ? "  (has icon)" : ""),
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // =================================================================
        // JOB DISCOVERY
        // =================================================================

        /// <summary>
        /// Pairs every icon slot with the prefab that slot actually spawns. The mapping is
        /// read from the data rather than guessed from file names, which is what keeps it
        /// correct for Laser — whose Path A/B prefabs are wired in the opposite order to
        /// every other turret.
        /// </summary>
        private void RebuildJobs()
        {
            _jobs.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:TurretData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<TurretData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                AddJob(data, "", data.Prefab, data.Icon, null);
                if (data.PathA != null) AddJob(data, "PathA", data.PathA.EvolvedPrefab, data.PathA.Icon, data.Icon);
                if (data.PathB != null) AddJob(data, "PathB", data.PathB.EvolvedPrefab, data.PathB.Icon, data.Icon);
                if (data.Tier2 != null) AddJob(data, "Tier2", data.Tier2.EvolvedPrefab, data.Tier2.Icon, data.Icon);
            }

            _jobs.Sort((a, b) => string.CompareOrdinal(a.Turret.name + a.SlotName, b.Turret.name + b.SlotName));
        }

        private void AddJob(TurretData data, string slot, GameObject prefab, Sprite existing, Sprite baseIcon)
        {
            if (prefab == null)
                return;

            // An evolution pointed at the base turret's sprite is a placeholder, not a real
            // icon, so it counts as missing.
            bool isPlaceholder = existing != null && baseIcon != null && existing == baseIcon;

            _jobs.Add(new IconJob
            {
                Turret = data,
                SlotName = slot,
                Prefab = prefab,
                FileName = prefab.name,
                HasIconAlready = existing != null && !isPlaceholder
            });
        }

        // =================================================================
        // CAPTURE
        // =================================================================

        private void ShootAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string previousScene = SceneManager.GetActiveScene().path;

            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);

            int written = 0;
            var writtenPaths = new List<string>();

            try
            {
                Scene shotScene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Single);
                if (!shotScene.IsValid())
                {
                    _lastResult = "Could not open " + _scenePath;
                    return;
                }

                Camera cam = FindShotCamera();
                if (cam == null)
                {
                    _lastResult = "No enabled camera found in " + _scenePath;
                    return;
                }

                Transform anchor = FindAnchor();

                for (int i = 0; i < _jobs.Count; i++)
                {
                    IconJob job = _jobs[i];
                    if (job.Prefab == null) continue;
                    if (!_overwriteExisting && job.HasIconAlready) continue;

                    EditorUtility.DisplayProgressBar("Shooting turret icons",
                        job.Turret.name + " " + job.SlotName, (float)i / Mathf.Max(1, _jobs.Count));

                    string path = Path.Combine(_outputFolder, job.FileName + ".png").Replace('\\', '/');
                    if (ShootOne(job, cam, anchor, path))
                    {
                        written++;
                        writtenPaths.Add(path);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // The shot scene is never saved: every turret placed in it is removed
                // again, and reopening leaves it exactly as it was dressed.
                if (!string.IsNullOrEmpty(previousScene) && previousScene != _scenePath)
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            AssetDatabase.Refresh();

            for (int i = 0; i < writtenPaths.Count; i++)
                ApplySpriteImportSettings(writtenPaths[i]);

            if (_assignToTurretData)
                AssignSprites();

            AssetDatabase.SaveAssets();
            RebuildJobs();

            _lastResult = $"Shot {written} icon(s) into {_outputFolder}." +
                          (_assignToTurretData ? " Sprites assigned to the turret assets." : "");
            Debug.Log("[TurretIconFootage] " + _lastResult);
        }

        private bool ShootOne(IconJob job, Camera cam, Transform anchor, string path)
        {
            GameObject instance = null;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = cam.targetTexture;
            CameraClearFlags previousFlags = cam.clearFlags;
            Color previousBackground = cam.backgroundColor;

            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(job.Prefab);
                if (instance == null) return false;

                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.position = anchor != null ? anchor.position : Vector3.zero;
                instance.transform.rotation = anchor != null ? anchor.rotation : Quaternion.identity;

                // Gameplay scripts never tick in edit mode, but a disabled component also
                // cannot surprise us by reacting to being instantiated.
                foreach (MonoBehaviour mb in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null) mb.enabled = false;

                RandomizeUpperBody(instance, job);

                ApplyBackgroundOverride(cam);

                int renderSize = _size * Mathf.Clamp(_supersample, 1, 4);
                rt = new RenderTexture(renderSize, renderSize, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB) { antiAliasing = 8 };

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var full = new Texture2D(renderSize, renderSize, TextureFormat.RGBA32, false, false);
                full.ReadPixels(new Rect(0, 0, renderSize, renderSize), 0, 0);
                full.Apply();

                Color32[] pixels = Downsample(full.GetPixels32(), renderSize, _size);
                DestroyImmediate(full);

                if (_rebuildAlphaFromLuminance)
                    pixels = RebuildAlpha(pixels);

                var outTex = new Texture2D(_size, _size, TextureFormat.RGBA32, false, false);
                outTex.SetPixels32(pixels);
                outTex.Apply();
                File.WriteAllBytes(path, outTex.EncodeToPNG());
                DestroyImmediate(outTex);
                return true;
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = previousTarget;
                cam.clearFlags = previousFlags;
                cam.backgroundColor = previousBackground;
                if (rt != null) { rt.Release(); DestroyImmediate(rt); }
                if (instance != null) DestroyImmediate(instance);
            }
        }

        private void ApplyBackgroundOverride(Camera cam)
        {
            switch (_background)
            {
                case BackgroundOverride.Black:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 1f);
                    break;
                case BackgroundOverride.Transparent:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    break;
            }
        }

        /// <summary>
        /// Spins the turret's rotating part so the finished set does not read as the same
        /// pose twenty times. The angle is derived from the prefab name plus the seed, so a
        /// re-run reproduces the same icons rather than silently reshuffling them.
        /// </summary>
        private void RandomizeUpperBody(GameObject instance, IconJob job)
        {
            Transform part = FindRotationPart(instance);
            if (part == null)
                return;

            var random = new System.Random(_seed ^ job.FileName.GetHashCode());
            float t = (float)random.NextDouble() * 2f - 1f;
            float yaw = _yawOffset + (t * _yawRange);

            part.localRotation = part.localRotation * Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// The upper body is whatever TurretController drives when it aims, so it is read
        /// off the component rather than guessed. Name matching is only a fallback for
        /// prefabs where that field was never wired.
        /// </summary>
        private static Transform FindRotationPart(GameObject instance)
        {
            foreach (MonoBehaviour mb in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "TurretController")
                    continue;

                var so = new SerializedObject(mb);
                SerializedProperty prop = so.FindProperty("_rotationPart");
                if (prop != null && prop.objectReferenceValue is Transform t && t != null)
                    return t;
            }

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("turret_top") || n.Contains("rotator") ||
                    n.Contains("upper") || n.Contains("gun"))
                {
                    return t;
                }
            }

            return null;
        }

        private Camera FindShotCamera()
        {
            Camera best = null;

            foreach (Camera c in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (best == null || c.depth > best.depth) best = c;
            }

            return best;
        }

        private Transform FindAnchor()
        {
            if (string.IsNullOrWhiteSpace(_anchorName))
                return null;

            GameObject go = GameObject.Find(_anchorName);
            return go != null ? go.transform : null;
        }

        // =================================================================
        // IMAGE HELPERS
        // =================================================================

        /// <summary>Box-filter downsample of the supersampled capture. Doing it here rather
        /// than leaving it to the importer keeps the edges clean at icon size.</summary>
        private static Color32[] Downsample(Color32[] source, int sourceSize, int targetSize)
        {
            if (sourceSize == targetSize)
                return source;

            int factor = sourceSize / targetSize;
            var result = new Color32[targetSize * targetSize];

            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;

                    for (int sy = 0; sy < factor; sy++)
                    {
                        int row = ((y * factor) + sy) * sourceSize;
                        for (int sx = 0; sx < factor; sx++)
                        {
                            Color32 c = source[row + (x * factor) + sx];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                        }
                    }

                    int n = factor * factor;
                    result[(y * targetSize) + x] = new Color32(
                        (byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }
            }

            return result;
        }

        /// <summary>
        /// Turns a black-background capture into straight alpha. Post-processing writes
        /// alpha as opaque, so coverage is taken from brightness instead — which also keeps
        /// bloom halos, since a halo is bright but has no geometry behind it. Colour is
        /// un-premultiplied afterwards, because compositing over black already darkened the
        /// half-transparent glow and the sprite shader would darken it again.
        /// </summary>
        private static Color32[] RebuildAlpha(Color32[] pixels)
        {
            var result = new Color32[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                float r = pixels[i].r / 255f;
                float g = pixels[i].g / 255f;
                float b = pixels[i].b / 255f;

                float alpha = Mathf.Clamp01(Mathf.Max(r, Mathf.Max(g, b)) * 1.15f);
                if (alpha <= 0.004f)
                {
                    result[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float inv = 1f / alpha;
                result[i] = new Color32(
                    (byte)(Mathf.Clamp01(r * inv) * 255f),
                    (byte)(Mathf.Clamp01(g * inv) * 255f),
                    (byte)(Mathf.Clamp01(b * inv) * 255f),
                    (byte)(alpha * 255f));
            }

            return result;
        }

        // =================================================================
        // IMPORT + WRITE-BACK
        // =================================================================

        private void ApplySpriteImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private void AssignSprites()
        {
            foreach (IconJob job in _jobs)
            {
                if (job.Prefab == null) continue;
                if (!_overwriteExisting && job.HasIconAlready) continue;

                string path = Path.Combine(_outputFolder, job.FileName + ".png").Replace('\\', '/');
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                var so = new SerializedObject(job.Turret);
                SerializedProperty prop = string.IsNullOrEmpty(job.SlotName)
                    ? so.FindProperty("Icon")
                    : so.FindProperty(job.SlotName)?.FindPropertyRelative("Icon");

                if (prop == null) continue;
                prop.objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(job.Turret);
            }
        }
    }
}
#endif
