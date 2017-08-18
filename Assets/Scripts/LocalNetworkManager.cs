using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
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

    public override void OnClientConnect(NetworkConnection conn) {

        // Create message to set the player
        IntegerMessage msg = new IntegerMessage(spawnPrefabs.FindIndex(item => item.name == EnvVariables.AvatarType));


        // Call Add player and pass the message
        ClientScene.AddPlayer(conn, 0, msg);
    }

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId, NetworkReader extraMessageReader) {


        var stream = extraMessageReader.ReadMessage<IntegerMessage>();
        int selectedPlayer = stream.value;
        
        //Select the prefab from the spawnable objects list
        var playerPrefab = spawnPrefabs[selectedPlayer];

        // Create player object with prefab
        var player = Instantiate(playerPrefab) as GameObject;

        // Add player object for connection
        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);

    }

    void ProcessDisconnect()
    {
		enabled = true;

    }

}
