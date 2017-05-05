using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.UI;
using System;

public class CustomNetworkManager : NetworkManager
{
    public Text clientsInfoText;
    public ClientHUD clientHudScript;
    public ServerHUD serverHudScript;

    private int connectedClients = 0;

    [HideInInspector]
    public string serverPassword;

    public delegate void NetworkManagerEvent();
    public NetworkManagerEvent OnConnectedToGame;
    public NetworkManagerEvent OnGameStart;
    public int NumberOfPlayers = 2;

    public Dictionary<string, NetworkConnection> players;

    //Server Side

    public override void OnStartServer()
    {
        base.OnStartServer();
        RegisterServerHandles();
        players = new Dictionary<string, NetworkConnection>(NumberOfPlayers);
        serverPassword = serverHudScript.passwordText.text;
        connectedClients = 0;
        clientsInfoText.text = "Connected Clients : " + connectedClients;

        Debug.Log("StartServer called");
    }

    //keeping track of Clients connecting.
    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);
        connectedClients += 1;
        clientsInfoText.text = "Connected Clients : " + connectedClients;

        //Sending password information to client.
        StringMessage msg = new StringMessage(serverPassword);
        NetworkServer.SendToClient(conn.connectionId, MsgType.Highest + 1, msg);

        Debug.Log("OnPlayerConnected, playerID:" + conn.connectionId.ToString());
        Debug.Log("Player Count : " + connectedClients);

        players.Add(conn.connectionId.ToString(), conn);
        if (connectedClients == NumberOfPlayers)
        {
            foreach(string connectionId in players.Keys)
            {
                StringMessage integerMessage = new StringMessage(connectionId);
                NetworkServer.SendToAll(MsgType.Ready + 1, integerMessage);
            }
        }
    }

    //keeping track of Clients disconnecting.
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);
        connectedClients -= 1;
        clientsInfoText.text = "Connected Clients : " + connectedClients;

        Debug.Log("Diconnected from the server:" + conn.connectionId.ToString());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
    }

    //Client Side
    public override void OnStartClient(NetworkClient client)
    {
        base.OnStartClient(client);
        RegisterClientHandles();
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);
        clientHudScript.ConnectSuccses();
    }

    //when client recieves password information from the server.
    public void OnReceivePassword(NetworkMessage netMsg)
    {
        //read the server password.
        var msg = netMsg.ReadMessage<StringMessage>().value;
        //serverPassword = msg;
        if (msg != clientHudScript.passwordText.text)
            clientHudScript.DisConnect(true);
    }

    public override void OnClientDisconnect(NetworkConnection conn)
    {
        base.OnClientDisconnect(conn);
        clientHudScript.DisConnect(false);
    }

    //Messages that need to be Registered on Server and Client Startup.
    void RegisterServerHandles()
    {
        NetworkServer.RegisterHandler(MsgType.Highest + 1, OnReceivePassword);
    }

    void RegisterClientHandles()
    {
        client.RegisterHandler(MsgType.Highest + 1, OnReceivePassword);
        client.RegisterHandler(MsgType.Ready + 1, OnReceiveReady);
    }

    private void OnReceiveReady(NetworkMessage netMsg)
    {
        string netId = netMsg.ReadMessage<StringMessage>().value;
        players.Add(netId, null);
    }
}
