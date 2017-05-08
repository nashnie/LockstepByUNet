using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

public class ClientHUD : MonoBehaviour
{
    public GameObject connectToServer, disConnect, addressPanel, connecting, menuCam, disConnectMessage;
    public InputField portText, ipText, passwordText;
    public Text connectingText;

    private NetworkManager manager;
    private float connectingTimer, connectionFaileTimer;
    private bool connected;

    // Use this for initialization
    void Start()
    {
        if (!manager)
            manager = GetComponent<NetworkManager>();

        //checking if we have saved server info.
        if (PlayerPrefs.HasKey("nwPortC"))
        {
            manager.networkPort = Convert.ToInt32(PlayerPrefs.GetString("nwPortC"));
            portText.text = PlayerPrefs.GetString("nwPortC");
        }
        if (PlayerPrefs.HasKey("IPAddressC"))
        {
            manager.networkAddress = PlayerPrefs.GetString("IPAddressC");
            ipText.text = PlayerPrefs.GetString("IPAddressC");
        }

        enabled = false;

        Instance = this;
        //nv = GetComponent<NetworkView>();
        gameSetup = FindObjectOfType(typeof(CustomNetworkManager)) as CustomNetworkManager;

        gameSetup.OnGameStart += PrepGameStart;
    }

    void Update()
    {
        if (!connected)
        {
            //shows the failed to connect message after a certain time waiting to connect.
            if (connectingTimer > 0)
                connectingTimer -= Time.deltaTime;
            else
            {
                // manager.StopClient();
                connectingText.text = "Failed To Connect !!";
                if (connectionFaileTimer > 0)
                    connectionFaileTimer -= Time.deltaTime;
                else connecting.SetActive(false);
            }
        }

        //Basically same logic as FixedUpdate, but we can scale it by adjusting FrameLength
        AccumilatedTime = AccumilatedTime + Convert.ToInt32((Time.deltaTime * 1000)); //convert sec to milliseconds

        //in case the FPS is too slow, we may need to update the game multiple times a frame
        while (AccumilatedTime > GameFrameTurnLength)
        {
            GameFrameTurn();
            AccumilatedTime = AccumilatedTime - GameFrameTurnLength;
        }
    }

    public void ConnectToServer()
    {
        if (ipText.text != "" && portText.text != "")//is the information filled in ?.
        {
            connected = false;
            disConnectMessage.SetActive(false);
            connectingText.text = "Connecting !!";
            connecting.SetActive(true);
            connectingTimer = 8;//how long we try to connect until the fail message appears.
            connectionFaileTimer = 2;//how long the fail message is showing.
            manager.networkAddress = ipText.text;
            manager.networkPort = Convert.ToInt32(portText.text);
            PlayerPrefs.SetString("IPAddressC", ipText.text);//saving the filled in ip.
            PlayerPrefs.SetString("nwPortC", portText.text);//saving the filled in port.

            manager.StartClient();
        }
    }

    //called by the CustomNetworkManager.
    public void ConnectSuccses()
    {
        connected = true;
        connecting.SetActive(false);
        disConnect.SetActive(true);
        connectToServer.SetActive(false);
        addressPanel.SetActive(false);
        //menuCam.SetActive(false);   //if your player has a camera on him this one should be turned off when entering the game/lobby.
    }

    public void ButtonDisConnect()
    {
        DisConnect(false);
    }

    public void DisConnect(bool showMessage)
    {
        if (showMessage)
            disConnectMessage.SetActive(true);
        connectToServer.SetActive(true);
        disConnect.SetActive(false);
        addressPanel.SetActive(true);
        //menuCam.SetActive(true);  //turn the camera on again when returning to menu scene.
        manager.StopClient();
    }

    #region Public Variables
    public static readonly int FirstLockStepTurnID = 0;

    public static ClientHUD Instance;

    public int LockStepTurnID = FirstLockStepTurnID;

    public int numberOfPlayers;
    #endregion

    #region Private Variables
    private PendingActions pendingActions;
    private ConfirmedActions confirmedActions;

    private Queue<Action> actionsToSend;

    //private NetworkView nv;
    private CustomNetworkManager gameSetup;

    private List<string> readyPlayers;
    private List<string> playersConfirmedImReady;

    private bool initialized = false; //indicates if we are initialized and ready for game start

    //Variables for adjusting Lockstep and GameFrame length
    RollingAverage networkAverage;
    RollingAverage runtimeAverage;
    long currentGameFrameRuntime; //used to find the maximum gameframe runtime in the current lockstep turn
    private Stopwatch gameTurnSW;
    private int initialLockStepTurnLength = 200; //in Milliseconds
    private int initialGameFrameTurnLength = 50; //in Milliseconds
    private int LockstepTurnLength;
    private int GameFrameTurnLength;
    private int GameFramesPerLockstepTurn;
    private int LockstepsPerSecond;
    private int GameFramesPerSecond;

    private int playerIDToProcessFirst = 0; //used to rotate what player's action gets processed first

    private int GameFrame = 0; //Current Game Frame number in the currect lockstep turn
    private int AccumilatedTime = 0; //the accumilated time in Milliseconds that have passed since the last time GameFrame was called
    #endregion

    #region GameStart
    public void InitGameStartLists()
    {
        if (initialized) { return; }

        readyPlayers = new List<string>(numberOfPlayers);
        playersConfirmedImReady = new List<string>(numberOfPlayers);

        initialized = true;
    }

    public void PrepGameStart()
    {
        UnityEngine.Debug.Log("GameStart called. My PlayerID: " + Network.player.ToString());
        LockStepTurnID = FirstLockStepTurnID;
        numberOfPlayers = gameSetup.NumberOfPlayers;
        pendingActions = new PendingActions(this);
        confirmedActions = new ConfirmedActions(this);
        actionsToSend = new Queue<Action>();

        gameTurnSW = new Stopwatch();
        currentGameFrameRuntime = 0;
        networkAverage = new RollingAverage(numberOfPlayers, initialLockStepTurnLength);
        runtimeAverage = new RollingAverage(numberOfPlayers, initialGameFrameTurnLength);

        InitGameStartLists();

        //nv.RPC("ReadyToStart", RPCMode.AllBuffered, Network.player.ToString());
        manager.client.Send(LockstepMsgType.ReadyToStart, new EmptyMessage());
    }

    private void CheckGameStart()
    {
        if (playersConfirmedImReady == null)
        {
            UnityEngine.Debug.Log("WARNING!!! Unexpected null reference during game start. IsInit? " + initialized);
            return;
        }
        //check if all expected players confirmed our gamestart message
        if (playersConfirmedImReady.Count == numberOfPlayers)
        {
            //check if all expected players sent their gamestart message
            if (readyPlayers.Count == numberOfPlayers)
            {
                //we are ready to start
                UnityEngine.Debug.Log("All players are ready to start. Starting Game.");

                //we no longer need these lists
                playersConfirmedImReady = null;
                readyPlayers = null;

                GameStart();
            }
        }
    }

    private void GameStart()
    {
        //start the LockStep Turn loop
        enabled = true;
    }

    public void ReadyToStart(string playerID)
    {
        UnityEngine.Debug.Log("Player " + playerID + " is ready to start the game.");

        //make sure initialization has already happened -incase another player sends game start before we are ready to handle it
        InitGameStartLists();

        readyPlayers.Add(playerID);

        //nv.RPC("ConfirmReadyToStartServer", RPCMode.Server, Network.player.ToString() /*confirmingPlayerID*/, playerID /*confirmedPlayerID*/);
        //manager.client.Send(LockstepMsgType.ConfirmReadyToStartServer, new StringMessage(playerID));

        manager.client.Send(LockstepMsgType.ConfirmReadyToStart, new StringMessage(playerID));
        //Check if we can start the game
        CheckGameStart();
    }

    public void ConfirmReadyToStart(string confirmedPlayerID, string confirmingPlayerID)
    {
        UnityEngine.Debug.Log("Server Message: Player " + confirmingPlayerID + " is confirming Player " + confirmedPlayerID + " is ready to start the game.");

        //validate ID
        if (!gameSetup.players.ContainsKey(confirmingPlayerID))
        {
            //TODO: error handling
            UnityEngine.Debug.Log("Server Message: WARNING!!! Unrecognized confirming playerID: " + confirmingPlayerID);
            return;
        }
        if (!gameSetup.players.ContainsKey(confirmedPlayerID))
        {
            //TODO: error handling
            UnityEngine.Debug.Log("Server Message: WARNING!!! Unrecognized confirmed playerID: " + confirmingPlayerID);
        }

        if (!Network.player.ToString().Equals(confirmedPlayerID)) { return; }

        //UnityEngine.Debug.Log ("Player " + confirmingPlayerID + " confirmed I am ready to start the game.");
        playersConfirmedImReady.Add(confirmingPlayerID);

        //Check if we can start the game
        CheckGameStart();
    }
    #endregion

    #region Actions
    public void AddAction(Action action)
    {
        UnityEngine.Debug.Log("Action Added");
        if (!initialized)
        {
            UnityEngine.Debug.Log("Game has not started, action will be ignored.");
            return;
        }
        actionsToSend.Enqueue(action);
    }

    private bool LockStepTurn()
    {
        UnityEngine.Debug.Log("LockStepTurnID: " + LockStepTurnID);
        //Check if we can proceed with the next turn
        bool nextTurn = NextTurn();
        if (nextTurn)
        {
            SendPendingAction();
            //the first and second lockstep turn will not be ready to process yet
            if (LockStepTurnID >= FirstLockStepTurnID + 3)
            {
                ProcessActions();
            }
        }
        //otherwise wait another turn to recieve all input from all players

        UpdateGameFrameRate();
        return nextTurn;
    }

    /// <summary>
    /// Check if the conditions are met to proceed to the next turn.
    /// If they are it will make the appropriate updates. Otherwise 
    /// it will return false.
    /// </summary>
    private bool NextTurn()
    {
        /*UnityEngine.Debug.Log ("Next Turn Check: Current Turn - " + LockStepTurnID);
        UnityEngine.Debug.Log ("    priorConfirmedCount - " + confirmedActions.playersConfirmedPriorAction.Count);
        UnityEngine.Debug.Log ("    currentConfirmedCount - " + confirmedActions.playersConfirmedCurrentAction.Count);
        UnityEngine.Debug.Log ("    allPlayerCurrentActionsCount - " + pendingActions.CurrentActions.Count);
        UnityEngine.Debug.Log ("    allPlayerNextActionsCount - " + pendingActions.NextActions.Count);
        UnityEngine.Debug.Log ("    allPlayerNextNextActionsCount - " + pendingActions.NextNextActions.Count);
        UnityEngine.Debug.Log ("    allPlayerNextNextNextActionsCount - " + pendingActions.NextNextNextActions.Count);*/

        if (confirmedActions.ReadyForNextTurn())
        {
            if (pendingActions.ReadyForNextTurn())
            {
                //increment the turn ID
                LockStepTurnID++;
                //move the confirmed actions to next turn
                confirmedActions.NextTurn();
                //move the pending actions to this turn
                pendingActions.NextTurn();

                return true;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Have not recieved player(s) actions: ");
                foreach (int i in pendingActions.WhosNotReady())
                {
                    sb.Append(i + ", ");
                }
                UnityEngine.Debug.Log(sb.ToString());
            }
        }
        else
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Have not recieved confirmation from player(s): ");
            foreach (int i in pendingActions.WhosNotReady())
            {
                sb.Append(i + ", ");
            }
            UnityEngine.Debug.Log(sb.ToString());
        }

        return false;
    }

    private void SendPendingAction()
    {
        Action action = null;
        if (actionsToSend.Count > 0)
        {
            action = actionsToSend.Dequeue();
        }

        //if no action for this turn, send the NoAction action
        if (action == null)
        {
            action = new NoAction();
        }

        //action.NetworkAverage = Network.GetLastPing (Network.connections[0/*host player*/]);
        if (LockStepTurnID > FirstLockStepTurnID + 1)
        {
            action.NetworkAverage = confirmedActions.GetPriorTime();
        }
        else
        {
            action.NetworkAverage = initialLockStepTurnLength;
        }
        action.RuntimeAverage = Convert.ToInt32(currentGameFrameRuntime);
        //clear the current runtime average
        currentGameFrameRuntime = 0;

        //add action to our own list of actions to process
        pendingActions.AddAction(action, Convert.ToInt32(Network.player.ToString()), LockStepTurnID, LockStepTurnID);
        //start the confirmed action timer for network average
        confirmedActions.StartTimer();
        //confirm our own action
        confirmedActions.ConfirmAction(Convert.ToInt32(Network.player.ToString()), LockStepTurnID, LockStepTurnID);
        //send action to all other players
        //nv.RPC("RecieveAction", RPCMode.Others, LockStepTurnID, Network.player.ToString(), BinarySerialization.SerializeObjectToByteArray(action));

        RecieveActionMessage recieveActionMessage = new RecieveActionMessage();
        recieveActionMessage.LockStepTurnID = LockStepTurnID;
        recieveActionMessage.value = BinarySerialization.SerializeObjectToByteArray(action);
        manager.client.Send(LockstepMsgType.RecieveAction, recieveActionMessage);

        UnityEngine.Debug.Log("Sent " + (action.GetType().Name) + " action for turn " + LockStepTurnID);
    }

    private void ProcessActions()
    {
        //process action should be considered in runtime performance
        gameTurnSW.Start();

        //Rotate the order the player actions are processed so there is no advantage given to
        //any one player
        for (int i = playerIDToProcessFirst; i < pendingActions.CurrentActions.Length; i++)
        {
            pendingActions.CurrentActions[i].ProcessAction();
            runtimeAverage.Add(pendingActions.CurrentActions[i].RuntimeAverage, i);
            networkAverage.Add(pendingActions.CurrentActions[i].NetworkAverage, i);
        }

        for (int i = 0; i < playerIDToProcessFirst; i++)
        {
            pendingActions.CurrentActions[i].ProcessAction();
            runtimeAverage.Add(pendingActions.CurrentActions[i].RuntimeAverage, i);
            networkAverage.Add(pendingActions.CurrentActions[i].NetworkAverage, i);
        }

        playerIDToProcessFirst++;
        if (playerIDToProcessFirst >= pendingActions.CurrentActions.Length)
        {
            playerIDToProcessFirst = 0;
        }

        //finished processing actions for this turn, stop the stopwatch
        gameTurnSW.Stop();
    }

    public void RecieveAction(int lockStepTurn, string playerID, byte[] actionAsBytes)
    {
        //UnityEngine.Debug.Log ("Recieved Player " + playerID + "'s action for turn " + lockStepTurn + " on turn " + LockStepTurnID);
        Action action = BinarySerialization.DeserializeObject<Action>(actionAsBytes);
        if (action == null)
        {
            UnityEngine.Debug.Log("Sending action failed");
            //TODO: Error handle invalid actions recieve
        }
        else
        {
            pendingActions.AddAction(action, Convert.ToInt32(playerID), LockStepTurnID, lockStepTurn);

            //send confirmation
            //nv.RPC("ConfirmActionServer", RPCMode.Server, lockStepTurn, Network.player.ToString(), playerID);

            RecieveActionMessage recieveActionMessage = new RecieveActionMessage();
            recieveActionMessage.LockStepTurnID = lockStepTurn;
            recieveActionMessage.playerID = playerID;

            manager.client.Send(LockstepMsgType.ConfirmAction, recieveActionMessage);
        }
    }

    public void ConfirmAction(int lockStepTurn, string confirmingPlayerID)
    {
        confirmedActions.ConfirmAction(Convert.ToInt32(confirmingPlayerID), LockStepTurnID, lockStepTurn);
    }
    #endregion

    #region Game Frame
    private void UpdateGameFrameRate()
    {
        //UnityEngine.Debug.Log ("Runtime Average is " + runtimeAverage.GetMax ());
        //UnityEngine.Debug.Log ("Network Average is " + networkAverage.GetMax ());
        LockstepTurnLength = (networkAverage.GetMax() * 2/*two round trips*/) + 1/*minimum of 1 ms*/;
        GameFrameTurnLength = runtimeAverage.GetMax();

        //lockstep turn has to be at least as long as one game frame
        if (GameFrameTurnLength > LockstepTurnLength)
        {
            LockstepTurnLength = GameFrameTurnLength;
        }

        GameFramesPerLockstepTurn = LockstepTurnLength / GameFrameTurnLength;
        //if gameframe turn length does not evenly divide the lockstep turn, there is extra time left after the last
        //game frame. Add one to the game frame turn length so it will consume it and recalculate the Lockstep turn length
        if (LockstepTurnLength % GameFrameTurnLength > 0)
        {
            GameFrameTurnLength++;
            LockstepTurnLength = GameFramesPerLockstepTurn * GameFrameTurnLength;
        }

        LockstepsPerSecond = (1000 / LockstepTurnLength);
        if (LockstepsPerSecond == 0) { LockstepsPerSecond = 1; } //minimum per second

        GameFramesPerSecond = LockstepsPerSecond * GameFramesPerLockstepTurn;
    }

    private void GameFrameTurn()
    {
        //first frame is used to process actions
        if (GameFrame == 0)
        {
            if (!LockStepTurn())
            {
                //if the lockstep turn is not ready to advance, do not run the game turn
                return;
            }
        }

        //start the stop watch to determine game frame runtime performance
        gameTurnSW.Start();

        GameFrame++;
        if (GameFrame == GameFramesPerLockstepTurn)
        {
            GameFrame = 0;
        }

        //stop the stop watch, the gameframe turn is over
        gameTurnSW.Stop();
        //update only if it's larger - we will use the game frame that took the longest in this lockstep turn
        long runtime = Convert.ToInt32((Time.deltaTime * 1000))/*deltaTime is in secounds, convert to milliseconds*/ + gameTurnSW.ElapsedMilliseconds;
        if (runtime > currentGameFrameRuntime)
        {
            currentGameFrameRuntime = runtime;
        }
        //clear for the next frame
        gameTurnSW.Reset();
    }
    #endregion
}