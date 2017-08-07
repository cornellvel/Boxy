using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dissonance.Editor.Welcome
{
    [InitializeOnLoad]
    public class WelcomeLauncher
    {
        public const string CurrentDissonanceVersion = "2.0.0";

        private static readonly string StatePath = new[] {
            Application.dataPath,
            "Plugins",
            "Dissonance",
            "Editor",
            "Welcome",
            ".WelcomeState.json"
        }.Aggregate(Path.Combine);

        /// <summary>
        /// This method will run as soon as the editor is loaded (with Dissonance in the project)
        /// </summary>
        static WelcomeLauncher()
        {
            //Launching the window here caused some issues (presumably it's a bit too early for Unity to handle). Instead we'll wait until the first update call to do it.
            EditorApplication.update += Update;
        }

        // Add a menu item to launch the window
        [MenuItem("Window/Dissonance/Welcome Screen")]
        private static void LaunchInstaller()
        {
            //Clear installer state
            File.Delete(StatePath);

            //Next update will launch the window
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            var state = GetWelcomeState();

            if (!state.ShownForVersion.Equals(CurrentDissonanceVersion))
            {
                SetWelcomeState(new WelcomeState(CurrentDissonanceVersion));
                WelcomeWindow.ShowWindow(state);
            }

            // We only want to run this once, so unsubscribe from update now that it has run
            // ReSharper disable once DelegateSubtraction (Justification: I know what I'm doing... famous last words)
            EditorApplication.update -= Update;
        }

        internal static WelcomeState GetWelcomeState()
        {
            if (!File.Exists(StatePath))
            {
                // State path does not exist at all so create the default
                var state = new WelcomeState("");
                SetWelcomeState(state);
                return state;
            }
            else
            {
                //Read the state from the file
                using (var reader = File.OpenText(StatePath))
                    return JsonUtility.FromJson<WelcomeState>(reader.ReadToEnd());
            }
        }

        internal static void SetWelcomeState([CanBeNull]WelcomeState state)
        {
            if (state == null)
            {
                //Clear installer state
                File.Delete(StatePath);
            }
            else
            {
                using (var writer = File.CreateText(StatePath))
                    writer.Write(JsonUtility.ToJson(state));
            }
        }
    }

    [Serializable]
    internal class WelcomeState
    {
        [SerializeField] private string _shownForVersion;

        public string ShownForVersion
        {
            get { return _shownForVersion; }
        }

        public WelcomeState(string version)
        {
            _shownForVersion = version;
        }

        public override string ToString()
        {
            return _shownForVersion;
        }
    }
}