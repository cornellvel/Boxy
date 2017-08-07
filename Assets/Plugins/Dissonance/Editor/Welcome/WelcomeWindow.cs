using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dissonance.Editor.Welcome
{
    internal class WelcomeWindow
        : EditorWindow
    {
        #region constants
        private const float WindowWidth = 300f;
        private const float WindowHeight = 290f;
        private const float WindowBorder = 1f;

        private static readonly Vector2 WindowSize = new Vector2(WindowWidth, WindowHeight);
        private static readonly Rect WindowRect = new Rect(Vector2.zero, WindowSize);
        private static readonly Rect BackgroundRect = new Rect(new Vector2(WindowBorder, WindowBorder), WindowSize - new Vector2(WindowBorder, WindowBorder) * 2);

        private static readonly Color BackgroundColor = new Color32(51, 51, 51, 255);

        private const string Title = "Welcome To Dissonance";
        #endregion

        #region construction
        public static void ShowWindow(WelcomeState state)
        {
            var window = GetWindow<WelcomeWindow>(true, Title, true);

            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window.titleContent = new GUIContent(Title);

            window.State = state;

            window.position = new Rect(150, 150, WindowWidth, WindowHeight);
            window.Repaint();
        }
        #endregion

        #region fields and properties
        public WelcomeState State { get; private set; }

        private Texture2D _logo;

        private bool _styleCreated;
        private GUIStyle _labelFieldStyle;
        #endregion

        public void Awake()
        {
            _logo = Resources.Load<Texture2D>("Dissonance_Large_Icon");
        }

        protected void OnGUI()
        {
            if (!_styleCreated)
            {
                CreateStyles();
                _styleCreated = true;
            }

            var bg = DrawBackground();
            using (new GUILayout.AreaScope(bg))
            {
                EditorGUI.DrawPreviewTexture(new Rect(0, 7, 300, 125), _logo);
                using (new GUILayout.AreaScope(new Rect(10, 142, bg.width - 20, bg.height - 152)))
                {
                    EditorGUILayout.LabelField("Thankyou for installing Dissonance Voice Chat!", _labelFieldStyle);
                    EditorGUILayout.LabelField(string.Format("Version {0}", WelcomeLauncher.CurrentDissonanceVersion), _labelFieldStyle);
                    EditorGUILayout.LabelField("", _labelFieldStyle);
                    EditorGUILayout.LabelField("Dissonance includes several optional integrations with other assets. Please visit the website to download and install them.", _labelFieldStyle);
                    EditorGUILayout.LabelField("", _labelFieldStyle);
                    if (GUILayout.Button("Open Integrations List"))
                        Application.OpenURL(string.Format("https://placeholder-software.co.uk/dissonance/releases/{0}.html{1}", WelcomeLauncher.CurrentDissonanceVersion, GetInstalledExtensionsString()));
                }
            }
        }

        private static string GetInstalledExtensionsString()
        {
            var extensions = IntegrationMetadata
                .FindIntegrations()
                .Select(a => string.Format("{0}={1}", a.Id, a.Version))
                .ToArray();

            return "?" + string.Join("&", extensions);
        }

        private void CreateStyles()
        {
            _labelFieldStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                normal = {
                    textColor = Color.white,
                },
            };
        }

        private static Rect DrawBackground()
        {
            var borderColor = EditorGUIUtility.isProSkin ? new Color(0.63f, 0.63f, 0.63f) : new Color(0.37f, 0.37f, 0.37f);
            EditorGUI.DrawRect(WindowRect, borderColor);

            EditorGUI.DrawRect(BackgroundRect, BackgroundColor);

            return BackgroundRect;
        }
    }
}