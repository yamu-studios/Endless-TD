// ============================================================================
// ETD.EditorTools - SteamCaptureToolWindow.cs
// Put in: Assets/Scripts/Editor/SteamCaptureToolWindow.cs
//
// What it does:
// - Take Game View screenshot to a chosen folder
// - Take supersampled screenshots
// - Start/stop timed PNG frame-sequence capture for trailer/GIF footage
// - Open the output folder
//
// Recommended for Steam:
// - Screenshots: 1920x1080 minimum, use Supersize 2 if possible
// - Trailer raw capture: use PNG sequence capture, then encode externally
//   with DaVinci Resolve / Premiere / Shotcut / ffmpeg.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace ETD.EditorTools
{
    public class SteamCaptureToolWindow : EditorWindow
    {
        private const string OutputFolderKey = "ETD_SteamCapture_OutputFolder";
        private const string PrefixKey = "ETD_SteamCapture_Prefix";
        private const string SupersizeKey = "ETD_SteamCapture_Supersize";
        private const string FrameRateKey = "ETD_SteamCapture_FrameRate";
        private const string DurationKey = "ETD_SteamCapture_Duration";

        private string _outputFolder;
        private string _filePrefix;
        private int _supersize;
        private int _frameRate;
        private float _sequenceDuration;

        private bool _captureSequenceRunning;
        private EditorCoroutine _sequenceCoroutine;

        [MenuItem("Tools/ETD/Marketing/Steam Capture Tool")]
        public static void Open()
        {
            GetWindow<SteamCaptureToolWindow>("Steam Capture");
        }

        private void OnEnable()
        {
            _outputFolder = EditorPrefs.GetString(OutputFolderKey, Path.Combine(Application.dataPath, "../SteamCaptures"));
            _filePrefix = EditorPrefs.GetString(PrefixKey, "EndlessDefense");
            _supersize = EditorPrefs.GetInt(SupersizeKey, 1);
            _frameRate = EditorPrefs.GetInt(FrameRateKey, 60);
            _sequenceDuration = EditorPrefs.GetFloat(DurationKey, 8f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Steam Capture Tool", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Use Game View at 16:9. For Steam screenshots, capture at 1920x1080 or higher. " +
                "For trailer/GIF footage, capture a PNG sequence and encode it outside Unity.",
                MessageType.Info);

            DrawSettings();
            DrawScreenshotButtons();
            DrawSequenceCapture();
            DrawUtilities();

            SavePrefs();
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Folder", _outputFolder);

                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Choose Capture Output Folder", _outputFolder, "");
                    if (!string.IsNullOrWhiteSpace(selected))
                        _outputFolder = selected;
                }
            }

            _filePrefix = EditorGUILayout.TextField("File Prefix", _filePrefix);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Screenshot Settings", EditorStyles.boldLabel);

            _supersize = EditorGUILayout.IntSlider("Supersize", _supersize, 1, 4);
            EditorGUILayout.LabelField("Supersize 1 = Game View size. 2/4 = higher resolution.");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sequence Settings", EditorStyles.boldLabel);

            _frameRate = EditorGUILayout.IntSlider("Frame Rate", _frameRate, 24, 120);
            _sequenceDuration = EditorGUILayout.FloatField("Duration Seconds", _sequenceDuration);
            if (_sequenceDuration < 0.25f) _sequenceDuration = 0.25f;
        }

        private void DrawScreenshotButtons()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Screenshots", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture Game View PNG", GUILayout.Height(32)))
                    CaptureGameView();

                if (GUILayout.Button("Capture Current Showcase", GUILayout.Height(32)))
                    CaptureGameView("showcase");
            }

            EditorGUILayout.HelpBox(
                "For best Steam screenshots: apply one showcase scenario, wait for action/VFX, then click Capture Game View PNG.",
                MessageType.None);
        }

        private void DrawSequenceCapture()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Trailer / GIF Frame Sequence", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Records PNG frames from the Game View. It does not encode MP4 directly. " +
                "This is safer and higher quality for trailer editing.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (!_captureSequenceRunning)
                {
                    if (GUILayout.Button($"Start Frame Sequence ({_sequenceDuration:0.##}s @ {_frameRate}fps)", GUILayout.Height(34)))
                        StartSequenceCapture();
                }
                else
                {
                    if (GUILayout.Button("Stop Frame Sequence", GUILayout.Height(34)))
                        StopSequenceCapture();
                }
            }

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode before recording frame sequences.", MessageType.Warning);
        }

        private void DrawUtilities()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Output Folder"))
                    OpenOutputFolder();

                if (GUILayout.Button("Create Folder"))
                    EnsureOutputFolder();
            }

            if (GUILayout.Button("Print ffmpeg Command Example"))
                PrintFfmpegCommand();
        }

        private void CaptureGameView(string suffix = null)
        {
            EnsureOutputFolder();

            string fileName = BuildFileName(suffix ?? "gameview", "png");
            string path = Path.Combine(_outputFolder, fileName);

            ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, _supersize));

            Debug.Log($"[SteamCaptureTool] Game View screenshot requested: {path}");
            AssetDatabase.Refresh();
        }

        private void StartSequenceCapture()
        {
            EnsureOutputFolder();

            if (_captureSequenceRunning)
                return;

            _captureSequenceRunning = true;
            _sequenceCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(CaptureFrameSequence());
        }

        private void StopSequenceCapture()
        {
            _captureSequenceRunning = false;

            if (_sequenceCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            Debug.Log("[SteamCaptureTool] Frame sequence stopped.");
        }

        private IEnumerator CaptureFrameSequence()
        {
            string sequenceId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = Path.Combine(_outputFolder, $"{SanitizeFileName(_filePrefix)}_sequence_{sequenceId}");
            Directory.CreateDirectory(folder);

            int totalFrames = Mathf.CeilToInt(_sequenceDuration * _frameRate);
            float frameStep = 1f / Mathf.Max(1, _frameRate);

            Debug.Log($"[SteamCaptureTool] Starting frame sequence: {folder}");
            Debug.Log($"[SteamCaptureTool] Frames: {totalFrames}, FPS: {_frameRate}");

            for (int frame = 0; frame < totalFrames && _captureSequenceRunning; frame++)
            {
                string path = Path.Combine(folder, $"frame_{frame:D05}.png");
                ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, _supersize));

                double start = EditorApplication.timeSinceStartup;
                while (EditorApplication.timeSinceStartup - start < frameStep)
                    yield return null;
            }

            _captureSequenceRunning = false;
            _sequenceCoroutine = null;

            Debug.Log($"[SteamCaptureTool] Frame sequence finished: {folder}");
            Debug.Log($"[SteamCaptureTool] ffmpeg example: ffmpeg -framerate {_frameRate} -i \"{folder}/frame_%05d.png\" -c:v libx264 -crf 18 -pix_fmt yuv420p \"{folder}/{SanitizeFileName(_filePrefix)}_clip.mp4\"");

            AssetDatabase.Refresh();
        }

        private void EnsureOutputFolder()
        {
            if (string.IsNullOrWhiteSpace(_outputFolder))
                _outputFolder = Path.Combine(Application.dataPath, "../SteamCaptures");

            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);
        }

        private string BuildFileName(string suffix, string extension)
        {
            string time = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string cleanPrefix = SanitizeFileName(_filePrefix);
            string cleanSuffix = SanitizeFileName(suffix);
            return $"{cleanPrefix}_{cleanSuffix}_{time}.{extension}";
        }

        private string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "capture";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value.Trim();
        }

        private void OpenOutputFolder()
        {
            EnsureOutputFolder();
            EditorUtility.RevealInFinder(_outputFolder);
        }

        private void PrintFfmpegCommand()
        {
            string exampleFolder = Path.Combine(_outputFolder, $"{SanitizeFileName(_filePrefix)}_sequence_YYYYMMDD_HHMMSS");

            Debug.Log(
                "[SteamCaptureTool] ffmpeg example:\n" +
                $"ffmpeg -framerate {_frameRate} -i \"{exampleFolder}/frame_%05d.png\" -c:v libx264 -crf 18 -pix_fmt yuv420p \"{exampleFolder}/{SanitizeFileName(_filePrefix)}_clip.mp4\"");
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(OutputFolderKey, _outputFolder);
            EditorPrefs.SetString(PrefixKey, _filePrefix);
            EditorPrefs.SetInt(SupersizeKey, _supersize);
            EditorPrefs.SetInt(FrameRateKey, _frameRate);
            EditorPrefs.SetFloat(DurationKey, _sequenceDuration);
        }
    }
}
#endif
