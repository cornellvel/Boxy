# Boxy

Basic Unity project setup for multiplayer with HTC Vive.

Seperate scenes for Local network or Internet version.

## Local network version
Both PCs need to be on same subnet to find each other.

Make sure "Run as server" on LocalNetworkManager is only checked on one of the PCs.

If you're running as server, do not fill in the ServerIP on LocalNetworkManager

If you're running as a client, fill in the ServerIP of the computer running as server

## Internet version
Replace the project id with one of your own and enable multiplayer.

Set "Create Room" on InternetManager on one of the PCs and start that one first. 
