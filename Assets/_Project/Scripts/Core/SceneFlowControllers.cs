using UnityEngine;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Shared scene-flow utilities for Boot and MainMenu initialization, persistent
    /// audio/graphics settings application, and Unity 2022.3 built-in font loading.
    /// </summary>
    public static class SceneFlowControllers
    {
        public const string MasterVolumePrefKey = "PRL_MasterVolume";
        public const string GraphicsQualityPrefKey = "PRL_GraphicsQuality";
        public const string LookSensitivityPrefKey = "PRL_LookSensitivity";

        public static void ApplySavedBootSettings()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("PRL_MasterVolume", AudioListener.volume));
            int savedQuality = PlayerPrefs.GetInt("PRL_GraphicsQuality", QualitySettings.GetQualityLevel());
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1), false);
            }

            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
        }

        public static Font GetSafeBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
        }
    }
}
