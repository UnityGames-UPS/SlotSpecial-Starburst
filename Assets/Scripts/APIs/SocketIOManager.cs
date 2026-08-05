using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

public class SocketIOManager : MonoBehaviour
{
  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private UIManager uiManager;
  [SerializeField] private GameObject RaycastBlocker;
  internal GameData initialData = null;
  internal UiData initUIData = null;
  internal Root resultData = null;
  internal Player playerdata = null;
  [SerializeField] internal List<string> bonusdata = null;
  internal bool isResultdone = false;
  internal List<List<int>> LineData = null;
  private SocketManager manager;
  protected string SocketURI = null;
  // protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";
  protected string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
  [SerializeField]
  private string testToken;
  private Socket gameSocket;
  protected string nameSpace = "playground";
  [SerializeField] internal JSFunctCalls JSManager;
  protected string gameID = "SL-SB";
  // protected string gameID = "";
  internal bool isLoaded = false;

  internal bool SetInit = false;
  private bool exited = false;

  private const int maxReconnectionAttempts = 6;
  private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);

  private bool isConnected = false;
  private bool hasEverConnected = false;
  private const int MaxReconnectAttempts = 5;
  private const float ReconnectDelaySeconds = 2f;

  private float lastPongTime = 0f;
  private float pingInterval = 2f;
  private float pongTimeout = 3f;
  private bool waitingForPong = false;
  private int missedPongs = 0;
  private const int MaxMissedPongs = 5;
  private Coroutine PingRoutine;

  private bool hasFocus = true;
  private float focusLostTime = 0f;
  private Coroutine focusCheckRoutine;
  private float maxBackgroundTime = 60f;
  private bool isExiting = false;
  private bool isBeingDestroyed = false;

  private void Awake()
  {
    //Debug.unityLogger.logEnabled = false;
    isLoaded = false;
    SetInit = false;
  }

  private void Start()
  {
    OpenSocket();
  }

  void ReceiveAuthToken(string jsonData)
  {
    Debug.Log("Received data: " + jsonData);

    // Parse the JSON data
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);

    // Proceed with connecting to the server using myAuth and socketURL
    SocketURI = data.socketURL;
    myAuth = data.cookie;
    nameSpace = data.nameSpace;
  }

  string myAuth = null;

  private void OpenSocket()
  {
    // Create and setup SocketOptions
    SocketOptions options = new SocketOptions();
    options.AutoConnect = false;
    options.Reconnection = false;
    options.Timeout = TimeSpan.FromSeconds(3);
    options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("authToken");
        StartCoroutine(WaitForAuthToken(options));
#else
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = testToken
      };
    };
    options.Auth = authFunction;
    // Proceed with connecting to the server
    SetupSocketManager(options);
#endif
  }

  private IEnumerator WaitForAuthToken(SocketOptions options)
  {
    // Wait until myAuth is not null
    while (myAuth == null)
    {
      Debug.Log("My Auth is null");
      yield return null;
    }
    while (SocketURI == null)
    {
      Debug.Log("My Socket is null");
      yield return null;
    }
    Debug.Log("My Auth is not null");

    // Once myAuth is set, configure the authFunction
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = myAuth
      };
    };
    options.Auth = authFunction;

    Debug.Log("Auth function configured with token: " + myAuth);

    // Proceed with connecting to the server
    SetupSocketManager(options);
  }

  private void SetupSocketManager(SocketOptions options)
  {
    // Create and setup SocketManager
#if UNITY_EDITOR
    this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
    this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
    if (string.IsNullOrEmpty(nameSpace) | string.IsNullOrWhiteSpace(nameSpace))
    {
      gameSocket = this.manager.Socket;
    }
    else
    {
      Debug.Log("Namespace used :" + nameSpace);
      gameSocket = this.manager.GetSocket("/" + nameSpace);
    }
    // Set subscriptions
    gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
    gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
    gameSocket.On<string>("game:init", OnListenEvent);
    gameSocket.On<string>("result", OnListenEvent);
    gameSocket.On<bool>("socketState", OnSocketState);
    gameSocket.On<string>("internalError", OnSocketError);
    gameSocket.On<string>("alert", OnSocketAlert);
    gameSocket.On<string>("pong", OnPongReceived);
    gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice);
    gameSocket.On<string>("balance:sync", OnBalanceSync);

    manager.Open();
  }

  // Connected event handler implementation
  void OnConnected(ConnectResponse resp)
  {
    Debug.Log("✅ Connected to server.");

    if (hasEverConnected)
    {
      uiManager.CheckAndClosePopups();
    }

    isConnected = true;
    hasEverConnected = true;
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    SendPing();
  }

  private void OnDisconnected()
  {
    Debug.LogWarning("⚠️ Disconnected from server.");
    isConnected = false;
    ResetPingRoutine();
  }

  private void OnPongReceived(string data)
  {
    // Debug.Log("✅ Received pong from server.");
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    // Debug.Log($"⏱️ Updated last pong time: {lastPongTime}");
    // Debug.Log($"📦 Pong payload: {data}");
  }

  private void OnError(Error err)
  {
    Debug.LogError("[ERROR] Socket error: " + err);
    if (!string.IsNullOrEmpty(err.message) && err.message.Contains("Session expired"))
    {
      Debug.LogWarning("Session expired detected");
      OnDisconnected();
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("session_expired");
#endif
    }
    else
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
    }
  }

  private void OnListenEvent(string data)
  {
    ParseResponse(data);
  }

  private void OnBalanceSync(string data)
  {
    BalanceSyncPayload syncPayload = JsonConvert.DeserializeObject<BalanceSyncPayload>(data);
    if (syncPayload == null) return;

    if (playerdata == null) playerdata = new Player();
    playerdata.balance = syncPayload.balance;

    slotManager.UpdateBalanceDisplay(syncPayload.balance);
  }

  // Driven exclusively by the WebGL/JS OnFocusChanged path — never by OnApplicationFocus,
  // which isn't reliable enough inside a WebView to gate a network-affecting timer.
  internal void HandleFocusChange(bool focus)
  {
    hasFocus = focus;

    if (!focus)
    {
      focusLostTime = Time.time;
      if (focusCheckRoutine == null && !isExiting && !isBeingDestroyed)
        focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
    }
    else
    {
      if (focusCheckRoutine != null)
      {
        StopCoroutine(focusCheckRoutine);
        focusCheckRoutine = null;
      }
    }
  }

  private IEnumerator FocusTimeoutCheck()
  {
    while (!hasFocus && !isExiting && !isBeingDestroyed)
    {
      if (Time.time - focusLostTime >= maxBackgroundTime)
      {
        Debug.LogWarning("[SOCKET] Background timeout — closing connection");
        isConnected = false;
        ResetPingRoutine();

        if (manager != null)
        {
          try { manager.Close(); }
          catch (Exception e) { Debug.LogWarning($"[SOCKET] Focus close error: {e.Message}"); }
        }

        uiManager.DisconnectionPopup();
        focusCheckRoutine = null;
        yield break;
      }

      yield return new WaitForSecondsRealtime(1f);
    }

    focusCheckRoutine = null;
  }

  private void OnDestroy()
  {
    isBeingDestroyed = true;
  }

  private void OnSocketState(bool state)
  {
    Debug.Log("my state is " + state);
  }

  private void OnSocketError(string data)
  {
    Debug.Log("Received error with data: " + data);
  }

  private void OnSocketAlert(string data)
  {
    Debug.Log("Received alert with data: " + data);
  }

  private void OnSocketOtherDevice(string data)
  {
    Debug.Log("Received Device Error with data: " + data);
    uiManager.ADfunction();
  }

  private void SendPing()
  {
    ResetPingRoutine();
    PingRoutine = StartCoroutine(PingCheck());
  }

  void ResetPingRoutine()
  {
    if (PingRoutine != null)
    {
      StopCoroutine(PingRoutine);
    }
    PingRoutine = null;
  }

  private IEnumerator PingCheck()
  {
    while (true)
    {
      // Debug.Log($"🟡 PingCheck | waitingForPong: {waitingForPong}, missedPongs: {missedPongs}, timeSinceLastPong: {Time.time - lastPongTime}");

      if (missedPongs == 0)
      {
        uiManager.CheckAndClosePopups();
      }

      // If waiting for pong, and timeout passed
      if (waitingForPong)
      {
        if (missedPongs == 2)
        {
          uiManager.ReconnectionPopup();
        }
        missedPongs++;
        Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

        if (missedPongs >= MaxMissedPongs)
        {
          Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
          isConnected = false;
          uiManager.DisconnectionPopup();
          yield break;
        }
      }

      // Send next ping
      waitingForPong = true;
      lastPongTime = Time.time;
      // Debug.Log("📤 Sending ping...");
      SendDataWithNamespace("ping");
      yield return new WaitForSeconds(pingInterval);
    }
  }

  private void SendDataWithNamespace(string eventName, string json = null)
  {
    // Send the message
    if (gameSocket != null && gameSocket.IsOpen)
    {
      if (json != null)
      {
        gameSocket.Emit(eventName, json);
        Debug.Log("JSON data sent: " + json);
      }
      else
      {
        gameSocket.Emit(eventName);
      }
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
  }

  void CloseGame()
  {
    Debug.Log("Unity: Closing game");
    StartCoroutine(CloseSocket());
  }

  internal IEnumerator CloseSocket()
  {
    isExiting = true;
    RaycastBlocker.SetActive(true);
    ResetPingRoutine();

    Debug.Log("Closing Socket");

    manager?.Close();
    manager = null;

    Debug.Log("Waiting for socket to close");

    yield return new WaitForSeconds(0.5f);

    Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit");
#endif
  }

  private void ParseResponse(string jsonObject)
  {
    Debug.Log(jsonObject);
    Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);

    string id = myData.id;
    playerdata = myData.player;
    switch (id)
    {
      case "initData":
        {
          initialData = myData.gameData;
          initUIData = myData.uiData;
          if (!SetInit)
          {
            PopulateSlotSocket(ConvertListListIntToListString(initialData.lines));
            SetInit = true;
          }
          else
          {
            RefreshUI();
          }
          break;
        }
      case "ResultData":
        {
          resultData = myData;
          isResultdone = true;
          break;
        }
    }
  }

  private void RefreshUI()
  {
    uiManager.InitialiseUIData(initUIData.paylines);
  }

  private void PopulateSlotSocket(List<string> LineIds)
  {
    slotManager.ShuffleInitialMatrix();
    for (int i = 0; i < LineIds.Count; i++)
    {
      slotManager.FetchLines(LineIds[i], i);
    }

    slotManager.SetInitialUI();

    isLoaded = true;
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnEnter");
#endif
    RaycastBlocker.SetActive(false);

  }

  internal void AccumulateResult(int currBet)
  {
    isResultdone = false;
    isResultdone = false;
    MessageData message = new();
    message.type = "SPIN";
    message.payload.betIndex = currBet;

    // Serialize message data to JSON
    string json = JsonUtility.ToJson(message);
    SendDataWithNamespace("request", json);
  }

  private List<string> RemoveQuotes(List<string> stringList)
  {
    for (int i = 0; i < stringList.Count; i++)
    {
      stringList[i] = stringList[i].Replace("\"", ""); // Remove inverted commas
    }
    return stringList;
  }

  private List<string> ConvertListListIntToListString(List<List<int>> listOfLists)
  {
    List<string> resultList = new List<string>();

    foreach (List<int> innerList in listOfLists)
    {
      // Convert each integer in the inner list to string
      List<string> stringList = new List<string>();
      foreach (int number in innerList)
      {
        stringList.Add(number.ToString());
      }

      // Join the string representation of integers with ","
      string joinedString = string.Join(",", stringList.ToArray()).Trim();
      resultList.Add(joinedString);
    }

    return resultList;
  }

  private List<string> ConvertListOfListsToStrings(List<List<string>> inputList)
  {
    List<string> outputList = new List<string>();

    foreach (List<string> row in inputList)
    {
      string concatenatedString = string.Join(",", row);
      outputList.Add(concatenatedString);
    }

    return outputList;
  }

  private List<string> TransformAndRemoveRecurring(List<List<string>> originalList)
  {
    // Flattened list
    List<string> flattenedList = new List<string>();
    foreach (List<string> sublist in originalList)
    {
      flattenedList.AddRange(sublist);
    }

    // Remove recurring elements
    HashSet<string> uniqueElements = new HashSet<string>(flattenedList);

    // Transformed list
    List<string> transformedList = new List<string>();
    foreach (string element in uniqueElements)
    {
      transformedList.Add(element.Replace(",", ""));
    }

    return transformedList;
  }
}

[Serializable]
public class GameData
{
  public List<List<int>> lines { get; set; }
  public List<double> bets { get; set; }
  public List<int> spinBonus { get; set; }
}

public class StarBurstResponse
{
  public List<List<string>> matrix { get; set; }
  public Payload payload { get; set; }
}

[Serializable]
public class FreeSpin
{
  public int count { get; set; }
  public bool isFreeSpin { get; set; }
}

[Serializable]
public class MessageData
{
  public string type;
  public Data payload = new();
}

[Serializable]
public class Data
{
  public int betIndex;
  public string Event;
  public List<int> index;
  public int option;
}

[Serializable]
public class Root
{
  public List<List<string>> matrix { get; set; }
  public Payload payload { get; set; }
  public Features features { get; set; }

  public string id { get; set; }
  public GameData gameData { get; set; }
  public UiData uiData { get; set; }
  public Player player { get; set; }
}

public class Features
{
  public FreeSpin freeSpin { get; set; }
  public string totalWinAmount { get; set; }
  public List<StarBurstResponse> starBurstFreeSpins { get; set; }
}

[Serializable]
public class Payload
{
  public double winAmount { get; set; }
  public List<Win> wins { get; set; }
}

[Serializable]
public class Win
{
  public int line { get; set; }
  public List<int> positions { get; set; }
  public double amount { get; set; }
  public string direction { get; set; }
}

[Serializable]
public class UiData
{
  public Paylines paylines { get; set; }
}

[Serializable]
public class Paylines
{
  public List<Symbol> symbols { get; set; }
}

[Serializable]
public class Symbol
{
  public int id { get; set; }
  public string name { get; set; }
  public List<int> multiplier { get; set; }
  public string description { get; set; }
}

[Serializable]
public class Player
{
  public double balance { get; set; }
}

[Serializable]
public class BalanceSyncPayload
{
  public double balance;
}

[Serializable]
public class AuthTokenData
{
  public string cookie;
  public string socketURL;
  public string nameSpace;
}


