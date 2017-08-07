using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class LocalNetworkManager : NetworkManager {

	public bool runAsServer = true;

	public string serverIP;

	void Start () {

		if (runAsServer) {
			SetupServer();
		} else {
			SetupClient();
		}
	}

	void SetupServer () {
        
        networkAddress = "localhost";
		StartHost();

	}

	void SetupClient () {
		networkAddress = serverIP;
		StartClient();
	}

    override public void OnClientDisconnect(NetworkConnection conn) {
        base.OnClientConnect(conn);

        StopClient();
        enabled = false;
        Invoke("ProcessDisconnect", 0.5f);
    }
    
    void ProcessDisconnect()
    {
		enabled = true;

    }

}
