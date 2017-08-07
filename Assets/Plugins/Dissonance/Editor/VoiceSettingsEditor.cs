#if !NCRUNCH

using Dissonance.Audio.Capture;
using Dissonance.Config;
using UnityEditor;
using UnityEngine;

namespace Dissonance.Editor
{
    [CustomEditor(typeof(VoiceSettings))]
    public class VoiceSettingsEditor : UnityEditor.Editor
    {
        private Texture2D _logo;

        //private bool _showPreprocessor;
        //private bool _showQuality;

        public void Awake()
        {
            _logo = Resources.Load<Texture2D>("dissonance_logo");
        }

        public override void OnInspectorGUI()
        {
            var settings = (VoiceSettings)target;

            GUILayout.Label(_logo);

            DrawQualitySettings(settings);
            EditorGUILayout.Space();
            DrawPreprocessorSettings(settings);

            if (GUI.changed)
                EditorUtility.SetDirty(settings);
        }

        private void DrawPreprocessorSettings(VoiceSettings settings)
        {
            settings.DenoiseAmount = (NoiseSuppressionLevels)EditorGUILayout.EnumPopup(new GUIContent("Noise Suppression"), settings.DenoiseAmount);
            EditorGUILayout.HelpBox("A higher value will remove more background noise but risks attenuating speech.\n\n" +
                                    "A lower value will remove less noise, but will attenuate speech less.", MessageType.Info);

            //settings.Denoise = EditorGUILayout.Toggle("De-Noise", settings.Denoise);
            //EditorGUI.indentLevel++;
            //using (new EditorGUI.DisabledGroupScope(!settings.Denoise))
            //{
            //    settings.DenoiseMaxAttenuation = -(int)EditorGUILayout.Slider("Max Attenuation (dB)", -settings.DenoiseMaxAttenuation, 0, 100);
            //}
            //EditorGUI.indentLevel--;

            //EditorGUILayout.Space();

            //settings.AGC = EditorGUILayout.Toggle("Automatic Gain Control", settings.AGC);
            //EditorGUI.indentLevel++;
            //using (new EditorGUI.DisabledGroupScope(!settings.AGC))
            //{
            //    settings.AgcTargetLevel = EditorGUILayout.Slider("Target", settings.AgcTargetLevel / 32768 * 100, 1, 100) * 32768 / 100;
            //    settings.AgcMaxGain = (int)EditorGUILayout.Slider("Max Gain (dB)", settings.AgcMaxGain, 1, 100);
            //    settings.AgcGainIncrement = (int)EditorGUILayout.Slider("Gain Increment (dB/s)", settings.AgcGainIncrement, 1, 100);
            //    settings.AgcGainDecrement = -(int)EditorGUILayout.Slider("Gain Decrement (dB/s)", -settings.AgcGainDecrement, 1, 100);
            //}
            //EditorGUI.indentLevel--;
        }

        private void DrawQualitySettings(VoiceSettings settings)
        {
            using (new EditorGUI.DisabledGroupScope(Application.isPlaying))
            {
                EditorGUILayout.Space();

                var f = (FrameSize)EditorGUILayout.EnumPopup("Frame Size", settings.FrameSize);
                if (!Application.isPlaying)
                    settings.FrameSize = f;
                EditorGUILayout.HelpBox(
                    "A smaller frame size will send smaller packets of data more frequently, improving latency at the expense of some network and CPU performance.\n\n" +
                    "A larger frame size will send larger packets of data less frequently, gaining some network and CPU performance at the expense of latency.",
                    MessageType.Info);

                var q = (AudioQuality)EditorGUILayout.EnumPopup("Audio Quality", settings.Quality);
                if (!Application.isPlaying)
                    settings.Quality = q;
                EditorGUILayout.HelpBox(
                    "A lower quality setting uses less CPU and bandwidth, but sounds worse.\n\n" +
                    "A higher quality setting uses more CPU and bandwidth, but sounds better.",
                    MessageType.Info);

                if (Application.isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "Quality settings cannot be changed at runtime",
                        MessageType.Warning);
                }
            }
        }

        public static void GoToSettings()
        {
            var logSettings = LoadVoiceSettings();
            EditorApplication.delayCall += () =>
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = logSettings;
            };
        }

        private static VoiceSettings LoadVoiceSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VoiceSettings>(VoiceSettings.SettingsFilePath);
            if (asset == null)
            {
                asset = CreateInstance<VoiceSettings>();
                AssetDatabase.CreateAsset(asset, VoiceSettings.SettingsFilePath);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }
    }
}
#endif