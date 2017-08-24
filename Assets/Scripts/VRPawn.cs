using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using RootMotion.FinalIK;

public class VRPawn : NetworkBehaviour {

    public Transform Head;
    public Transform LeftController;
    public Transform RightController;

    public bool isDazAvatar;
    public bool isMixamoAvatar;

    public Transform DazHead;
    public Transform DazHips;

    public Vector3 adjustmentHead = new Vector3(0, 0, 0);
    public Vector3 adjustmentHips = new Vector3(0, 0, 0);

	private Transform CameraRig;
	private Transform CameraRigHead;
	private Transform CameraRigLeft;
	private Transform CameraRigRight;

    private float dazHeightDiff;

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

        } else {

			if (isMixamoAvatar) {
				GetComponent<VRIK> ().solver.locomotion.weight = 0;
			}

            gameObject.name = "VRPawn (RemotePlayer)";
        }

        if (isServer && isLocalPlayer) {
            CameraRig.position = new Vector3(1, 0, 0);
            CameraRig.rotation = Quaternion.Euler(0, -90, 0);

        } else if (isLocalPlayer) {
            CameraRig.position = new Vector3(-1, 0, 0);
            CameraRig.rotation = Quaternion.Euler(0, 90, 0);

        }

	}

    void FollowPosition(Transform follower, Transform mover) {
        follower.position = mover.position;
    }

    void FollowRotation(Transform follower, Transform mover) {
        follower.rotation = mover.rotation;
    }

	void FollowObject (Transform follower, Transform mover) {
		if (mover.hasChanged) {
			mover.hasChanged = false;
            FollowPosition(follower, mover);
			FollowRotation(follower, mover);
		} 
	}

    void FollowDazObjects (Transform dazHips, Transform dazHead) {
        
        // dazHead.position = new Vector3(Head.position.x, Head.position.y, Head.position.z - .3f);
        FollowRotation(dazHead, CameraRigHead);
        dazHead.position = new Vector3(Head.position.x + adjustmentHead.x, dazHead.position.y + adjustmentHead.y, Head.position.z + adjustmentHead.z);
        dazHips.position = new Vector3(Head.position.x + adjustmentHips.x, dazHips.position.y + adjustmentHips.y, Head.position.z + adjustmentHips.z);

        
        // dazHips.rotation = Quaternion.Euler(0, dazHead.rotation.eulerAngles.y, 0);
    }


	void Update () {
		// This follows the CameraRig on a Transform -- if we teleport, we want the puppet to follow
		if (isLocalPlayer && CameraRig.hasChanged) {
			print("CameraRig Transform Change -- Updating the Puppet");
			CameraRig.hasChanged = false;
			this.transform.position = CameraRig.position;
            this.transform.rotation = CameraRig.rotation;
		}

		if (isLocalPlayer) {
			FollowObject (Head, CameraRigHead);
			FollowObject (LeftController, CameraRigLeft);
			FollowObject (RightController, CameraRigRight);
		}

        if (isDazAvatar) FollowDazObjects(DazHips, DazHead);
			
	}
		
}
