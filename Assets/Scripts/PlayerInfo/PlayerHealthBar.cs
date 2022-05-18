using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class PlayerHealthBar : NetworkBehaviour
{
    //[SerializeField] RectTransform healthAmount;
    public Slider healthSlider;
    public Slider abovePlayerHealth;
    GameObject programmingInterface;

    // Network variables
    NetworkVariable<int> healthPoints = new NetworkVariable<int>(100);

    // Local variables
    public int localHealth = 100;
    public int heal = 50;
    public bool changeColorLocal = false;
    public int damageTilePower = 10;


    //Local scripts
    RobotRoundsHandler roundsHandlerScript;
    RobotFlags flagScript;
    Dead deadScript;

  


    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            healthSlider = GameObject.Find("Hud").transform.Find("HealthBar").GetComponent<Slider>();
        }

        abovePlayerHealth = this.GetComponentInChildren<Slider>();
        roundsHandlerScript = GetComponentInParent<RobotRoundsHandler>();
        programmingInterface = GameObject.Find("ProgrammingInterface Multiplayer Variant");
        flagScript = GetComponentInParent<RobotFlags>();
        deadScript = GetComponentInParent<Dead>();

    }

    void Update()
    {
        if (Input.GetKeyDown("space"))
            GetHit(25);

        if (!IsOwner)
        {
            localHealth = healthPoints.Value;
            abovePlayerHealth.value = (float)localHealth;
            healthSlider.value = (float)localHealth;
        }

        //Die on fall
        if (gameObject.transform.position.y < -20) GetHit(100);
    }

    
    public void ReviveRobot()
    {
        localHealth = 100;
        UpdateHealthInfoServerRpc(localHealth);
        deadScript.SetDeadServerRpc(false);
        localHealth = healthPoints.Value;
        abovePlayerHealth.value = (float)localHealth;
        healthSlider.value = (float)localHealth;

    }

    public void GetHit(int damageAmount)
    {
        if (IsOwner && roundsHandlerScript.InsideActiveGame() && localHealth > 0)
        {
            if ((localHealth - damageAmount) > 0)
            {
                localHealth = localHealth - damageAmount;
                healthSlider.value = (float)localHealth;
                abovePlayerHealth.value = (float)localHealth;
            }
            else
            {
                localHealth = 0;
                abovePlayerHealth.value = (float)localHealth;
                healthSlider.value = 0f;
                killed();
            }
            UpdateHealthInfoServerRpc(localHealth);
        }
    }

    [ServerRpc]
    public void UpdateHealthInfoServerRpc(int health)
    {
        healthPoints.Value = health;
        abovePlayerHealth.value = (float)health;
        healthSlider.value = (float)health;
    }

    public void HealPowerUp()
    {
        if (IsOwner)
        {
            if ((localHealth + heal) < 100)
            {
                localHealth = localHealth + heal;
            }
            else
            {
                localHealth = 100;
            }

            healthSlider.value = (float)localHealth;
            abovePlayerHealth.value = (float)localHealth;
            UpdateHealthInfoServerRpc(localHealth);
            
        }
    }

    public void killed()
    {
        MonoBehaviour[] comps = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour c in comps)
        {
            if (c.GetType() == typeof(MultiplayerDetectTarget))
            {
                c.enabled = false;
            }
        }

        healthSlider.value = (float)localHealth;
        abovePlayerHealth.value = (float)localHealth;

        if (IsOwner)
        {
            GetComponentInParent<RobotMultiplayerInstructionScript>().StopExecute();
            programmingInterface.SetActive(false);
            flagScript.LoseFlag();
            //robotMovementScript.MoveToSpawnPoints(worldScript.GetSpawnPoint());
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (!IsHost)
        {
            if (!NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent<Dead>(out var dead))
                return;
            dead.SetDeadServerRpc(true);
        }
        else
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out NetworkClient networkClient))
                return;
            if (!networkClient.PlayerObject.TryGetComponent<Dead>(out var dead))
                return;
            dead.SetDeadServerRpc(true);
        }
    }

    public void DamageTile()
    {
        GetHit(damageTilePower);
    }
}
