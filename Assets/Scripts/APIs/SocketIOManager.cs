using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;

public class SocketIOManager : MonoBehaviour
{
  [SerializeField]
  private SlotBehaviour slotManager;

  [SerializeField]
  private UIManager uiManager;

  internal GameData initialData = null;
  internal UiData initUIData = null;
  internal Root resultData = null;
  internal Player playerdata = null;
  [SerializeField]
  internal bool isResultdone = false;

  private SocketManager manager;
  // protected string nameSpace="game"; //BackendChanges
  protected string nameSpace = "playground"; //BackendChanges
  private Socket gameSocket; //BackendChanges

  [SerializeField]
  internal JSHandler _jsManager;

  protected string TestSocketURI = "https://mx2md3l5-5000.inc1.devtunnels.ms/";
  //protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";
  protected string SocketURI = null;
  //protected string SocketURI = "https://916smq0d-5000.inc1.devtunnels.ms/";
  [SerializeField] internal JSFunctCalls JSManager;
  [SerializeField]
  private string testToken;

  protected string gameID = "SL-GCT";
  //protected string gameID = "";

  internal bool isLoaded = false;
  internal bool SetInit = false;
  private const int maxReconnectionAttempts = 6;
  private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);

  private void Awake()
  {
    isLoaded = false;
  }

  private void Start()
  {
    SetInit = false;
    OpenSocket();
    //Debug.unityLogger.logEnabled = false;
  }

  void ReceiveAuthToken(string jsonData)
  {
    Debug.Log("Received data: " + jsonData);

    // Parse the JSON data
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
    SocketURI = data.socketURL;
    myAuth = data.cookie;
    nameSpace = data.nameSpace;
    // Proceed with connecting to the server using myAuth and socketURL
  }

  string myAuth = null;

  private void OpenSocket()
  {
    //Create and setup SocketOptions
    SocketOptions options = new SocketOptions();
    options.ReconnectionAttempts = maxReconnectionAttempts;
    options.ReconnectionDelay = reconnectionDelay;
    options.Reconnection = true;
    options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket; //BackendChanges

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
    SetupSocketManager(options);
#endif
    // #if UNITY_WEBGL && !UNITY_EDITOR
    //     string url = Application.absoluteURL;
    //     Debug.Log("Unity URL : " + url);
    //     ExtractUrlAndToken(url);

    //     Func<SocketManager, Socket, object> webAuthFunction = (manager, socket) =>
    //     {
    //       return new
    //       {
    //         token = testToken,
    //       };
    //     };
    //     options.Auth = webAuthFunction;
    // #else
    //         Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    //         {
    //             return new
    //             {
    //                 token = testToken,
    //             };
    //         };
    //         options.Auth = authFunction;
    // #endif
    //         // Proceed with connecting to the server
    //         SetupSocketManager(options);

  }

  public void ExtractUrlAndToken(string fullUrl)
  {
    Uri uri = new Uri(fullUrl);
    string query = uri.Query; // Gets the query part, e.g., "?url=http://localhost:5000&token=e5ffa84216be4972a85fff1d266d36d0"

    Dictionary<string, string> queryParams = new Dictionary<string, string>();
    string[] pairs = query.TrimStart('?').Split('&');

    foreach (string pair in pairs)
    {
      string[] kv = pair.Split('=');
      if (kv.Length == 2)
      {
        queryParams[kv[0]] = Uri.UnescapeDataString(kv[1]);
      }
    }

    if (queryParams.TryGetValue("url", out string extractedUrl) &&
        queryParams.TryGetValue("token", out string token))
    {
      Debug.Log("Extracted URL: " + extractedUrl);
      Debug.Log("Extracted Token: " + token);
      testToken = token;
      SocketURI = extractedUrl;
    }
    else
    {
      Debug.LogError("URL or token not found in query parameters.");
    }
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
        token = myAuth,
        gameId = gameID
      };
    };
    options.Auth = authFunction;

    Debug.Log("Auth function configured with token: " + myAuth);

    // Proceed with connecting to the server
    SetupSocketManager(options);
  }

  private void SetupSocketManager(SocketOptions options)
  {
#if UNITY_EDITOR
    //Create and Setup SocketManager for Testing
    this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        // Create and setup SocketManager
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif

    if (string.IsNullOrEmpty(nameSpace))
    {  //BackendChanges Start
      gameSocket = this.manager.Socket;
    }
    else
    {
      print("nameSpace: " + nameSpace);
      gameSocket = this.manager.GetSocket("/" + nameSpace);
    }
    // Set subscriptions
    gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    gameSocket.On<string>(SocketIOEventTypes.Disconnect, OnDisconnected);
    gameSocket.On<string>(SocketIOEventTypes.Error, OnError);
    gameSocket.On<string>("game:init", OnListenEvent);
    gameSocket.On<string>("result", OnResult);
    gameSocket.On<bool>("socketState", OnSocketState);
    gameSocket.On<string>("internalError", OnSocketError);
    gameSocket.On<string>("alert", OnSocketAlert);
    gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice); //BackendChanges Finish
  }

  // Connected event handler implementation
  void OnConnected(ConnectResponse resp)
  {
    Debug.Log("Connected!");
    SendPing();
  }

  private void OnDisconnected(string response)
  {
    Debug.Log("Disconnected from the server");
    StopAllCoroutines();
    uiManager.DisconnectionPopup(false);
  }

  private void OnError(string response)
  {
    Debug.LogError("Error: " + response);
  }

  private void OnListenEvent(string data)
  {
    ParseResponse(data);
  }

  private void OnSocketState(bool state)
  {
    if (state)
    {
      Debug.Log("my state is " + state);
      //InitRequest("AUTH");
    }
    else
    {

    }
  }

  void OnResult(string data)
  {
    ParseResponse(data);
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
    InvokeRepeating("AliveRequest", 0f, 3f);
  }

  private void AliveRequest()
  {
    SendDataWithNamespace("YES I AM ALIVE");
  }

  private void SendDataWithNamespace(string eventName, string json = null)
  {
    // Send the message
    if (gameSocket != null && gameSocket.IsOpen) //BackendChanges
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

  private void InitRequest(string eventName)
  {
    InitData message = new InitData();
    message.Data = new AuthData();
    message.Data.GameID = gameID;
    message.id = "Auth";
    // Serialize message data to JSON
    string json = JsonUtility.ToJson(message);
    Debug.Log(json);
    // Send the message
    if (gameSocket != null && gameSocket.IsOpen)
    {
      gameSocket.Emit(eventName, json);
      Debug.Log("JSON data sent: " + json);
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
  }

  internal void CloseSocket()
  {
    SendDataWithNamespace("game:exit");
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit");
#endif
  }

  private void ParseResponse(string jsonObject)
  {
    Debug.Log(jsonObject);
    Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);

    string id = myData.id;

    switch (id)
    {
      case "initData":
        {
          initialData = myData.gameData;
          initUIData = myData.uiData;
          playerdata = myData.player;
          // bonusdata = myData.BonusData;

          if (!SetInit)
          {
            List<string> LinesString = ConvertListListIntToListString(initialData.lines);
            PopulateSlotSocket(LinesString);
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
          playerdata = myData.player;
          isResultdone = true;
          break;
        }
      case "ExitUser":
        {
          if (gameSocket != null) //BackendChanges
          {
            Debug.Log("Dispose my Socket");
            this.manager.Close();
          }
          //   Application.ExternalCall("window.parent.postMessage", "onExit", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
          JSManager.SendCustomMessage("OnExit");
#endif
          break;
        }
    }
  }

  private void RefreshUI()
  {
    uiManager.InitialiseUIData(initUIData.paylines);
  }

  //This method is used to populate the initial slots.
  private void PopulateSlotSocket(List<string> LineIds)
  {
    slotManager.shuffleInitialMatrix();
    for (int i = 0; i < LineIds.Count; i++)
    {
      slotManager.FetchLines(LineIds[i], i);
    }

    slotManager.SetInitialUI();

    isLoaded = true;
    // Application.ExternalCall("window.parent.postMessage", "OnEnter", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("OnEnter");
#endif
  }

 internal void AccumulateResult(int currBet)
    {
        isResultdone = false;
        MessageData message = new MessageData();
    message.type = "SPIN";
    Debug.Log($"current bet is " + currBet);
    message.payload = new Data();
    message.payload.betIndex = currBet;
    string json = JsonUtility.ToJson(message);
    SendDataWithNamespace("request", json);
    }
  private void SendDataWithNamespace(string namespaceName, double bet, string eventName , string json=null)
  {
    // Construct message data

    // MessageData message = new MessageData();
    // message.currentBet = bet;

    // // Serialize message data to JSON
    // string json = JsonUtility.ToJson(message);
    // Debug.Log(json);
    // // Send the message
    if (gameSocket != null && gameSocket.IsOpen)
    {
      gameSocket.Emit(eventName, json);
      Debug.Log("JSON data sent: " + json);
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
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
public class AuthData
{
  public string GameID;
  //public double TotalLines;
}

[Serializable]
public class MessageData
{
  // public int option;
  // public List<int> index;
  public string type;
  public Data payload;

}
[Serializable]
public class Data
{
  public int betIndex;
//   public string Event;
//   public List<int> index;
//   public int option;

}

[Serializable]
public class ExitData
{
  public string id;
}

[Serializable]
public class InitData
{
  public AuthData Data;
  public string id;
}

[Serializable]
public class AbtLogo
{
  public string logoSprite { get; set; }
  public string link { get; set; }
}

[Serializable]
public class GameData
{
  public List<List<int>> lines { get; set; }
  public List<double> bets { get; set; }
  public List<int> spinBonus;
}

[Serializable]
public class FreeSpins
{
  public int count { get; set; }
  public bool isFreeSpin { get; set; }
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
}

[Serializable]
public class Root
{
  //Result Data
  public bool success { get; set; }
  public List<List<string>> matrix { get; set; }
  public Payload payload { get; set; }
  public Bonus bonus { get; set; }
  public Jackpot jackpot { get; set; }
  public Scatter scatter { get; set; }
  public FreeSpins freeSpin { get; set; }
  //Initial Data
  public string id { get; set; }
  public GameData gameData { get; set; }
  public UiData uiData { get; set; }
  public Player player { get; set; }
}

[SerializeField]
public class Bonus
{
  public int BonusSpinStopIndex { get; set; }
  public double amount { get; set; }
}

[Serializable]
public class Scatter
{
  public double amount { get; set; }
}
[Serializable]
public class Jackpot
{
  public bool isTriggered { get; set; }
  public double amount { get; set; }
}

public class Player
{
  public double balance { get; set; }
}

public class UiData
{
  public Paylines paylines { get; set; }
}

[Serializable]
public class Paylines
{
  public List<Symbol> symbols { get; set; }
}

public class Symbol
{
  public int id { get; set; }
  public string name { get; set; }
  public List<int> multiplier { get; set; }
  public string description { get; set; }
}



[Serializable]
public class PlayerData
{
  public double Balance { get; set; }
  public double haveWon { get; set; }
  public double currentWining { get; set; }
}

[Serializable]
public class AuthTokenData
{
  public string cookie;
  public string socketURL;
  public string nameSpace;
}

