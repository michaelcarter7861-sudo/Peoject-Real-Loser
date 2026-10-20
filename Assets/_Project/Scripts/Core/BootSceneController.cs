using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Boot Scene Controller (`BootSceneController.cs`).
    /// Applies saved audio/quality settings, enforces Landscape orientation, pre-warms audio,
    /// and transitions to `MainMenu`.
    /// </summary>
    public class BootSceneController : MonoBehaviour
    {
        [Header("Boot Configuration")]
        public float splashDelaySeconds = 0.5f;
        public string nextSceneName = "MainMenu";

        private float _elapsed;
        private bool _loadedNext;

        private void Awake()
        {
            SceneFlowControllers.ApplySavedBootSettings();
            RuntimeGLBAndMeshLoader.LoadWavAudioClip("sfx_ai_radio_checkin_beep");
        }

        private void Start()
        {
            if (splashDelaySeconds <= 0f)
            {
                LoadMainMenu();
            }
        }

        private void Update()
        {
            if (_loadedNext) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= splashDelaySeconds)
            {
                LoadMainMenu();
            }
        }

        public void LoadMainMenu()
        {
            if (_loadedNext) return;
            _loadedNext = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
