using System.Linq;
using Dissonance.Config;
using UnityEditor;
using UnityEngine;

namespace Dissonance.Editor
{
    [CustomEditor(typeof (VoiceBroadcastTrigger))]
    public class VoiceBroadcastTriggerEditor : UnityEditor.Editor
    {
        private Texture2D _logo;
        private ChatRoomSettings _roomSettings;

        private readonly TokenControl _tokenEditor = new TokenControl("This broadcast trigger will only send voice if the local player has at least one of these access tokens");

        public void Awake()
        {
            _logo = Resources.Load<Texture2D>("dissonance_logo");
            _roomSettings = ChatRoomSettings.Load();
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label(_logo);

            var transmitter = (VoiceBroadcastTrigger) target;

            ChannelTypeGui(transmitter);

            EditorGUILayout.Space();
            PositionalAudioGui(transmitter);

            EditorGUILayout.Space();
            PriorityGui(transmitter);

            EditorGUILayout.Space();
            ActivationModeGui(transmitter);

            EditorGUILayout.Space();
            _tokenEditor.DrawInspectorGui(transmitter, transmitter);

            EditorGUILayout.Space();
            TriggerActivationGui(transmitter);

            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(target);
        }

        private void ChannelTypeGui(VoiceBroadcastTrigger transmitter)
        {
            transmitter.ChangeWithUndo(
                "Changed Dissonance Channel Type",
                (CommTriggerTarget)EditorGUILayout.EnumPopup("Channel Type", transmitter.ChannelType),
                transmitter.ChannelType,
                a => transmitter.ChannelType = a
            );

            if (transmitter.ChannelType == CommTriggerTarget.Player)
            {
                transmitter.ChangeWithUndo(
                    "Changed Dissonance Channel Transmitter Player Name",
                    EditorGUILayout.TextField("Recipient Player Name", transmitter.PlayerId),
                    transmitter.PlayerId,
                    a => transmitter.PlayerId = a
                );

                EditorGUILayout.HelpBox("Player mode sends voice data to the specified player.", MessageType.None);
            }

            if (transmitter.ChannelType == CommTriggerTarget.Room)
            {
                var roomNames = _roomSettings.Names;

                var haveRooms = roomNames.Count > 0;
                if (haveRooms)
                {
                    EditorGUILayout.BeginHorizontal();

                    var selectedIndex = string.IsNullOrEmpty(transmitter.RoomName) ? -1 : roomNames.IndexOf(transmitter.RoomName);
                    transmitter.ChangeWithUndo(
                        "Changed Dissonance Transmitter Room",
                        EditorGUILayout.Popup("Chat Room", selectedIndex, roomNames.ToArray()),
                        selectedIndex,
                        a => transmitter.RoomName = roomNames[a]
                    );

                    if (GUILayout.Button("Config Rooms"))
                        ChatRoomSettingsEditor.GoToSettings();

                    EditorGUILayout.EndHorizontal();

                    if (string.IsNullOrEmpty(transmitter.RoomName))
                        EditorGUILayout.HelpBox("Please select a chat room", MessageType.Warning);
                    else if (!roomNames.Contains(transmitter.RoomName))
                        EditorGUILayout.HelpBox(string.Format("Room '{0}' is no longer defined in the chat room configuration! \nRe-create the '{0}' room, or select a different room.", transmitter.RoomName), MessageType.Warning);
                }
                else
                {
                    if (GUILayout.Button("Create New Rooms"))
                        ChatRoomSettingsEditor.GoToSettings();
                }

                EditorGUILayout.HelpBox("Room mode sends voice data to all players in the specified room.", MessageType.None);

                if (!haveRooms)
                    EditorGUILayout.HelpBox("No rooms are defined. Click 'Create New Rooms' to configure chat rooms.", MessageType.Warning);
            }

            if (transmitter.ChannelType == CommTriggerTarget.Self)
            {
                EditorGUILayout.HelpBox(
                    "Self mode sends voice data to the DissonancePlayer attached to this game object.",
                    MessageType.None
                );

                var player = transmitter.GetComponent<IDissonancePlayer>();
                if (player == null)
                {
                    EditorGUILayout.HelpBox(
                        "This entity has no Dissonance player component!",
                        MessageType.Error
                    );
                }
                else if (Application.isPlaying && player.Type == NetworkPlayerType.Local)
                {
                    EditorGUILayout.HelpBox(
                        "This is the local player.\n" +
                        "Are you sure you mean to broadcast to the local player?",
                        MessageType.Warning
                    );
                }
            }
        }

        private static void PositionalAudioGui(VoiceBroadcastTrigger transmitter)
        {
            transmitter.ChangeWithUndo(
                "Changed Dissonance Positional Audio",
                EditorGUILayout.Toggle("Use Positional Data", transmitter.BroadcastPosition),
                transmitter.BroadcastPosition,
                a => transmitter.BroadcastPosition = a
            );

            EditorGUILayout.HelpBox(
                "Send audio on this channel with positional data to allow 3D playback if set up on the receiving end. There is no performance cost to enabling this.\n\n" +
                "Please see the Dissonance documentation for instructions on how to set your project up for playback of 3D voice comms.",
                MessageType.Info);
        }

        private static void PriorityGui(VoiceBroadcastTrigger transmitter)
        {
            transmitter.ChangeWithUndo(
                "Changed Dissonance Channel Priority",
                (ChannelPriority)EditorGUILayout.EnumPopup("Priority", transmitter.Priority),
                transmitter.Priority,
                a => transmitter.Priority = a
            );

            EditorGUILayout.HelpBox("Priority for the voice sent from this room. Voices will mute all lower priority voices on the receiver while they are speaking.\n\n" +
                                    "'None' means that this room specifies no particular priority and the priority of this player will be used instead", MessageType.Info);
        }

        private static void ActivationModeGui(VoiceBroadcastTrigger transmitter)
        {
            transmitter.ChangeWithUndo(
                "Changed Dissonance Activation Mode",
                (CommActivationMode)EditorGUILayout.EnumPopup("Activation Mode", transmitter.Mode),
                transmitter.Mode,
                a => transmitter.Mode = a
            );

            if (transmitter.Mode == CommActivationMode.None)
            {
                EditorGUILayout.HelpBox(
                    "While in this mode no voice will ever be transmitted",
                    MessageType.Info
                );
            }

            if (transmitter.Mode == CommActivationMode.PushToTalk)
            {
                transmitter.ChangeWithUndo(
                    "Changed Dissonance Push To Talk Axis",
                    EditorGUILayout.TextField("Input Axis Name", transmitter.InputName),
                    transmitter.InputName,
                    a => transmitter.InputName = a
                );

                EditorGUILayout.HelpBox(
                    "Define an input axis in Unity's input manager if you have not already.",
                    MessageType.Info
                );
            }
        }

        private static void TriggerActivationGui(VoiceBroadcastTrigger transmitter)
        {
            using (var toggle = new EditorGUILayout.ToggleGroupScope("Trigger Activation", transmitter.UseTrigger))
            {
                transmitter.ChangeWithUndo(
                    "Changed Dissonance Trigger Activation",
                    toggle.enabled,
                    transmitter.UseTrigger,
                    a => transmitter.UseTrigger = a
                );

                if (transmitter.UseTrigger)
                {
                    if (!transmitter.gameObject.GetComponents<Collider>().Any(c => c.isTrigger))
                        EditorGUILayout.HelpBox("Cannot find any collider triggers attached to this entity.", MessageType.Warning);
                    if (!transmitter.gameObject.GetComponents<Rigidbody>().Any() && !transmitter.gameObject.GetComponents<CharacterController>().Any())
                        EditorGUILayout.HelpBox("Cannot find either a RigidBody nor CharacterController attached to this entity (required for triggers to work).", MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    "Use trigger activation to only broadcast when the player is inside a trigger volume.",
                    MessageType.Info
                );
            }
        }
    }
}
