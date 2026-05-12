using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace Photon.Chat.DemoChat
{
    public class ChannelSelector : MonoBehaviour, IPointerClickHandler
    {
        public string Channel;

        public void SetChannel(string channel)
        {
            this.Channel = channel;
            Text t = GetComponentInChildren<Text>();
            t.text = this.Channel;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            #if UNITY_6000_0_OR_NEWER
            ChatNewGui handler = GameObject.FindFirstObjectByType<ChatNewGui>();
            #else
            ChatNewGui handler = FindObjectOfType<ChatNewGui>();
            #endif
            handler.ShowChannel(this.Channel);
        }
    }
}