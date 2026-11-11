using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CommonEnemy : MonoBehaviour 
{
    protected NavMeshAgent aI;
    protected GameObject player;
    protected bool gameOver;
    public SphereCollider hitBox;
    protected bool canAttack = true;
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

    protected void FollowPlayer()
    {

        if (gameOver == false)
        {
            aI.SetDestination(player.transform.position);
        }
    }

    protected virtual void Attacking()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= 2 && canAttack)
        {
            hitBox.enabled = true;
            canAttack = false;
            StartCoroutine(ResetAttack());
        }
    }

    protected void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Damaged player");
            hitBox.enabled = false;
        }
    }

    protected IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(attackTime);
        canAttack = true;
    }

}
