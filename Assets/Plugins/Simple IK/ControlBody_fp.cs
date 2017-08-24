using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ControlBody_fp : NetworkBehaviour {


	private GameObject holdHeight;
	private float theHeight;

	private Vector3 old_pos;
	private Vector3 new_pos;

    private Transform head;
    private Transform hips;

	// Use this for initialization
	void Start ()
	{
		if (!isLocalPlayer) {
			//Renderer[] renderers = this.GetComponentsInChildren<Renderer>();
			//foreach (Renderer r in renderers) {r.enabled = false;}
			return;
		} else {

		}

		holdHeight = GameObject.Find ("hold_height");
		old_pos = holdHeight.transform.position;
		new_pos = old_pos;

		hips = this.transform.GetChild (0);
		hips.position = new_pos;

        head = this.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0);



    }

    void followPosition() {
		old_pos = new_pos;
		new_pos = holdHeight.transform.position;
		new_pos.y -= 0.3f;
		Vector3 change = new_pos - old_pos;

		Vector3 _tmp = holdHeight.transform.position;
		_tmp.x = (holdHeight.transform.position.x);
		_tmp.y = (holdHeight.transform.position.y) - .3f;
		_tmp.z = (holdHeight.transform.position.z);

		hips.position = hips.position + change; 
	}

	void followRotation()
	{
		Vector3 _tmp2 = holdHeight.transform.eulerAngles;
		_tmp2.x = 0f;
		_tmp2.y = holdHeight.transform.eulerAngles.y;
		_tmp2.z = 0f;

		hips.eulerAngles = _tmp2;
	}

	void followPositionHead()
	{
	    Vector3 _tmp = holdHeight.transform.position;
		_tmp.x = holdHeight.transform.position.x;
		_tmp.y = holdHeight.transform.position.y;
		_tmp.z = holdHeight.transform.position.z;

        head.position = _tmp;
	}

	void followRotationHead()
	{
		Vector3 _tmp2 = holdHeight.transform.eulerAngles;
		_tmp2.y = holdHeight.transform.eulerAngles.y;

		head.eulerAngles = _tmp2;

	}

	// Update is called once per frame
	void Update () {

		if (!isLocalPlayer) {
			return;
		} 

		followPosition ();
		followRotation ();

		followPositionHead ();
		followRotationHead ();
	}
}
