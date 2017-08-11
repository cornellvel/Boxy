﻿using System.Collections;
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

	private bool isServerBehavoiur;
	private bool timerStarted = false;

	private float duration = 5f * 60f;

	void Update () {
		
	}

	public bool addPlayersReady (string displayName, bool isServer) {

		if (playersPressed.Contains (displayName)) return false;

		playersPressed.Add (displayName);

		checkAudioStart (isServer);

		return true;

	}

	private void checkAudioStart (bool isServer) {
		
		Debug.Log("Player " + playersPressed.Count + " ready!");
		if (playersPressed.Count == 1) {

			isServerBehavoiur = isServer;

			playAudio(this.GetComponent<AudioSource> ());

		}

	}

	private void playAudio (AudioSource audio){
		float clipLength = audio.clip.length;
		audio.Play();
		StartCoroutine(StartMethod(clipLength));
	}

	private IEnumerator StartMethod(float clipLength)
	{
		yield return new WaitForSeconds(clipLength);

		timerStarted = true;

		if (isServerBehavoiur) {
			Event e = new Event ();
			e.eventName = System.DateTime.Now.ToString();
			e.displayName = "UNet Server";

			StartCoroutine(WebRequests.SendRequest(EnvVariables.BaseURI + "api/action/create", JsonUtility.ToJson (e)));
		}

	} 
}

