using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;


public class HouseBuilding : Building
{
    public int MaxLivingPeopleInHouse;
    public float SpawnInterval;
    public Human HumanPrefab;
    public Transform SpawnPoint;
    public float JobSearchRadius;
    public float BuildCost = 500f;
    public float MaintenanceCost;
    public float TaxPerResident;
    public GameObject WorkZone;
    private List<Human> residents = new List<Human>();
    

    private WorkBuilding FindNearestJob()
    {
        Collider[] colliders = Physics.OverlapSphere(SpawnPoint.position, JobSearchRadius);
        WorkBuilding best=null;
        float bestDist = float.MaxValue;
        foreach (var coll in colliders)
        {
            WorkBuilding candidate = coll.GetComponent<WorkBuilding>();
            if(candidate == null || !candidate.HasFreeSlot()) continue;
            
            float dist = Vector3.Distance(coll.transform.position, SpawnPoint.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }

            
        }
        if (best != null)
        {
            best.RegisterWorker();
        }
        return  best;
    }
    public void Start()
    {
        WorkZone.transform.localScale = new Vector3(JobSearchRadius, 0.1f, JobSearchRadius);
    }

    public override void OnBuild()
    {
        LightingManager.Instance.OnMorning += SendResidentsToWork;
        LightingManager.Instance.OnEvening += SendResidentHome;
        LightingManager.Instance.OnEvening += ProcessDayEconomy;
        WorkZone.SetActive(false);
        
        base.OnBuild();
    }
    
    
    private void SendResidentsToWork()
    {
        foreach (var resident in residents)
        {
            
            resident.skin.SetActive(true);
            resident.GoToWork();
        }
    }

    private void SendResidentHome()
    {
        foreach (var resident in residents)
        {
            resident.skin.SetActive(true);
            resident.GoToHome();
        }
    }
    private void ProcessDayEconomy()
    {
        float income = residents.Count * TaxPerResident;
        float expenses = MaintenanceCost;

        EconomyManager.Instance.AddMoney(income - expenses);
    }
    public void OnDestroy()
    {
        LightingManager.Instance.OnMorning -= SendResidentsToWork;
        LightingManager.Instance.OnEvening -= SendResidentHome;
        LightingManager.Instance.OnEvening -= ProcessDayEconomy;
    }
}
