using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationController : NetworkBehaviour {

	private bool buttonPressed = false;

	private SteamVR_TrackedController controller;

	private SyncListString playersPressed = new SyncListString();

	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		controller = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		controller.TriggerClicked += HandleTriggerClicked;
		playersPressed.Callback = PlayersPressedChanged;

	}
		

	void HandleTriggerClicked (object sender, ClickedEventArgs e) {
		
		Debug.Log("trigger clicked");
		buttonPressed = true;


		if (!playersPressed.Contains (EnvVariables.DisplayName)) {
			playersPressed.Add (EnvVariables.DisplayName);
		}

	}

	void PlayersPressedChanged(SyncListString.Operation op, int itemIndex) {
		print ("clicked by " + playersPressed [itemIndex]);
		if (playersPressed.Count == 2 /* we're running two people */) {
			// code for playing audio goes here
			print ("start audio");
		}
	}

}
