using UnityEngine;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-111: hands text to the Android share sheet (<c>Intent.ACTION_SEND</c>, <c>text/plain</c>,
    /// via <c>Intent.createChooser</c>). No file is written and no network call is made; the player
    /// picks the target. Compiled only for Android players; device behaviour UNVERIFIED (KI-002).
    /// </summary>
    public static class AndroidShareSheet
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        public static bool IsAvailable => true;

        public static bool ShareText(string text)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent", intentClass.GetStatic<string>("ACTION_SEND")))
                {
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "Simple Walk Game save").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share a copy of your save"))
                        activity.Call("startActivity", chooser);
                }
                return true;
            }
            catch (System.Exception) { return false; }
        }
#else
        public static bool IsAvailable => false;
        public static bool ShareText(string text) => false;
#endif
    }
}
