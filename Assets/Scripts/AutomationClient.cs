using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationClient : NetworkBehaviour {

	private SteamVR_TrackedController controller;

	private SyncListString playersPressed = new SyncListString();

	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		controller = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		controller.TriggerClicked += HandleTriggerClicked;
		playersPressed.Callback = PlayersPressedChanged;

	}

	void addPlayerPressedItem (string displayName) {
		if (!playersPressed.Contains (displayName)) {
			playersPressed.Add (displayName);
		}
	}

	void HandleTriggerClicked (object sender, ClickedEventArgs e) {

		if (!isLocalPlayer)
			return;


		CmdPlayerPressed (EnvVariables.DisplayName);

	}

	void PlayersPressedChanged(SyncListString.Operation op, int itemIndex) {
		print ("clicked by " + playersPressed [itemIndex]);
		AutomationController.addPlayersReady (playersPressed[itemIndex]);
	}


	[Command]
	void CmdPlayerPressed(string displayName) {
		addPlayerPressedItem (displayName);
	}

}