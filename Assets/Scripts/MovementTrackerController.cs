using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;


// Serializable classes that model JSON for the server

[System.Serializable]
public class RotationData {
	public float pitch;
	public float yaw;
	public float roll;
}

[System.Serializable]
public class PositionData {
	public float x;
	public float y;
	public float z;
}

[System.Serializable]
public class SensorData {
	public PositionData position = new PositionData();
	public RotationData rotation = new RotationData();
}

[System.Serializable]
public class Hands {
	public SensorData rightHand = new SensorData();
	public SensorData leftHand = new SensorData();
}

[System.Serializable]
public class CompleteFrameData {
	
	public string displayName;
	public Hands hands = new Hands();
	public SensorData head = new SensorData();

	public CompleteFrameData (string displayName) {
		this.displayName = displayName;
	}
}

public class MovementTrackerController : MonoBehaviour {

	private int UpdateThreshold = EnvVariables.MovementTrackerInterval;
	private int currentFrameCounter = 0;
	private string DisplayName = EnvVariables.DisplayName;
	private string BaseURI = EnvVariables.BaseURI;

	// private int startTime = System.DateTime.Now.Millisecond;

	void setPositionalData (SensorData sensor, SteamVR_Controller.Device controller) {
		sensor.position.x = controller.transform.pos.x;
		sensor.position.y = controller.transform.pos.y;
		sensor.position.z = controller.transform.pos.z;

		sensor.rotation.pitch = controller.transform.rot.eulerAngles.y;
		sensor.rotation.yaw   = controller.transform.rot.eulerAngles.z;
		sensor.rotation.roll  = controller.transform.rot.eulerAngles.x;
	}

	void setPositionalData (SensorData sensor, SteamVR_Camera head) {
		sensor.position.x = head.transform.position.x;
		sensor.position.y = head.transform.position.y;
		sensor.position.z = head.transform.position.z;

		sensor.rotation.pitch = head.transform.rotation.eulerAngles.y;
		sensor.rotation.yaw   = head.transform.rotation.eulerAngles.z;
		sensor.rotation.roll  = head.transform.rotation.eulerAngles.x;
	}
	
	// Update is called once per frame
	void Update () {
		
		currentFrameCounter++;

		if (!(currentFrameCounter > UpdateThreshold)) return;

		// Get index of right and left most controllers
		int rightIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
		int leftIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);

		if (rightIndex < 0 || leftIndex < 0) return;

		// Set 'em up to get the position
		SteamVR_Controller.Device rightDevice = SteamVR_Controller.Input(rightIndex);
		SteamVR_Controller.Device leftDevice = SteamVR_Controller.Input(leftIndex);
		SteamVR_Camera hmdDevice = GetComponentInChildren<SteamVR_Camera> (true);

		CompleteFrameData currentFrame = new CompleteFrameData (DisplayName);

		// Setting the transform and position for the right hand
		setPositionalData(currentFrame.hands.rightHand, rightDevice);
		setPositionalData(currentFrame.hands.leftHand, leftDevice);
		setPositionalData(currentFrame.head, hmdDevice);

		string json =  JsonUtility.ToJson(currentFrame);

		// print (startTime - System.DateTime.Now.Millisecond);
		// startTime = System.DateTime.Now.Millisecond;

		StartCoroutine (SendRequest (BaseURI + "api/action/create", json));

		currentFrameCounter = 0;

	}

    IEnumerator SendRequest(string url, string json) {

		UnityWebRequest request = new UnityWebRequest(url, "POST");
		byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
		request.uploadHandler = (UploadHandler) new UploadHandlerRaw(bodyRaw);
		request.downloadHandler = (DownloadHandler) new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");

		yield return request.Send();

		if (request.isNetworkError) {
			Debug.Log(request.error);
        } else {
			Debug.Log(request.responseCode);
        }
    }
		
}
