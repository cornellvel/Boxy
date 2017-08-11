using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationClient : NetworkBehaviour {

	private SteamVR_TrackedController controller;

	[SyncVar(hook="ButtonPressChanged")]
	private bool buttonPressed = false;


	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		controller = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		controller.TriggerClicked += HandleTriggerClicked;

	}


	void HandleTriggerClicked (object sender, ClickedEventArgs e) {

		if (!isLocalPlayer)
			return;
		
		CmdPlayerPressed ();

	}

	void ButtonPressChanged(bool newValue) {
		AutomationController.addPlayersReady (EnvVariables.DisplayName);
		buttonPressed = newValue;
	}


	[Command]
	void CmdPlayerPressed() {
		buttonPressed = true;
	}

}
