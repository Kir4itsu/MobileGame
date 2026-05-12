using UnityEngine;


namespace Photon.Chat.DemoChat.Utilities
{
    /// <summary>This component will enable (setactive) the Target GameObject (or the one it is attached to).</summary>
    public class EnableOnStart : MonoBehaviour
    {
        [Tooltip("If null, this game object becomes active in this Start().")]
        public GameObject Target; // set in inspector

        void Start()
        {
            if (this.Target == null)
            {
                this.Target = this.gameObject;
            }

            this.Target.SetActive(true);
        }
    }
}