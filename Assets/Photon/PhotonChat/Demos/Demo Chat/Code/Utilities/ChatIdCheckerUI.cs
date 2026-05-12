using UnityEngine;


namespace Photon.Chat.DemoChat.Utilities
{
    /// <summary>
    /// This is used in the Editor Splash to properly inform the developer about the chat AppId requirement.
    /// </summary>
    [ExecuteInEditMode]
    public class ChatIdCheckerUI : MonoBehaviour
    {
        public GameObject VisibilityRoot; // must be set in inspector

        void Update()
        {
            if (this.VisibilityRoot != null)
            {
                this.VisibilityRoot.SetActive(string.IsNullOrEmpty(ChatSettings.Instance.AppId));
            }
        }
    }
}