using NUnit.Framework;
using Potshot;
using Potshot.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Potshot.Tests
{
    public class UiFactoryTests
    {
        [Test]
        public void MainMenuScene_HasAllWiredElements()
        {
            UIFactory.BuildMainMenuScene();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");

            var canvas = GameObject.Find("Canvas");
            Assert.That(canvas, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);

            foreach (var path in new[]
            {
                "NamePanel/NameField", "PlayOnlineButton", "HostButton",
                "JoinRow/AddressField", "JoinRow/JoinButton",
                "OfflineRow/OfflineButton", "OfflineRow/MapButton",
                "SettingsButton", "QuitButton",
                "SettingsPanel/FullscreenToggle", "SettingsPanel/VolumeSlider",
                "SettingsPanel/BackButton",
            })
                Assert.That(canvas.transform.Find(path), Is.Not.Null,
                    $"missing menu element: {path}");
        }

        [Test]
        public void PauseMenuPrefab_HasButtons()
        {
            UIFactory.CreatePauseMenuPrefab();
            var prefab = Resources.Load<GameObject>("UI/PauseMenu");
            Assert.That(prefab, Is.Not.Null);
            foreach (var path in new[]
                { "Panel/ResumeButton", "Panel/LeaveButton", "Panel/QuitButton" })
                Assert.That(prefab.transform.Find(path), Is.Not.Null, path);
        }
    }

    public class PlayerNameRulesTests
    {
        [Test]
        public void Sanitize_TrimsAndCaps() =>
            Assert.That(PlayerNameRules.Sanitize("  Brandon The Destroyer!! ", 5),
                Is.EqualTo("Brandon The Dest"));

        [Test]
        public void Sanitize_StripsControlChars() =>
            Assert.That(PlayerNameRules.Sanitize("Bran\ndon\t", 5), Is.EqualTo("Brandon"));

        [Test]
        public void Sanitize_EmptyFallsBack() =>
            Assert.That(PlayerNameRules.Sanitize("   ", 7), Is.EqualTo("Tanker7"));
    }
}
