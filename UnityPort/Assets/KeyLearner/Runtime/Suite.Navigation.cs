using UnityEngine;

namespace KeyLearner.Unity
{
    public sealed partial class Suite
    {
        bool gameMenu, parentGate, listenerWasPaused;
        double parentHoldStarted = -1;
        int parentFinger = -1;
        static readonly Rect MenuRect = new Rect(1200, 20, 220, 88);
        static readonly Rect ParentHoldRect = new Rect(390, 365, 660, 130);
        bool TouchMode => services?.TouchPlay ?? Application.platform == RuntimePlatform.Android;
        public bool GameMenuOpen => gameMenu;

        static Vector2 NavigationPoint(Vector2 screen)
        {
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            return (new Vector2(screen.x, Screen.height - screen.y) -
                new Vector2((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2)) / scale;
        }
        public void OpenGameMenu()
        {
            if (!ready || studio || introduction?.Active == true || gameMenu) return;
            gameMenu = true;
            services.Keys = default;
            pointerInput.Reset();
            listenerWasPaused = AudioListener.pause;
            AudioListener.pause = true;
            services.Audio.SetMenuPaused(true);
            Time.timeScale = 0;
            DiagnosticState("game-menu-open");
        }
        void CloseGameMenu()
        {
            parentGate = false;
            parentHoldStarted = -1;
            parentFinger = -1;
            if (!gameMenu) return;
            gameMenu = false;
            AudioListener.pause = listenerWasPaused;
            services?.Audio?.SetMenuPaused(false);
            pointerInput.Reset();
            if (services != null) services.Keys = default;
            // Polling continues while paused, so releases are drained without resetting the round.
        }
        public void RestartCurrentGame()
        {
            if (game == null) return;
            game.ResetActivity();
            Select(services.Settings.Mode);
        }
        void DrawGameNavigation()
        {
            if (!gameMenu)
            {
                if (TouchMode) Ui.Button(MenuRect, picker ? "Parents" : "Menu", () => {
                    OpenGameMenu();
                    if (picker) parentGate = true;
                });
                return;
            }
            Ui.Panel(new Rect(0, 0, 1440, 900), new Color(.025f, .045f, .08f, .97f), 0);
            if (parentGate)
            {
                Ui.Label(new Rect(290, 185, 860, 100), "For grown-ups", 48, Color.white);
                Ui.Label(new Rect(290, 280, 860, 70), "Hold the button below for 3 seconds", 28, Color.white);
                Ui.Panel(ParentHoldRect, Style.Panel);
                Ui.Label(ParentHoldRect, "Hold for Parent Options", 32, Color.white);
                float progress = parentHoldStarted < 0 ? 0 : Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble - parentHoldStarted) / 3));
                Ui.Panel(new Rect(390, 510, 660 * progress, 12), Style.Mint, 0);
                Ui.Button(new Rect(490, 600, 460, 90), "Go back", () => {
                    parentGate = false;
                    parentHoldStarted = -1;
                    parentFinger = -1;
                    if (picker) CloseGameMenu();
                });
                return;
            }
            Ui.Label(new Rect(390, 100, 660, 100), "Take a little pause", 48, Color.white);
            Ui.Button(new Rect(390, 250, 660, 90), "Resume", CloseGameMenu);
            Ui.Button(new Rect(390, 365, 660, 90), "Choose Another Game", OpenPicker);
            Ui.Button(new Rect(390, 480, 660, 90), "Restart Current Game", RestartCurrentGame, game != null);
            Ui.Button(new Rect(390, 595, 660, 90), "Parent Options", () => parentGate = true);
        }
        void UpdateParentGate()
        {
            if (!gameMenu || !parentGate || !Application.isFocused) return;
            bool held = false;
            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began && ParentHoldRect.Contains(NavigationPoint(touch.position)))
                { parentFinger = touch.fingerId; parentHoldStarted = Time.realtimeSinceStartupAsDouble; }
                held = touch.fingerId == parentFinger && touch.phase != TouchPhase.Ended &&
                    touch.phase != TouchPhase.Canceled && ParentHoldRect.Contains(NavigationPoint(touch.position));
            }
            else if (Input.touchCount == 0 && parentFinger == -1)
            {
                bool inside = ParentHoldRect.Contains(NavigationPoint(Input.mousePosition));
                if (Input.GetMouseButtonDown(0) && inside) parentHoldStarted = Time.realtimeSinceStartupAsDouble;
                held = Input.GetMouseButton(0) && inside;
            }
            if (!held) { parentHoldStarted = -1; parentFinger = -1; return; }
            if (parentHoldStarted < 0 || Time.realtimeSinceStartupAsDouble - parentHoldStarted < 3) return;
            CloseGameMenu();
            OpenStudio();
        }
    }
}
