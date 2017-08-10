using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AutomationController : NetworkBehaviour {

	private bool buttonPressed = false;
	private bool audioStarted = false;

	private SteamVR_TrackedController controller;


	private void Start() {
		// controller = GetComponent<SteamVR_TrackedController>();
		controller = GameObject.Find("[CameraRig]").transform.GetChild(0).GetComponent<SteamVR_TrackedController>();
		controller.TriggerClicked += HandleTriggerClicked;

	}
		

	void HandleTriggerClicked (object sender, ClickedEventArgs e) {
		Debug.Log("trigger clicked");
		buttonPressed = true;

		if (!audioStarted) CmdButtonPressed (isServer);
		// networking
	}

	[Command]
	public void CmdButtonPressed(bool sentByServer) {
		RpcButtonPressed (sentByServer);
	}

	[ClientRpc]
	void RpcButtonPressed(bool sentByServer) {

		// ooh this is complicated
		if ((sentByServer && !isServer) || (!sentByServer && isServer) && buttonPressed && !audioStarted) {
			audioStarted = true;
			print ("starting audio!");
			CmdButtonPressed (!sentByServer);
		}

	}
		
	// client reception

}
