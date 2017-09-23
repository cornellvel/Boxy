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

    public float hipAdjustment = .1f;
    public float dazAvatarHeight = 1.78f;

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

            GameObject.Find("[CameraRig]").transform.GetChild(1).GetComponent<SteamVR_TrackedController>().TriggerClicked += ResetHeight;


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

    void ResetHeight(object sender, ClickedEventArgs e) {
        float theHeight = CameraRigHead.position.y;
        float percentage = (theHeight / dazAvatarHeight);
        this.transform.localScale = new Vector3(percentage, percentage, percentage);

        CmdAvatarScaleChange(this.transform.localScale);
    }

    [Command]
    public void CmdAvatarScaleChange(Vector3 newAvatarScale)
    {
        RpcAvatarScaleChange(newAvatarScale);
    }

    [ClientRpc]
    void RpcAvatarScaleChange(Vector3 newAvatarScale)
    {
        this.transform.localScale = newAvatarScale;
    }

    void FollowDazObjects (Transform dazHips, Transform dazHead) {
    
        /* 
            We need to offset the hips in the X or Z direction depending on the direction the avatar is facing
            otherwise the head goes --into-- the camerarig. Depending on the direction we face, we need to offset in
            either x or z. This is just a sinusoidal regression that fixes how much x we should offset or how much y we should offset
        */

        // graph these in desmos or email me if this doesn't make sense (me at oshaikh dot com)
        float x = -hipAdjustment * Mathf.Sin((Mathf.PI / 180) * (CameraRigHead.rotation.eulerAngles.y));
        float z = -hipAdjustment * Mathf.Sin((Mathf.PI / 180) * (CameraRigHead.rotation.eulerAngles.y + 90));

        FollowRotation(dazHead, CameraRigHead);
        dazHips.position = new Vector3(Head.position.x + x, dazHips.position.y, Head.position.z + z);
        dazHips.rotation = Quaternion.Euler(0, CameraRigHead.rotation.eulerAngles.y, 0);

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
            if (isDazAvatar) FollowDazObjects(DazHips, DazHead);
		}
			
	}
		
}
