using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using RealLoserStudio.Tactical.Player;

namespace RealLoserStudio.Tactical.UI
{
    /// <summary>Unscaled game-over overlay wired to the player's centralized health receiver.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDeathScreen : MonoBehaviour
    {
        private PlayerDamageReceiver _health;
        private GameObject _root;
        private bool _shown;

        private void Awake()
        {
            _health = GetComponent<PlayerDamageReceiver>();
            if (_health == null) _health = GetComponentInParent<PlayerDamageReceiver>();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<PlayerDamageReceiver>();
            if (_health != null) _health.OnPlayerDied += Show;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnPlayerDied -= Show;
        }

        private void Show()
        {
            if (_shown) return;
            _shown = true;
            Time.timeScale = 0f;
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("PRL_GameOverEventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasObject = new GameObject("PRL_GameOverCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            _root = Panel(canvasObject.transform, "GameOverPanel", new Vector2(.22f, .15f), new Vector2(.78f, .85f), new Color(.025f, .035f, .04f, .98f));
            Label(_root.transform, "GameOverTitle", "MISSION FAILED", new Vector2(.05f, .70f), new Vector2(.95f, .91f), 48, new Color(1f, .32f, .24f));
            Label(_root.transform, "GameOverSubtitle", "OPERATOR DOWN — REDEPLOY OR RETURN TO COMMAND", new Vector2(.05f, .57f), new Vector2(.95f, .70f), 22, Color.white);
            MakeButton(_root.transform, "RetryMission", "RETRY MISSION", new Vector2(.18f, .35f), new Vector2(.82f, .49f), RestartMission);
            MakeButton(_root.transform, "ReturnToCommand", "RETURN TO COMMAND", new Vector2(.18f, .15f), new Vector2(.82f, .29f), ReturnToMenu);
        }

        private static void RestartMission()
        {
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && !string.IsNullOrEmpty(active.name)) SceneManager.LoadScene(active.name);
            else SceneManager.LoadScene("MainMenu");
        }

        private static void ReturnToMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
            else SceneManager.LoadScene(0);
        }

        private static GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static void Label(Transform parent, string name, string value, Vector2 min, Vector2 max, int size, Color color)
        {
            GameObject go = Panel(parent, name, min, max, new Color(0f, 0f, 0f, 0f));
            Text text = go.AddComponent<Text>();
            text.font = RealLoserStudio.Tactical.Core.SceneFlowControllers.GetSafeBuiltinFont();
            text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void MakeButton(Transform parent, string name, string caption, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            GameObject go = Panel(parent, name, min, max, new Color(.12f, .31f, .24f, 1f));
            Button button = go.AddComponent<Button>();
            button.onClick.AddListener(action);
            Label(go.transform, "Caption", caption, Vector2.zero, Vector2.one, 24, Color.white);
        }
    }
}
