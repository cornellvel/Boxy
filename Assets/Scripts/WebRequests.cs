using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;


public class WebRequests {

	public static IEnumerator SendRequest(string url, string json) {

		UnityWebRequest request = new UnityWebRequest(url, "POST");
		byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
		request.uploadHandler = (UploadHandler) new UploadHandlerRaw(bodyRaw);
		request.downloadHandler = (DownloadHandler) new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");

		yield return request.Send();

		if (EnvVariables.debug) { 
			if (request.isNetworkError) {
				Debug.Log (request.error);
			} else {
				Debug.Log (request.responseCode);
			}
		}
	}

}

