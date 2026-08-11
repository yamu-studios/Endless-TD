// ============================================================================
// ETD.EditorTools - IconShotWindow.cs
// Put in: Assets/Scripts/Editor/IconShotWindow.cs
//
// One button: capture the Game View and drop the PNG into a chosen folder.
//
// Pose the turret in the shot scene, press Capture, repeat. Nothing here places
// prefabs, moves the camera or touches post-processing — what you see in the
// Game View is what lands on disk.
//
// Capture goes through ScreenCapture.CaptureScreenshot (same call the Steam
// Capture Tool uses) because it is the one path that reliably grabs the Game
// View from edit mode. That call writes on the next rendered frame, so the file
// is picked up and post-processed once it appears rather than immediately.
//
// Usage:  Tools > ETD > Art > Icon Shot
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ETD.EditorTools
{
    public class IconShotWindow : EditorWindow
    {
        private const string FolderKey = "ETD_IconShot_Folder";
        private const string NameKey = "ETD_IconShot_Name";
        private const string SupersizeKey = "ETD_IconShot_Supersize";
        private const string SquareKey = "ETD_IconShot_Square";
        private const string AlphaKey = "ETD_IconShot_Alpha";
        private const string SpriteKey = "ETD_IconShot_Sprite";

        private string _folder = "Assets/Art/Textures/Icons/Turrets";
        private string _fileName = "NewIcon";
        private int _supersize = 3;
        private bool _cropSquare = true;
        private bool _blackToAlpha = true;
        private bool _importAsSprite = true;

        private string _pendingTempPath;
        private string _pendingFinalPath;
        private double _pendingSince;
        private string _status;

        [MenuItem("Tools/ETD/Art/Icon Shot")]
        public static void Open()
        {
            GetWindow<IconShotWindow>("Icon Shot");
        }

        private void OnEnable()
        {
            _folder = EditorPrefs.GetString(FolderKey, _folder);
            _fileName = EditorPrefs.GetString(NameKey, _fileName);
            _supersize = EditorPrefs.GetInt(SupersizeKey, _supersize);
            _cropSquare = EditorPrefs.GetBool(SquareKey, _cropSquare);
            _blackToAlpha = EditorPrefs.GetBool(AlphaKey, _blackToAlpha);
            _importAsSprite = EditorPrefs.GetBool(SpriteKey, _importAsSprite);
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(FolderKey, _folder);
            EditorPrefs.SetString(NameKey, _fileName);
            EditorPrefs.SetInt(SupersizeKey, _supersize);
            EditorPrefs.SetBool(SquareKey, _cropSquare);
            EditorPrefs.SetBool(AlphaKey, _blackToAlpha);
            EditorPrefs.SetBool(SpriteKey, _importAsSprite);
            EditorApplication.update -= WaitForCapture;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Choose icon folder",
                        Directory.Exists(_folder) ? _folder : "Assets", "");

                    if (!string.IsNullOrWhiteSpace(picked))
                        _folder = ToProjectRelative(picked);
                }
            }

            _fileName = EditorGUILayout.TextField("File name", _fileName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
            _supersize = EditorGUILayout.IntSlider("Supersize", _supersize, 1, 4);
            _cropSquare = EditorGUILayout.Toggle("Crop to square", _cropSquare);
            _blackToAlpha = EditorGUILayout.Toggle("Black to alpha", _blackToAlpha);
            _importAsSprite = EditorGUILayout.Toggle("Import as sprite", _importAsSprite);

            if (_blackToAlpha)
            {
                EditorGUILayout.HelpBox(
                    "Turns the black background transparent using pixel brightness, so bloom " +
                    "halos survive instead of being clipped at the turret's edge. Needs a " +
                    "black background in the Game View.", MessageType.None);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_pendingTempPath != null))
            {
                if (GUILayout.Button("Capture Game View", GUILayout.Height(34)))
                    Capture();
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);

            EditorGUILayout.HelpBox(
                "The Game View must be open and visible — the capture lands on the next frame " +
                "it renders.", MessageType.None);
        }

        private void Capture()
        {
            if (string.IsNullOrWhiteSpace(_fileName))
            {
                _status = "Give the file a name first.";
                return;
            }

            if (!Directory.Exists(_folder))
                Directory.CreateDirectory(_folder);

            _pendingFinalPath = Path.Combine(_folder, _fileName + ".png").Replace('\\', '/');

            // Captured to a temp file first: CaptureScreenshot writes asynchronously, and a
            // half-written PNG inside Assets would be picked up by the importer mid-write.
            _pendingTempPath = Path.Combine(Path.GetTempPath(),
                "etd_iconshot_" + System.Guid.NewGuid().ToString("N") + ".png").Replace('\\', '/');

            ScreenCapture.CaptureScreenshot(_pendingTempPath, Mathf.Clamp(_supersize, 1, 4));

            _pendingSince = EditorApplication.timeSinceStartup;
            _status = "Waiting for the Game View to render a frame...";

            EditorApplication.update -= WaitForCapture;
            EditorApplication.update += WaitForCapture;
        }

        private void WaitForCapture()
        {
            if (_pendingTempPath == null)
            {
                EditorApplication.update -= WaitForCapture;
                return;
            }

            // Nudge the Game View, otherwise an unfocused editor may never render the frame
            // the capture is waiting on.
            EditorApplication.QueuePlayerLoopUpdate();

            if (!File.Exists(_pendingTempPath))
            {
                if (EditorApplication.timeSinceStartup - _pendingSince > 10d)
                {
                    _status = "Timed out. Is the Game View open and visible?";
                    _pendingTempPath = null;
                    EditorApplication.update -= WaitForCapture;
                    Repaint();
                }
                return;
            }

            // The file exists but may still be flushing; wait for the write to settle.
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(_pendingTempPath);
            }
            catch (IOException)
            {
                return;
            }

            if (bytes.Length == 0)
                return;

            EditorApplication.update -= WaitForCapture;
            Finish(bytes);
        }

        private void Finish(byte[] bytes)
        {
            string temp = _pendingTempPath;
            string final = _pendingFinalPath;
            _pendingTempPath = null;

            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!tex.LoadImage(bytes))
                {
                    _status = "Could not read the captured PNG.";
                    DestroyImmediate(tex);
                    return;
                }

                Texture2D output = tex;

                if (_cropSquare)
                {
                    output = CropCentreSquare(tex);
                    if (output != tex) DestroyImmediate(tex);
                }

                if (_blackToAlpha)
                    ApplyBlackToAlpha(output);

                File.WriteAllBytes(final, output.EncodeToPNG());
                DestroyImmediate(output);

                AssetDatabase.ImportAsset(final, ImportAssetOptions.ForceUpdate);
                if (_importAsSprite)
                    ApplySpriteImportSettings(final);

                _status = "Saved " + final;
                Debug.Log("[IconShot] " + _status,
                    AssetDatabase.LoadAssetAtPath<Texture2D>(final));

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(final));
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (IOException) { /* temp file, not worth failing the capture over */ }
                }

                Repaint();
            }
        }

        private static Texture2D CropCentreSquare(Texture2D source)
        {
            int side = Mathf.Min(source.width, source.height);
            if (side == source.width && side == source.height)
                return source;

            int x = (source.width - side) / 2;
            int y = (source.height - side) / 2;

            var result = new Texture2D(side, side, TextureFormat.RGBA32, false, false);
            result.SetPixels(source.GetPixels(x, y, side, side));
            result.Apply();
            return result;
        }

        /// <summary>
        /// Rebuilds alpha from brightness. Post-processing writes alpha as opaque, so a
        /// transparent clear colour never survives bloom; coverage is taken from the pixel
        /// instead. Colour is un-premultiplied afterwards, because compositing over black
        /// already darkened the half-transparent glow and the sprite shader would multiply
        /// by alpha and darken it a second time.
        /// </summary>
        private static void ApplyBlackToAlpha(Texture2D tex)
        {
            Color32[] pixels = tex.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                float r = pixels[i].r / 255f;
                float g = pixels[i].g / 255f;
                float b = pixels[i].b / 255f;

                float alpha = Mathf.Clamp01(Mathf.Max(r, Mathf.Max(g, b)) * 1.15f);
                if (alpha <= 0.004f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float inv = 1f / alpha;
                pixels[i] = new Color32(
                    (byte)(Mathf.Clamp01(r * inv) * 255f),
                    (byte)(Mathf.Clamp01(g * inv) * 255f),
                    (byte)(Mathf.Clamp01(b * inv) * 255f),
                    (byte)(alpha * 255f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
        }

        private static void ApplySpriteImportSettings(string path)
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

        private static string ToProjectRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string root = Application.dataPath.Replace('\\', '/');

            return absolute.StartsWith(root)
                ? "Assets" + absolute.Substring(root.Length)
                : absolute;
        }
    }
}
#endif
