using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;

public class InternetManager : NetworkManager
{
    public bool CreateRoom = false;
    public string RoomName = "room1";
    
    public void Start () {

        StartMatchMaker();
        
        if (CreateRoom)
        {
            CreateMatch(RoomName);
        } else
        {
            matchMaker.ListMatches(0, 20, "", true, 0, 0, OnMatchList);
        }
       
    }

    void CreateMatch(string newMatchName)
    {
        matchName = newMatchName;
        matchMaker.CreateMatch(matchName, 2, true, "", "", "", 0, 0, OnMatchCreate);
    }

    void JoinMatch(MatchInfoSnapshot match)
    {
        matchName = match.name;
        matchSize = (uint)match.currentSize;
        matchMaker.JoinMatch(match.networkId, "", "","", 0, 0, OnMatchJoined);
    }

    override public void OnMatchList(bool success, string extendedInfo, List<MatchInfoSnapshot> matchList) {

        base.OnMatchList(success, extendedInfo, matchList);

        // auto join room if found
        foreach (MatchInfoSnapshot match in matchList)
        {
            if (match.name == RoomName)
            {
                JoinMatch(match);
                return;
            }
        }

    }


}
