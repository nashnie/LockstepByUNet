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
                NetworkServer.SendToAll(MsgType.AddPlayer + 1, integerMessage);
            }
            NetworkServer.SendToAll(MsgType.Ready + 1, new EmptyMessage());
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
        players = new Dictionary<string, NetworkConnection>(NumberOfPlayers);
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

        NetworkServer.RegisterHandler(LockstepMsgType.ReadyToStart, onReadyToStart);
        NetworkServer.RegisterHandler(LockstepMsgType.ConfirmReadyToStart, onReadyToStart);

        NetworkServer.RegisterHandler(LockstepMsgType.RecieveAction, onRecieveAction);
        NetworkServer.RegisterHandler(LockstepMsgType.ConfirmAction, onConfirmAction);
    }

    private void onConfirmAction(NetworkMessage netMsg)
    {
        NetworkServer.SendToAll(netMsg.msgType, netMsg.ReadMessage<RecieveActionMessage>());
    }

    private void onRecieveAction(NetworkMessage netMsg)
    {
        NetworkServer.SendToAll(netMsg.msgType, netMsg.ReadMessage<RecieveActionMessage>());
    }

    private void onReadyToStart(NetworkMessage netMsg)
    {
        NetworkServer.SendToAll(netMsg.msgType, new EmptyMessage());
    }

    void RegisterClientHandles()
    {
        client.RegisterHandler(MsgType.Highest + 1, OnReceivePassword);
        client.RegisterHandler(MsgType.AddPlayer + 1, OnAddPlayer);
        client.RegisterHandler(MsgType.Ready + 1, OnStartGame);

        client.RegisterHandler(LockstepMsgType.ReadyToStart, OnClientReadyToStart);
        client.RegisterHandler(LockstepMsgType.ConfirmReadyToStart, OnConfirmReadyToStart);

        client.RegisterHandler(LockstepMsgType.RecieveAction, OnClientRecieveAction);
        client.RegisterHandler(LockstepMsgType.ConfirmAction, OnClientConfirmAction);
    }

    private void OnClientConfirmAction(NetworkMessage netMsg)
    {
        RecieveActionMessage recieveActionMessage = netMsg.ReadMessage<RecieveActionMessage>();
        clientHudScript.ConfirmAction(recieveActionMessage.LockStepTurnID, netMsg.conn.connectionId.ToString());
    }

    private void OnClientRecieveAction(NetworkMessage netMsg)
    {
        RecieveActionMessage recieveActionMessage = netMsg.ReadMessage<RecieveActionMessage>();
        clientHudScript.RecieveAction(recieveActionMessage.LockStepTurnID, netMsg.conn.connectionId.ToString(), recieveActionMessage.value);
    }

    private void OnConfirmReadyToStart(NetworkMessage netMsg)
    {
        string netId = netMsg.ReadMessage<StringMessage>().value;
        clientHudScript.ConfirmReadyToStart(netMsg.conn.connectionId.ToString(), netId);
        Debug.Log("OnConfirmReadyToStart " + netMsg.conn.connectionId.ToString() + " netId " + netId);
    }

    private void OnClientReadyToStart(NetworkMessage netMsg)
    {
        clientHudScript.ReadyToStart(netMsg.conn.connectionId.ToString());
        Debug.Log("OnClientReadyToStart " + netMsg.conn.connectionId.ToString());
    }

    private void OnStartGame(NetworkMessage netMsg)
    {
        Debug.Log("OnStartGame...");
    }

    private void OnAddPlayer(NetworkMessage netMsg)
    {
        string netId = netMsg.ReadMessage<StringMessage>().value;
        players.Add(netId, null);
        Debug.Log("OnAddPlayer...netId " + netId);
    }
}

public class LockstepMsgType
{
    public const short ReadyToStart = 100;
    //public const short ConfirmReadyToStartServer = 101;
    public const short ConfirmReadyToStart = 102;
    public const short RecieveAction = 103; 
    public const short ConfirmAction = 104;
}