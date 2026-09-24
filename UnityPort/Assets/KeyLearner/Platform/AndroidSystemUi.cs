using UnityEngine;

namespace KeyLearner.Unity.Platform
{
    internal static class AndroidSystemUi
    {
        public static void Apply()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
                    // Acquire JNI references on the UI thread; do not capture a
                    // reference that the caller has already disposed.
                    using (var currentPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var current = currentPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = current.Call<AndroidJavaObject>("getWindow"))
                    using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        // Android 10 uses legacy immersive-sticky flags. Setting
                        // them also prevents API 30's first touch from merely
                        // revealing navigation instead of reaching the game.
                        const int immersiveSticky = 0x1000;
                        const int layoutStable = 0x100;
                        const int layoutHideNavigation = 0x200;
                        const int layoutFullscreen = 0x400;
                        const int hideNavigation = 0x2;
                        const int fullscreen = 0x4;
                        decor.Call("setSystemUiVisibility", immersiveSticky | layoutStable |
                            layoutHideNavigation | layoutFullscreen | hideNavigation | fullscreen);
                        if (version.GetStatic<int>("SDK_INT") >= 30)
                        {
                            using (var controller = window.Call<AndroidJavaObject>("getInsetsController"))
                            {
                                if (controller != null)
                                {
                                    controller.Call("setSystemBarsBehavior", 2); // BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                                    controller.Call("hide", 3); // statusBars() | navigationBars()
                                }
                            }
                        }
                    }
                }));
#endif
        }
    }
}
