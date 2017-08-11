using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationClient : NetworkBehaviour {

	private SteamVR_TrackedController rightController;
	private SteamVR_TrackedController leftController;


	private SyncListString playersPressed = new SyncListString();

	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		rightController = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		leftController = GameObject.Find("[CameraRig]").transform.GetChild(1).GetComponent<SteamVR_TrackedController>();

		rightController.TriggerClicked += HandleTriggerClicked;
		leftController.TriggerClicked += HandleTriggerClicked;

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
		AutomationController.addPlayersReady (playersPressed[itemIndex]);
	}


	[Command]
	void CmdPlayerPressed(string displayName) {
		addPlayerPressedItem (displayName);
	}

}

