using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorkBuilding : Building
{
    public int maxWorkers;
    public Transform entrancePoint;
    private int _currentWorkers;

    public bool HasFreeSlot()
    {
        return _currentWorkers < maxWorkers;
    }

    public void RegisterWorker()
    {
        _currentWorkers++;
    }

    public void FireWorker()
    {
        _currentWorkers=Mathf.Max(0,_currentWorkers-1);
    }

    public Vector3 GetEntrancePosition()
    {
        return entrancePoint.position;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
