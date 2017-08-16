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

	private TextMesh TimerText;

	private float timeLeft = EnvVariables.duration;

    private AudioSource audioSource = new AudioSource();

	void Start () {

		TimerText = GameObject.Find("TimerText").GetComponent<TextMesh>();
        audioSource = GetComponent<AudioSource> ();
	}

	void Update () {
		
		if (timerStarted) {
			timeLeft -= Time.deltaTime;
            string minutes = Mathf.Floor(timeLeft / 60).ToString("00");
            string seconds = (timeLeft % 60).ToString("00");
			TimerText.text = minutes + ":" + seconds;

		} 

		if (timerStarted && timeLeft < 0) {
			
			timerStarted = false;
			timeLeft = EnvVariables.duration;
			TimerText.text = "0:00";

            audioSource.clip = Resources.Load<AudioClip>("Audio/end");
			playAudio(audioSource);

			sendEvent ("end");

		}

	}

	public bool addPlayersReady (string displayName, bool isServer) {

		if (playersPressed.Contains (displayName)) return false;

		playersPressed.Add (displayName);

		checkAudioStart (isServer);

		return true;

	}

	private void checkAudioStart (bool isServer) {
		
		Debug.Log("Player " + playersPressed.Count + " ready!");
		if (playersPressed.Count == 2) {

			isServerBehavoiur = isServer;
            audioSource.clip = Resources.Load<AudioClip>("Audio/instructions");
			playAudio(audioSource);

		}

	}

	private void playAudio (AudioSource audio) {
		float clipLength = audio.clip.length;
		audio.Play();
		StartCoroutine(StartMethod(clipLength));
	}

	private IEnumerator StartMethod(float clipLength) {
		yield return new WaitForSeconds(clipLength);

		timerStarted = true;
		sendEvent ("start");

	} 

	private void sendEvent (string status) {
		if (isServerBehavoiur) {
			Event e = new Event ();
			e.eventName = System.DateTime.Now.ToString() + " " + status;
			e.displayName = "UNet Server";

			StartCoroutine(WebRequests.SendRequest(EnvVariables.BaseURI + "api/event/create", JsonUtility.ToJson (e)));
		}
	}
}

