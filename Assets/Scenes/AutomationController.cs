using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationController : NetworkBehaviour {

	private bool buttonPressed = false;

	private SteamVR_TrackedController controller;


	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		controller = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		controller.TriggerClicked += HandleTriggerClicked;

	}
		

	void HandleTriggerClicked (object sender, ClickedEventArgs e) {
		Debug.Log("trigger clicked");
		buttonPressed = true;

		CmdButtonPressed ();

		// networking
	}

	[Command]
	public void CmdButtonPressed() {
		RpcButtonPressed ();
	}

	[ClientRpc]
	void RpcButtonPressed() {
		if (buttonPressed) {
			print ("both buttons pressed");
		}
	}
		
	// client reception

}
