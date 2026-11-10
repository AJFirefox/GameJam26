using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CommonEnemy : MonoBehaviour
{
    private NavMeshAgent aI;
    private GameObject player;
    private bool gameOver;
    public SphereCollider hitBox;
    private bool canAttack = true;
    public float attackTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        aI = GetComponent<NavMeshAgent>();
        player = GameObject.Find("Player");
        hitBox.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        FollowPlayer();
        Attacking();
    }

    void FollowPlayer()
    {

        if (gameOver == false)
        {
            aI.SetDestination(player.transform.position);
        }
    }

    void Attacking()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= 2 && canAttack)
        {
            hitBox.enabled = true;
            canAttack = false;
            StartCoroutine(ResetAttack());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Damaged player");
            hitBox.enabled = false;
        }
    }

    IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackTime);
        canAttack = true;
    }

}
