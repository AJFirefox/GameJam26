using UnityEngine;
using UnityEngine.AI;

public class Cyclops : CommonEnemy
{
    public GameObject boulder;
    public float throwForce;
    public Transform bSpawnPos;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        aI = GetComponent<NavMeshAgent>();
        player = GameObject.Find("Player");
    }

    // Update is called once per frame
    void Update()
    {
        FollowPlayer();
        Attacking();
    }
    

    protected override void Attacking()
    {

        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= 15 && canAttack)
        {
            GameObject projectile = Instantiate(boulder, bSpawnPos.position, Quaternion.identity);
            projectile.GetComponent<Rigidbody>().AddForce(transform.forward * throwForce, ForceMode.Impulse);
            canAttack = false;
            StartCoroutine(ResetAttack());
            
        }

        if (distance <= 15)
        {
            transform.LookAt(player.transform.position);
        }
    }



}
