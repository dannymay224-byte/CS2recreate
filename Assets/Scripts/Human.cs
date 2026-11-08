using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

public class Human : MonoBehaviour
{
    private NavMeshAgent agent;
    
    private Animator animator;
    public HouseBuilding Home;
    public WorkBuilding Work;
    public float arrivalThreshold = 1.2f;
    [SerializeField]public GameObject skin;
    public enum State
    {
        idle,
        walkingToWork,
        walkingHome,
        working,
        atHome
    }

    public State CurrentState { get; private set; } = State.idle;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        transform.position = Home.SpawnPoint.position;
    }
    public void GoToWork()
    {
        agent.enabled = true;

        if (Work == null)
            return;
        agent.Warp(Home.SpawnPoint.position);
        skin.SetActive(true);
        
        agent.SetDestination(Work.transform.position);
        CurrentState = State.walkingToWork;
    }

    public void GoToHome()
    {
        agent.enabled = true;

        if (Home == null || Work == null)
            return;
        agent.Warp(Work.entrancePoint.position);
        skin.SetActive(true);
        agent.SetDestination(Home.transform.position);
        CurrentState = State.walkingHome;
    }


    public void MoveTo(Vector3 target)
    {
        
        agent.SetDestination(target);
    }
    public void SetHome(HouseBuilding building)
    {
        Home = building;
    }
    public void SetWork(WorkBuilding target)
    {
        Work = target;
    }
    void Update()
    {
        if (!agent.pathPending && agent.remainingDistance <= arrivalThreshold)
        {
            Debug.Log(agent.remainingDistance);
            switch (CurrentState)
            {
                case State.walkingToWork:
                    CurrentState = State.working;
                    skin.SetActive(false);
                    agent.enabled = false;
                    break;

                case State.walkingHome:
                    CurrentState = State.atHome;
                    skin.SetActive(false);
                    agent.enabled = false;
                    break;
            }
        }


        UpdateAnimation();
    }
    void UpdateAnimation()
    {
        bool isWalking = CurrentState == State.walkingHome || CurrentState == State.walkingToWork;
        animator.SetBool("Walk", isWalking);
    }

    
}