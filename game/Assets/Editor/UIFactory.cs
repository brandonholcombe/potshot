using System.IO;
using Potshot.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Potshot.EditorTools
{
    /// <summary>
    /// All UI is generated here (zero-GUI rule): the MainMenu scene and the
    /// PauseMenu prefab. Deliberately plain (dark flat panels, LegacyRuntime
    /// font) — the identity slice restyles. Canvases render via
    /// ScreenSpaceCamera so the QA screenshot rig can capture them.
    /// Run: scripts/unity.sh -quit -executeMethod Potshot.EditorTools.UIFactory.CreateAll
    /// </summary>
    public static class UIFactory
    {
        static Font UiFont =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static readonly Color PanelColor = new(0.10f, 0.11f, 0.13f, 0.95f);
        static readonly Color ButtonColor = new(0.20f, 0.22f, 0.27f, 1f);
        static readonly Color FieldColor = new(0.16f, 0.17f, 0.20f, 1f);
        static readonly Color TextColor = new(0.92f, 0.93f, 0.95f, 1f);

        public static void CreateAll()
        {
            BuildMainMenuScene();
            CreatePauseMenuPrefab();
            CreateLobbyOverlayPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIFactory] CreateAll done");
        }

        public static void CreateLobbyOverlayPrefab()
        {
            var rootGo = new GameObject("LobbyOverlay");
            try
            {
                var canvas = rootGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50; // under the pause menu (100)
                var scaler = rootGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                rootGo.AddComponent<GraphicRaycaster>();

                var roster = MakePanel(rootGo.transform, "RosterPanel",
                    new Vector2(-720f, 200f), new Vector2(380f, 420f));
                MakeText(roster, "RosterTitle", "LOBBY", 34,
                    new Vector2(0f, 175f), new Vector2(320f, 46f), FontStyle.Bold);
                var rosterText = MakeText(roster, "RosterText", "", 26,
                    new Vector2(0f, -20f), new Vector2(340f, 330f));
                rosterText.alignment = TextAnchor.UpperLeft;

                var leader = MakePanel(rootGo.transform, "LeaderPanel",
                    new Vector2(-720f, -160f), new Vector2(380f, 260f));
                MakeButton(leader, "MapButton", "Map: DevArena",
                    new Vector2(0f, 90f), new Vector2(330f, 52f), 22);
                MakeButton(leader, "BotsMinus", "-", new Vector2(-130f, 30f), new Vector2(52f, 46f));
                MakeText(leader, "BotsLabel", "Bots: 3", 24, new Vector2(0f, 30f), new Vector2(180f, 40f));
                MakeButton(leader, "BotsPlus", "+", new Vector2(130f, 30f), new Vector2(52f, 46f));
                MakeButton(leader, "TargetMinus", "-", new Vector2(-130f, -25f), new Vector2(52f, 46f));
                MakeText(leader, "TargetLabel", "First to 10", 24, new Vector2(0f, -25f), new Vector2(190f, 40f));
                MakeButton(leader, "TargetPlus", "+", new Vector2(130f, -25f), new Vector2(52f, 46f));
                var start = MakeButton(leader, "StartButton", "START",
                    new Vector2(0f, -92f), new Vector2(330f, 58f));
                start.targetGraphic.color = new Color(0.25f, 0.5f, 0.28f, 1f);

                var waiting = MakeText(rootGo.transform, "WaitingLabel",
                    "Waiting for the leader to start…", 28,
                    new Vector2(-720f, -120f), new Vector2(400f, 60f));
                waiting.color = new Color(0.8f, 0.82f, 0.86f, 0.9f);

                var countdown = MakeText(rootGo.transform, "CountdownLabel", "3", 200,
                    new Vector2(0f, 120f), new Vector2(500f, 240f), FontStyle.Bold);
                countdown.color = new Color(0.95f, 0.85f, 0.3f, 0.95f);

                var post = MakePanel(rootGo.transform, "PostMatchPanel",
                    Vector2.zero, new Vector2(560f, 520f));
                post.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.97f);
                var standings = MakeText(post, "StandingsText", "", 30,
                    new Vector2(0f, 0f), new Vector2(500f, 470f));
                standings.alignment = TextAnchor.UpperCenter;

                Directory.CreateDirectory("Assets/Resources/UI");
                PrefabUtility.SaveAsPrefabAsset(rootGo, "Assets/Resources/UI/LobbyOverlay.prefab");
                Debug.Log("[UIFactory] LobbyOverlay.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("MenuCamera").AddComponent<Camera>();
            cam.gameObject.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.orthographic = true;

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();

            var root = canvasGo.transform;

            MakeText(root, "Title", "POTSHOT", 96, new Vector2(0f, 420f),
                new Vector2(900f, 120f), FontStyle.Bold);

            var namePanel = MakePanel(root, "NamePanel",
                new Vector2(0f, 300f), new Vector2(520f, 70f));
            MakeText(namePanel, "NameLabel", "NAME", 28,
                new Vector2(-190f, 0f), new Vector2(120f, 50f));
            MakeInputField(namePanel, "NameField", "Tanker",
                new Vector2(70f, 0f), new Vector2(340f, 54f));

            MakeButton(root, "PlayOnlineButton", "PLAY ONLINE",
                new Vector2(0f, 190f), new Vector2(520f, 76f));
            MakeButton(root, "HostButton", "HOST",
                new Vector2(0f, 100f), new Vector2(520f, 76f));

            var joinRow = MakePanel(root, "JoinRow",
                new Vector2(0f, 10f), new Vector2(520f, 70f));
            MakeInputField(joinRow, "AddressField", "address",
                new Vector2(-90f, 0f), new Vector2(320f, 54f));
            MakeButton(joinRow, "JoinButton", "JOIN",
                new Vector2(165f, 0f), new Vector2(170f, 54f));

            var offlineRow = MakePanel(root, "OfflineRow",
                new Vector2(0f, -80f), new Vector2(520f, 70f));
            MakeButton(offlineRow, "OfflineButton", "OFFLINE VS BOTS",
                new Vector2(-95f, 0f), new Vector2(310f, 54f));
            MakeButton(offlineRow, "MapButton", "Map: DevArena",
                new Vector2(160f, 0f), new Vector2(180f, 54f), 20);

            MakeButton(root, "SettingsButton", "SETTINGS",
                new Vector2(0f, -170f), new Vector2(520f, 66f));
            MakeButton(root, "QuitButton", "QUIT",
                new Vector2(0f, -250f), new Vector2(520f, 66f));

            var settings = MakePanel(root, "SettingsPanel",
                Vector2.zero, new Vector2(640f, 420f));
            settings.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
            MakeText(settings, "SettingsTitle", "SETTINGS", 40,
                new Vector2(0f, 150f), new Vector2(400f, 60f), FontStyle.Bold);
            MakeToggle(settings, "FullscreenToggle", "Fullscreen",
                new Vector2(0f, 60f));
            MakeSlider(settings, "VolumeSlider",
                new Vector2(0f, -20f), new Vector2(420f, 30f));
            MakeText(settings, "VolumeLabel", "Volume", 24,
                new Vector2(0f, 20f), new Vector2(300f, 40f));
            MakeButton(settings, "BackButton", "BACK",
                new Vector2(0f, -140f), new Vector2(300f, 60f));
            settings.gameObject.SetActive(false); // starts hidden in the SAVED scene

            // Pre-join status panel (Brandon: show the room before joining).
            var status = MakePanel(root, "StatusPanel",
                Vector2.zero, new Vector2(640f, 320f));
            status.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
            MakeText(status, "StatusTitle", "POTSHOT.KODLOKI.IO", 32,
                new Vector2(0f, 110f), new Vector2(560f, 50f), FontStyle.Bold);
            MakeText(status, "StatusText", "Checking server…", 26,
                new Vector2(0f, 30f), new Vector2(560f, 90f));
            MakeButton(status, "ConfirmJoinButton", "JOIN",
                new Vector2(-120f, -100f), new Vector2(220f, 64f));
            MakeButton(status, "StatusBackButton", "BACK",
                new Vector2(120f, -100f), new Vector2(220f, 64f));
            status.gameObject.SetActive(false);

            canvasGo.AddComponent<MenuController>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
            Debug.Log("[UIFactory] MainMenu.unity saved");
        }

        public static void CreatePauseMenuPrefab()
        {
            var rootGo = new GameObject("PauseMenu");
            try
            {
                var canvas = rootGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = rootGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                rootGo.AddComponent<GraphicRaycaster>();

                var panel = MakePanel(rootGo.transform, "Panel",
                    Vector2.zero, new Vector2(460f, 360f));
                MakeText(panel, "PauseTitle", "PAUSED", 44,
                    new Vector2(0f, 120f), new Vector2(300f, 60f), FontStyle.Bold);
                MakeButton(panel, "ResumeButton", "RESUME",
                    new Vector2(0f, 30f), new Vector2(340f, 64f));
                MakeButton(panel, "LeaveButton", "LEAVE MATCH",
                    new Vector2(0f, -50f), new Vector2(340f, 64f));
                MakeButton(panel, "QuitButton", "QUIT GAME",
                    new Vector2(0f, -130f), new Vector2(340f, 64f));

                Directory.CreateDirectory("Assets/Resources/UI");
                PrefabUtility.SaveAsPrefabAsset(rootGo, "Assets/Resources/UI/PauseMenu.prefab");
                Debug.Log("[UIFactory] PauseMenu.prefab saved");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        // ── element helpers ─────────────────────────────────────────────

        static RectTransform Rect(GameObject go, Transform parent,
            Vector2 anchoredPos, Vector2 size)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        static Transform MakePanel(Transform parent, string name,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, size);
            go.AddComponent<Image>().color = PanelColor;
            return go.transform;
        }

        static Text MakeText(Transform parent, string name, string text,
            int size, Vector2 pos, Vector2 rectSize,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, rectSize);
            var t = go.AddComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = TextColor;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, int fontSize = 30)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, size);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            MakeText(go.transform, "Text", label, fontSize,
                Vector2.zero, size, FontStyle.Bold);
            return button;
        }

        static InputField MakeInputField(Transform parent, string name,
            string placeholder, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, size);
            go.AddComponent<Image>().color = FieldColor;
            var field = go.AddComponent<InputField>();

            var ph = MakeText(go.transform, "Placeholder", placeholder, 24,
                Vector2.zero, size - new Vector2(20f, 8f));
            ph.color = new Color(0.6f, 0.62f, 0.66f, 0.8f);
            ph.fontStyle = FontStyle.Italic;
            var text = MakeText(go.transform, "Text", "", 24,
                Vector2.zero, size - new Vector2(20f, 8f));
            text.supportRichText = false;

            field.targetGraphic = go.GetComponent<Image>();
            field.placeholder = ph;
            field.textComponent = text;
            return field;
        }

        static Toggle MakeToggle(Transform parent, string name, string label,
            Vector2 pos)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, new Vector2(420f, 44f));
            var toggle = go.AddComponent<Toggle>();

            var bg = new GameObject("Background");
            Rect(bg, go.transform, new Vector2(-180f, 0f), new Vector2(36f, 36f));
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = FieldColor;

            var check = new GameObject("Checkmark");
            Rect(check, bg.transform, Vector2.zero, new Vector2(24f, 24f));
            var checkImage = check.AddComponent<Image>();
            checkImage.color = new Color(0.55f, 0.85f, 0.45f, 1f);

            MakeText(go.transform, "Label", label, 26,
                new Vector2(30f, 0f), new Vector2(320f, 40f));

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            return toggle;
        }

        static Slider MakeSlider(Transform parent, string name,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            Rect(go, parent, pos, size);
            var slider = go.AddComponent<Slider>();

            var bg = new GameObject("Background");
            var bgRt = Rect(bg, go.transform, Vector2.zero, size);
            bg.AddComponent<Image>().color = FieldColor;

            var fillArea = new GameObject("FillArea");
            var fillAreaRt = Rect(fillArea, go.transform, Vector2.zero, size);
            var fill = new GameObject("Fill");
            var fillRt = Rect(fill, fillArea.transform, Vector2.zero, size);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.45f, 0.65f, 0.9f, 1f);

            var handleArea = new GameObject("HandleArea");
            Rect(handleArea, go.transform, Vector2.zero, size);
            var handle = new GameObject("Handle");
            Rect(handle, handleArea.transform, Vector2.zero, new Vector2(24f, size.y + 10f));
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = TextColor;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRt;
            slider.handleRect = handle.GetComponent<RectTransform>();
            // The slider stretches fill/handle by ANCHORS — a nonzero
            // sizeDelta ADDS on top and doubles the width.
            fillRt.sizeDelta = new Vector2(0f, size.y);
            slider.handleRect.sizeDelta = new Vector2(24f, size.y + 10f);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }
    }
}
