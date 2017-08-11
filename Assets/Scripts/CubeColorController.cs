using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
class RChunk {
	public float R;
	public float distancePersonA;
	public float distancePerosnB;
	public int chunkNum;
}

[System.Serializable]
class RawRData {
	public int totalTime;
	public int chunkTime;
	public int completionTime;
	public RChunk[] data;
}

public class CubeColorController : NetworkBehaviour {

	[SyncVar]
	public Color m_color;

	private int UpdateThreshold = EnvVariables.CubeColorInterval;
	private int currentFrame = 0;

	// Update is called once per frame
	void Update () {

		currentFrame++;

		if (currentFrame < UpdateThreshold)
			return;

		currentFrame = 0;

		if (isServer) {
			StartCoroutine (GetRequest (EnvVariables.BaseURI + "api/compare/rawdata"));
		}


		transform.GetChild (0).GetChild (0).GetComponent<Renderer> ().material.color = m_color;

	}
		


	string ParamString (Dictionary <string, string> paramDict) {
		
		string paramStr = "?";
		foreach (KeyValuePair<string, string> entry in paramDict) {
			paramStr += entry.Key + "=" + entry.Value + "&";
		}
		return paramStr;

	}

	IEnumerator GetRequest(string url) {

		string paramStr = ParamString (new Dictionary<string, string> () {
			{ "username", EnvVariables.DisplayName },
			{ "comparator", EnvVariables.Comparator },
			{ "chunks", "1" },
			{ "cache", "true" }
		});

		using (UnityWebRequest www = UnityWebRequest.Get(url + paramStr))
		{
			yield return www.Send();

			if (www.isNetworkError) {
				if (EnvVariables.debug) Debug.Log(www.error);
			} else {
				
				RawRData response = JsonUtility.FromJson<RawRData> (www.downloadHandler.text);

				// This is because lerps are clamped between 0 and 1, so we need to limit the interval that t exists on
				float RValueLimited = .5f * response.data [0].R + .5f;

				if (RValueLimited < .5f) { 
					m_color = Color.Lerp (Color.red, Color.white, RValueLimited * 2f);
				} else {
					m_color = Color.Lerp (Color.white, Color.blue, (RValueLimited - .5f) * 2f);
				}

			}
		}
	}


}