using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class LocalNetworkManager : NetworkManager {

    private string AvatarType = EnvVariables.AvatarType;
	private string serverIP = EnvVariables.ServerIP;

	void Start () {

        networkAddress = serverIP;

		if (serverIP == "localhost") {
			StartHost();
		} else {
			StartClient();
		}
	}

    override public void OnClientDisconnect(NetworkConnection conn) {
        base.OnClientConnect(conn);

        StopClient();
        enabled = false;
        Invoke("ProcessDisconnect", 0.5f);
    }

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId) {

        GameObject player = Instantiate(Resources.Load("PlayerPrefabs/" + AvatarType), transform.position, Quaternion.identity) as GameObject;

        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
    }

    void ProcessDisconnect()
    {
		enabled = true;

    }

}
