using UnityEngine;
using UnityEngine.UI;


namespace Photon.Chat.DemoChat
{
    [RequireComponent(typeof(ChatNewGui))]
    public class NamePickNewGui : MonoBehaviour
    {
        private const string UserNamePlayerPref = "NamePickUserName";

        public ChatNewGui chatNewComponent;

        public InputField idInput;

        public void Start()
        {
            InitChatNewComponent();

            string prefsName = PlayerPrefs.GetString(NamePickNewGui.UserNamePlayerPref);
            if (!string.IsNullOrEmpty(prefsName))
            {
                this.idInput.text = prefsName;
            }
        }


        // new UI will fire "EndEdit" event also when loosing focus. So check "enter" key and only then StartChat.
        public void EndEditOnEnter()
        {
            if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            {
                this.StartChat();
            }
        }

        public void StartChat()
        {
            if (!InitChatNewComponent())
            {
                return;
            }
            this.chatNewComponent.UserName = this.idInput.text.Trim();
            this.chatNewComponent.Connect();
            this.enabled = false;

            PlayerPrefs.SetString(NamePickNewGui.UserNamePlayerPref, this.chatNewComponent.UserName);
        }

        public bool InitChatNewComponent()
        {
            #if UNITY_6000_0_OR_NEWER
            this.chatNewComponent = GameObject.FindFirstObjectByType<ChatNewGui>();
            #else
            this.chatNewComponent = FindObjectOfType<ChatNewGui>();
            #endif

            return this.chatNewComponent != null;
        }
    }
}