using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;


public enum GameState { InLobby, Countdown, Programming, Excecuting, GameOver };

public class RobotRoundsHandler : NetworkBehaviour
{
    NetworkVariable<bool> isReady = new NetworkVariable<bool>();
    public NetworkVariable<GameState> gameState = new NetworkVariable<GameState>();
    NetworkVariable<float> countdownTimer = new NetworkVariable<float>();
    NetworkVariable<float> gameTimer = new NetworkVariable<float>();

    TextMeshProUGUI countdownText;
    TextMeshProUGUI gameTimeText;
    GameObject readyScreen;
    GameObject finishedButton;
    GameObject programmingInterface;
    GameObject runProgramButton;
    GameObject stopProgramButton;
    Slider energySlider;
    GameObject hud;

    RobotList robotList;

    RobotMultiplayerInstructionScript instructionScript;
    RobotMultiplayerMovement movementScript;
    MultiplayerLevelInfo levelInfoScript;
    Dead deadScript;
    RobotFlags flagScript;
    PlayerHealthBar healthBarScript;

    MultiplayerWorldParse worldScript;

    public float countdownTime = 5;
    public float programmingTime = 8;
    public float finishedTime = 10;
    public float excecutingTime = 3;
    public float gameTime = 600;


    //Network functions
    public override void OnNetworkSpawn()
    {
        countdownText = GameObject.Find("Countdown").GetComponent<TextMeshProUGUI>();
        gameTimeText = GameObject.Find("Game Time").GetComponent<TextMeshProUGUI>();

        robotList = GameObject.Find("RobotList").GetComponent<RobotList>();
        readyScreen = GameObject.Find("ReadyScreen");
        finishedButton = GameObject.Find("Finished");
        programmingInterface = GameObject.Find("ProgrammingInterface Multiplayer Variant");
        runProgramButton = GameObject.Find("StartButton");
        stopProgramButton = GameObject.Find("StopButton");
        hud = GameObject.Find("Hud");
        energySlider = hud.transform.Find("EnergyBar").GetComponent<Slider>();
        worldScript = GameObject.Find("Load World Multiplayer").GetComponent<MultiplayerWorldParse>();

        //Tells the game to run a function everytime the variables is changed
        gameState.OnValueChanged += GameStateChanged;
        countdownTimer.OnValueChanged += CountdownTimerChanged;
        gameTimer.OnValueChanged += GameTimerChanged;

        instructionScript = GetComponent<RobotMultiplayerInstructionScript>();
        levelInfoScript = GetComponent<MultiplayerLevelInfo>();
        deadScript = GetComponent<Dead>();
        flagScript = GetComponent<RobotFlags>();
        healthBarScript = GetComponentInChildren<PlayerHealthBar>();
        movementScript = GetComponent<RobotMultiplayerMovement>();

        if (IsHost)
        {
            HostSetGameState(GameState.InLobby);
            isReady.Value = false;
        }




        if (IsOwner)
        {
            GameObject.Find("Ready").GetComponent<ReadyButtonScript>().SetRobotRoundsScript(this);
            finishedButton.SetActive(false);
        }

    }

    public override void OnNetworkDespawn()
    {
        gameState.OnValueChanged -= GameStateChanged;
        countdownTimer.OnValueChanged -= CountdownTimerChanged;

    }


    //Changed gamestate
    private void GameStateChanged(GameState oldState, GameState newState)
    {
        Debug.Log("Old: " + oldState + " |  new: " + newState);

        if (IsOwner)
        {
            switch (oldState)
            {
                case GameState.Excecuting:
                    flagScript.CaptureFlag();
                    break;

                case GameState.Countdown:
                    if (IsHost && IsOwner)
                    {
                        gameTimer.Value = gameTime;
                    }
                    break;
            }



            switch (newState)
            {
                //Countdown starts
                case GameState.Countdown:


                    readyScreen.SetActive(false);
                    finishedButton.SetActive(true);
                    runProgramButton.SetActive(false);
                    stopProgramButton.SetActive(false);
                    programmingInterface.SetActive(false);
                    instructionScript.StopExecute();



                    //Host starts countdown
                    if (IsHost)
                    {
                        levelInfoScript.HostSendWorldStringToClients();
                        SetTimerServerRpc(countdownTime);
                        levelInfoScript.HostSendsSpawnPointsToClients();
                        //Move all players to MAP
                    }

                    break;


                case GameState.Programming:
                    //Host starts countdown
                    if (IsHost)
                    {
                        SetTimerServerRpc(programmingTime);

                        //Set all players ready to false (Used for finised button)
                        GameObject[] robots = robotList.GetRobots();
                        foreach (GameObject robot in robots)
                        {
                            robot.GetComponent<RobotRoundsHandler>().HostSetReady(false);
                        }
                    }



                    if (deadScript.IsDead())
                    {
                        healthBarScript.ReviveRobot();
                        movementScript.MoveToSpawnPoints(worldScript.GetSpawnPoint());

                    }

                    programmingInterface.GetComponent<ProgramMuiltiplayerRobot>().stopProgram();
                    programmingInterface.SetActive(true);
                    break;


                case GameState.Excecuting:
                    if (IsHost)
                    {
                        SetTimerServerRpc(excecutingTime);
                    }


                    programmingInterface.GetComponent<ProgramMuiltiplayerRobot>().sendProgramToRobot();
                    programmingInterface.SetActive(false);
                    break;

                case GameState.GameOver:

                    hud.SetActive(false);
                    programmingInterface.GetComponent<ProgramMuiltiplayerRobot>().stopProgram();
                    programmingInterface.SetActive(false);
                    break;
            }
        }
    }

    //Update timer text
    private void CountdownTimerChanged(float oldTimer, float newTimer)
    {
        countdownText.text = "";

        if (newTimer > 0)
            countdownText.text += Mathf.CeilToInt(newTimer);

    }

    //Update timer text
    private void GameTimerChanged(float oldTimer, float newTimer)
    {

        if (newTimer > 0)
        {
            int min = ((int)Mathf.CeilToInt(newTimer) / 60);
            int sec = ((int)Mathf.CeilToInt(newTimer) % 60);
            gameTimeText.text = min + ":" + sec;

        }
        else
            gameTimeText.text = "";


    }


    // Update is called once per frame
    void Update()
    {
        //Host updates timer
        if (IsHost && IsOwner)
        {

            //Countdown the timer (Not "> 0" to stop it from being false on first countdown update)
            if (countdownTimer.Value >= 0)
            {
                countdownTimer.Value -= Time.deltaTime;


                switch (gameState.Value)
                {
                    case GameState.Programming:

                        //If finised programming early
                        if (countdownTimer.Value > finishedTime)
                        {
                            GameObject[] robots = robotList.GetRobots();

                            foreach (GameObject robot in robots)
                            {
                                if (robot.GetComponent<RobotRoundsHandler>().GetIsReady())
                                    SetTimerServerRpc(finishedTime);
                            }
                        }
                        break;
                }
            }

            //TImer is done
            else
            {
                switch (gameState.Value)
                {
                    case GameState.Countdown:
                        HostSetGameStateForAll(GameState.Programming);
                        break;
                    case GameState.Programming:
                        HostSetGameStateForAll(GameState.Excecuting);
                        break;
                    case GameState.Excecuting:
                        HostSetGameStateForAll(GameState.Programming);
                        break;
                }
            }

            if (InsideActiveGame())
            {
                if (gameTimer.Value >= 0)
                    gameTimer.Value -= Time.deltaTime;
                else
                    HostSetGameStateForAll(GameState.GameOver);
            }



        }

        //Bad but wont work in GameStateChanged
        if (gameState.Value == GameState.GameOver)
            programmingInterface.SetActive(false);
    }


    //Return values
    public bool GetIsReady()
    {
        return isReady.Value;
    }


    //Setting network variables
    [ServerRpc]
    public void ToggleReadyServerRpc()
    {
        isReady.Value = !isReady.Value;
    }

    [ServerRpc]
    public void SetTimerServerRpc(float newTimer)
    {

        countdownTimer.Value = newTimer;
    }

    [ServerRpc]
    public void SetIsReadyServerRpc(bool ready)
    {
        isReady.Value = ready;
    }

    [ServerRpc]
    public void SetGameStateForAllServerRpc(GameState gameState)
    {
        HostSetGameStateForAll(gameState);
    }


    public void FinishedProgramming()
    {
        SetIsReadyServerRpc(true);
        programmingInterface.SetActive(false);
    }


    //Functions for the host to set the gamestate
    public void HostSetGameStateForAll(GameState gameState)
    {
        if (IsHost)
        {
            GameObject[] robots = robotList.GetRobots();

            foreach (GameObject robot in robots)
            {
                robot.GetComponent<RobotRoundsHandler>().HostSetGameState(gameState);
            }
        }
    }

    public void HostSetGameState(GameState gameState)
    {
        if (IsHost)
            this.gameState.Value = gameState;
    }

    public void HostSetReady(bool ready)
    {
        if (IsHost)
            this.isReady.Value = ready;
    }


    public bool InsideActiveGame()
    {
        return (gameState.Value == GameState.Programming || gameState.Value == GameState.Excecuting);
    }

    public bool InLobby()
    {
        return gameState.Value == GameState.InLobby;
    }

    public GameState GetCurrentGameState()
    {
        return gameState.Value;
    }


}
