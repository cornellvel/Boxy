using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CubeSizeController : NetworkBehaviour {

	private int UpdateThreshold = EnvVariables.CubeSizeInterval;
	private int currentFrame = 0;

	private float newCubeSideLength (float distBetweenControllers) {
		// Simple Linear Regression
		return .267f * distBetweenControllers + .122f;
	}

	// Update is called once per frame
	void Update () {

		if (!isLocalPlayer) return;

		currentFrame++;

		if (!(currentFrame > UpdateThreshold)) return;

		// Get index of right and left most controllers
		int rightIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
		int leftIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);

		if (rightIndex < 0 || leftIndex < 0) return;

		// Set 'em up to get the position
		SteamVR_Controller.Device rightDevice = SteamVR_Controller.Input(rightIndex);
		SteamVR_Controller.Device leftDevice = SteamVR_Controller.Input(leftIndex);

		float distBetweenControllers = Vector3.Distance(rightDevice.transform.pos, leftDevice.transform.pos);
		float updatedSideLength = newCubeSideLength (distBetweenControllers);

		Vector3 newCubeScale = new Vector3(updatedSideLength, updatedSideLength, updatedSideLength);
		// Grab the cube child, and transform it here
		transform.GetChild(0).GetChild(0).transform.localScale = newCubeScale;

		CmdCubeScaleChange(newCubeScale);
		currentFrame = 0;

	}

	// Major WTF right here lol look at the networking docs
	// for some reason unity doesn't support transforming scale so you can use a SyncVar or Commands and RPC's
	// Check this out on the UNET docs. It's really really weird :)

	[Command]
	public void CmdCubeScaleChange(Vector3 newCubeScale) {
		RpcCubeScaleChange(newCubeScale);
	}

	[ClientRpc]
	void RpcCubeScaleChange(Vector3 newCubeScale) {
		transform.GetChild(0).GetChild(0).transform.localScale = newCubeScale;
	}

}
