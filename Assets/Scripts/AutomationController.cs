using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class AutomationController {

	public static List<string> playersPressed = new List<string>();

	public static bool addPlayersReady (string displayName) {
		Debug.Log (playersPressed.Count);
		if (playersPressed.Contains (displayName))
			return false;

		playersPressed.Add (displayName);

		checkAudioStart ();

		return true;
	}

	private static void checkAudioStart () {
		Debug.Log (playersPressed.Count);
		if (playersPressed.Count == 2) {
			Debug.Log("FINALLY, DAMMIT");
			GameObject.Find ("InstructAudio").GetComponent<AudioSource> ().Play ();
		}
	}

}