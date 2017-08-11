using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class Event {
	public string eventName;
	public string displayName;
}

public class AutomationController : MonoBehaviour {

	public List<string> playersPressed = new List<string>();

	public bool addPlayersReady (string displayName, bool isServer) {

		if (playersPressed.Contains (displayName)) return false;

		playersPressed.Add (displayName);

		checkAudioStart (isServer);

		return true;

	}

	private void checkAudioStart (bool isServer) {
		
		Debug.Log("Player " + playersPressed.Count + " ready!");
		if (playersPressed.Count == 2) {

			Event e = new Event ();
			e.eventName = System.DateTime.Now.ToString () + " start";
			e.displayName = "Unet Server";

			this.GetComponent<AudioSource> ().Play();

		}

	}

}

