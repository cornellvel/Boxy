using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using RootMotion.FinalIK;

public class VRPawn : NetworkBehaviour {

    public Transform Head;
    public Transform LeftController;
    public Transform RightController;

	public Transform FinalIKHead;
	public Transform FinalIKLeft;
	public Transform FinalIKRight;

	private Transform CameraRig;
	private Transform CameraRigHead;
	private Transform CameraRigLeft;
	private Transform CameraRigRight;

    void Start () {
        if (isLocalPlayer) { 
			
			CameraRig = GameObject.Find("[CameraRig]").transform;
			CameraRig.hasChanged = false;

			Head.GetComponentsInChildren<MeshRenderer>(true).ToList().ForEach(x => x.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On);

            gameObject.name = "VRPawn (LocalPlayer)";

			CameraRigHead = CameraRig.transform.GetChild (2);
			CameraRigLeft = CameraRig.transform.GetChild (1);
			CameraRigRight = CameraRig.transform.GetChild (0);

			CameraRigHead.hasChanged = false;
			CameraRigLeft.hasChanged = false;
			CameraRigRight.hasChanged = false;


			if (isRealAvatar()) {

				FinalIKHead.localPosition = new Vector3(0, 0, -.08f);
				FinalIKLeft.localPosition = new Vector3(0, 0.05f, -.08f);
				FinalIKRight.localPosition = new Vector3(0, 0.05f, -.08f);

			}

        } else {

			if (isRealAvatar ()) {
				GetComponent<VRIK> ().solver.locomotion.weight = 0;
			}

            gameObject.name = "VRPawn (RemotePlayer)";
        }
	}

	void FollowObject (Transform follower, Transform mover) {
		if (mover.hasChanged) {
			mover.hasChanged = false;
			follower.position = mover.position;
			follower.rotation = mover.rotation;
		} 
	}

	bool isRealAvatar () {
		return FinalIKHead && FinalIKLeft && FinalIKRight;
	}

	void Update () {
		// This follows the CameraRig on a Transform -- if we teleport, we want the puppet to follow
		if (isLocalPlayer && CameraRig.hasChanged) {
			print("CameraRig Transform Change -- Updating the Puppet");
			CameraRig.hasChanged = false;
			this.transform.position = CameraRig.position;
		}

		if (isLocalPlayer) {
			FollowObject (Head, CameraRigHead);
			FollowObject (LeftController, CameraRigLeft);
			FollowObject (RightController, CameraRigRight);
		}
			
	}
		
}
