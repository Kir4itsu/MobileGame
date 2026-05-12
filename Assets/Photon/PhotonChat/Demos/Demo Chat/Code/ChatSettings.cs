using System.IO;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Photon.Chat.DemoChat
{
    /// <summary>
    /// Utility class to provide an instance with the Chat AppId to use by the demo. Can use PUN PhotonServerSettings or work as standalone.
    /// </summary>
    /// <remarks>
    /// See the Instance getter for the different implementations (with and without PUN).
    /// </remarks>
    public class ChatSettings : ScriptableObject
    {
        [Tooltip("Your Chat AppId from Photon Dashboard")]
        public string AppId;

        [HideInInspector]
        public bool WizardDone;


        //  backing field for property
        private static ChatSettings instance;

        // provides access to instance of ChatSettings. file gets created if not available
        public static ChatSettings Instance
        {
            get
            {
                #if PUN_2_OR_NEWER
                if (instance == null)
                {
                    instance = ScriptableObject.CreateInstance<ChatSettings>();
                }

                instance.AppId = Photon.Pun.PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat;
                return instance;

                #else

                if (instance == null)
                {
                    instance = Load();
                }

                return instance;
                #endif
            }
        }


        // loads existing "ChatSettingsFile" of type ChatSettings from a Resources folder
        public static ChatSettings Load()
        {
            ChatSettings settings = (ChatSettings)Resources.Load("ChatSettingsFile", typeof(ChatSettings));
            if (settings != null)
            {
                return settings;
            }
            else
            {
                return Create();
            }
        }

        // creates an instance of ChatSettings and in Editor, stores it in a default path
        private static ChatSettings Create()
        {
            ChatSettings settings = (ChatSettings)ScriptableObject.CreateInstance("ChatSettings");
            #if UNITY_EDITOR
            if (!Directory.Exists("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.ImportAsset("Assets/Resources");
            }

            AssetDatabase.CreateAsset(settings, "Assets/Resources/ChatSettingsFile.asset");
            EditorUtility.SetDirty(settings);

            settings = (ChatSettings)Resources.Load("ChatSettingsFile", typeof(ChatSettings));
            #endif
            return settings;
        }
    }
}