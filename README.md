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

## Creating Semi-customized Avatars
In order to create facially customized avatars, front facing and profile (either left or right side) photographs of the participants are required. The photographs should be taken in a passport style fashion without any facial obstruction (e.g. glasses, bangs, or piercings) to avoid items getting blended into the skin texture. When taking the photograph, there are several considerations to take into account: (1) the head should be centered without being tilted to either side of the body, (2) the lighting should be a white light in order to capture the participant’s true skin tone, (3) the lighting should be even without shadows or overexposure to one side, and (4) the camera should be held steadily in order to avoid blurriness or distortions. 



The photos were then imported into the commercial facial scanning software, FaceGen, where a 3D model of the face was generated. The exported face file was then imported into Daz3D Studio, and combined with a generic avatar body. The hairstyle, hair color, and skin tone were then personalized based on the participants’ photos. The avatar was then exported as an FBX file. 



The avatar FBX file was then imported into a Unity project file where the avatar’s skeleton was rigged using inverse kinematics approach to allow for natural movement of the upper body. The environment was then networked for dyadic collaboration, which allowed two participants to see other’s avatars and hear each other’s voices in virtual reality. Once in the virtual environment, the avatar’s height was adjusted to match the participants’ own height. 
