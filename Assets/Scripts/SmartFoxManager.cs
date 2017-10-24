using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Variables;
using Sfs2X.Logging;
using Sfs2X.Util;
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

public class SmartFoxManager : MonoBehaviour
{
    public bool debugMode = false;
    public Text numStudentsValue;
    public Button startGameButton;

    public Transform questionsContent;
    public Button questionMold;

    public GameObject startScreen;
    public GameObject answersScreen;
    public GameObject questionSelectionScreen;

    public Text currentQuestion;
    public List<Text> numAnswers;

    private string defaultHost = "smartfox.scholastic-labs.io";
    private int defaultTcpPort = 443;
    private int defaultWsPort = 8080;

    private int httpPort = 8080;                // HTTP port (for BlueBox connection)
    private int httpsPort = 8443;				// HTTPS port (for protocol encryption initialization in non-websocket connections)
    private List<String> questions = new List<String>();
    private SmartFox sfs;
    private SFSRoom _room;
    private int _numStudents;
    private struct Question
    {
        public int id;
        public string value;
        public List<string> answers;
    }
    private List<Question> _questions = new List<Question>();

    void Start()
    {

        if (sfs == null || !sfs.IsConnected)
        {
            Debug.Log("Teacher connecting...");
            sfs = new SmartFox();
            sfs.ThreadSafeMode = true;

            sfs.AddEventListener(SFSEvent.CONNECTION, OnConnection);
            sfs.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
            sfs.AddEventListener(SFSEvent.CRYPTO_INIT, OnCryptoInit);
            sfs.AddEventListener(SFSEvent.LOGIN, OnLogin);
            sfs.AddEventListener(SFSEvent.LOGIN_ERROR, OnLoginError);
            sfs.AddEventListener(SFSEvent.PING_PONG, OnPingPong);
            sfs.AddEventListener(SFSEvent.ROOM_JOIN, OnJoinRoom);
            sfs.AddEventListener(SFSEvent.ROOM_JOIN_ERROR, OnJoinRoomError);
            sfs.AddEventListener(SFSEvent.EXTENSION_RESPONSE, OnExtensionResponse);
            sfs.AddEventListener(SFSEvent.PUBLIC_MESSAGE, OnPublicMessage);
            sfs.AddEventListener(SFSEvent.ROOM_VARIABLES_UPDATE, OnRoomVariableUpdated);

            sfs.AddLogListener(LogLevel.DEBUG, OnDebugMessage);
            sfs.AddLogListener(LogLevel.INFO, OnInfoMessage);
            sfs.AddLogListener(LogLevel.WARN, OnWarnMessage);
            sfs.AddLogListener(LogLevel.ERROR, OnErrorMessage);

            // Set connection parameters
            ConfigData cfg = new ConfigData();
            cfg.Host = defaultHost;
            cfg.Port = Convert.ToInt32(defaultTcpPort);
            cfg.HttpPort = httpPort;
            cfg.HttpsPort = httpsPort;
            cfg.Zone = "BasicExamples";
            cfg.Debug = debugMode;
           
            TextAsset textAsset = (TextAsset)Resources.Load("community_challenge");
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(textAsset.text);
            //Read

            XmlNode root = xmldoc.LastChild.FirstChild;
            Debug.Log("name is"+root.Name);
            foreach (XmlNode node in root.ChildNodes)
            {
                Debug.Log("Children: " + node.ChildNodes);
                //Debug.Log("Outisde if"+node.FirstChild.NodeType);
                if (node.Name.CompareTo("question") ==0)
                {
                    int id = 1;
                    foreach (XmlNode node1 in node.ChildNodes)
                    {
                        //Debug.Log("answer count+ "+node1.ChildNodes.Count);
                        var newquestion = new Question();
                        newquestion.value = node1.Attributes["value"].Value;
                        newquestion.answers = new List<string>();
                        newquestion.id = id;
                        id++;
                        foreach (XmlNode node2 in node1.ChildNodes)
                        {
                            newquestion.answers.Add(node2.Attributes["type"].Value);
                        }
                        if(questionsContent != null && questionMold != null)
                        {
                            var newQuestionButton = Instantiate(questionMold, questionsContent);
                            var buttonScript = newQuestionButton.GetComponent<Button>();
                            var buttonText = buttonScript.transform.GetComponentInChildren<Text>();
                            if(buttonText!=null)
                            {
                                buttonText.text = newquestion.value;
                            }
                            buttonScript.gameObject.SetActive(true);
                            buttonScript.name = newquestion.id.ToString();
                            buttonScript.onClick.AddListener(delegate { SelectQuestion(newquestion.id); });
                        }
                        _questions.Add(newquestion);
                    }
                }   
            }
            sfs.Connect(cfg);
        }
    }

    void SelectQuestion(int questionId)
    {
        Debug.Log("QuestionSelected = " + questionId);
        //TODO: send the answers to the kids
        var quest = _questions.Find(a => a.id == questionId);
        string message = "Options:[\"";
        message += string.Join("\",\"", quest.answers.ToArray());
        message += "\"]";
        sfs.Send(new Sfs2X.Requests.PublicMessageRequest(message));
        questionSelectionScreen.SetActive(false);
        answersScreen.SetActive(true);
        currentQuestion.text = quest.value;
        foreach(var studNum in numAnswers)
        {
            studNum.text = "0";
        }
    }

    void Update()
    {
        // As Unity is not thread safe, we process the queued up callbacks on every frame
        if (sfs != null)
            sfs.ProcessEvents();
    }
    void OnApplicationQuit()
    {
        if(sfs!=null&&_room!=null)
        {
            sfs.Send(new Sfs2X.Requests.LeaveRoomRequest(_room));
            sfs.Send(new Sfs2X.Requests.LogoutRequest());
            Debug.Log("Server Closing");
            Debug.Log("Application ending after " + Time.time + " seconds");
        }
       
       
    }

    private void reset()
    {
        // Remove SFS2X listeners
        sfs.RemoveAllEventListeners();

        sfs.RemoveLogListener(LogLevel.DEBUG, OnDebugMessage);
        sfs.RemoveLogListener(LogLevel.INFO, OnInfoMessage);
        sfs.RemoveLogListener(LogLevel.WARN, OnWarnMessage);
        sfs.RemoveLogListener(LogLevel.ERROR, OnErrorMessage);

        sfs = null;
    }

    private void login()
    {
        // Login as guest

        Debug.Log("Starting to login as the teacher");

        sfs.Send(new Sfs2X.Requests.LoginRequest("teacher"));
    }

    public void StartGame()
    {
        Debug.Log("Start the game");
        startScreen.SetActive(false);
        questionSelectionScreen.SetActive(true);
    }

    public void SendOptions()
    {
        sfs.Send(new Sfs2X.Requests.PublicMessageRequest("Options:[\"Option 1\",\"Option 2\",\"Option 3\"]"));
    }

    private void OnConnection(BaseEvent evt)
    {
        if ((bool)evt.Params["success"])
        {
            Debug.Log("Connection established successfully, SFS2X API version: " + sfs.Version + ", Connection mode is: " + sfs.ConnectionMode);

            login();
        }
        else
        {
            Debug.Log("Connection failed; is the server running at all?");

            // Remove SFS2X listeners and re-enable interface
            reset();
        }
    }

    private void OnConnectionLost(BaseEvent evt)
    {
        Debug.Log("Connection was lost; reason is: " + (string)evt.Params["reason"]);

        // Remove SFS2X listeners and re-enable interface
        reset();
    }

    private void OnCryptoInit(BaseEvent evt)
    {
        if ((bool)evt.Params["success"])
        {
            Debug.Log("Encryption initialized successfully");

            // Attempt login
            login();
        }
        else
        {
            Debug.Log("Encryption initialization failed: " + (string)evt.Params["errorMessage"]);
        }
    }

    private void OnLogin(BaseEvent evt)
    {
        User user = (User)evt.Params["user"];

        Debug.Log("Login successful");
        Debug.Log("Username is: " + user.Name);

        _room = new SFSRoom(0, "The Lobby");
        sfs.Send(new Sfs2X.Requests.JoinRoomRequest(_room));
    }

    private void OnLoginError(BaseEvent evt)
    {
        Debug.Log("Login failed: " + (string)evt.Params["errorMessage"]);
    }

    private void OnPingPong(BaseEvent evt)
    {
        Debug.Log("Measured lag is: " + (int)evt.Params["lagValue"] + "ms");
    }

    private void OnJoinRoom(BaseEvent evt)
    {
        var _room = (evt.Params["room"] as SFSRoom);
        Debug.Log("OnJoingRoom: [id = " +_room.Id + ", name = " + _room.Name + "]");

        List<RoomVariable> roomVars = new List<RoomVariable>();
        roomVars.Add(new SFSRoomVariable("gameStarted", false));
        sfs.Send(new Sfs2X.Requests.SetRoomVariablesRequest(roomVars));

        UpdateNumOfStudents(_room.GetVariable("numStudents").GetIntValue());

        /*var reqParams = new Sfs2X.Entities.Data.SFSObject();
        reqParams.PutInt("a", 25);
        reqParams.PutInt("b", 17);

        sfs.Send(new Sfs2X.Requests.ExtensionRequest("sum", reqParams, _room));*/
    }

    private void OnJoinRoomError(BaseEvent evt)
    {
        Debug.Log("OnJoinRoomFailed: " + (string)evt.Params["errorMessage"]);
    }

    private void OnExtensionResponse(BaseEvent evt)
    {
        var responseParams = (evt.Params["params"] as Sfs2X.Entities.Data.SFSObject);
        if (responseParams == null)
        {
            Debug.Log("error: cannot get params from the message");
            return;
        }
        int result = responseParams.GetInt("res");

        Debug.Log("sum = " + result);
    }

    private void OnPublicMessage(BaseEvent evt)
    {
        var message = (string)evt.Params["message"];
        Debug.Log("OnPublicMessage: " + message);
        if(message.StartsWith("Answer:"))
        {
            var temp1 = message.Split(':');
            var temp2 = temp1[1].Split(',');
            Debug.Log("answer num = " + temp2[1]);
            string result = temp2[1].Replace("]", "");
            int answer = Convert.ToInt32(result);
            var numStr = numAnswers[answer - 1].text;
            numAnswers[answer - 1].text = (Convert.ToInt32(numStr) + 1).ToString();
        }
    }

    private void OnRoomVariableUpdated(BaseEvent evt)
    {
        Debug.Log("OnRoomVariableUpdated: " + evt.ToString());
        List<string> changedVars = (List<string>)evt.Params["changedVars"];
        Room room = (Room)evt.Params["room"];

        // Check if the gameStarted variable was changed
        if (changedVars.Contains("gameStarted"))
        {
            var gameStarted = room.GetVariable("gameStarted").GetBoolValue();
            Debug.Log("Received updated gameStarted = " + gameStarted);
            if(gameStarted == true)
            {
                sfs.Send(new Sfs2X.Requests.PublicMessageRequest("Options=[\"Option 1\",\"Option 2\",\"\"]"));
            }
        }

        if (changedVars.Contains("numStudents"))
        {
            UpdateNumOfStudents(room.GetVariable("numStudents").GetIntValue());
        }
    }

    private void UpdateNumOfStudents(int num)
    {
        _numStudents = num;
        Debug.Log("Received updated numStudents = " + _numStudents);
        numStudentsValue.text = _numStudents.ToString();
        if (_numStudents > 0)
        {
            startGameButton.interactable = true;
        }
    }

    public void OnDebugMessage(BaseEvent evt)
    {
        string message = (string)evt.Params["message"];
        ShowLogMessage("DEBUG", message);
    }

    public void OnInfoMessage(BaseEvent evt)
    {
        string message = (string)evt.Params["message"];
        ShowLogMessage("INFO", message);
    }

    public void OnWarnMessage(BaseEvent evt)
    {
        string message = (string)evt.Params["message"];
        ShowLogMessage("WARN", message);
    }

    public void OnErrorMessage(BaseEvent evt)
    {
        string message = (string)evt.Params["message"];
        ShowLogMessage("ERROR", message);
    }

    private void ShowLogMessage(string level, string message)
    {
        message = "[SFS > " + level + "] " + message;
        Debug.Log(message);
    }
}
