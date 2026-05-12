using UnityEngine;


namespace Photon.Chat.DemoChat.Utilities
{
    /// <summary>This component will destroy the Target GameObject (or the one it is attached to).</summary>
    public class DestroyOnStart : MonoBehaviour
    {
        [Tooltip("If null, this game object gets destroyed in this Start().")]
        public GameObject Target; // set in inspector

        void Start()
        { 
            if (this.Target == null)
            {
                this.Target = this.gameObject;
            }

            GameObject.Destroy(this.Target);
        }
    }
}