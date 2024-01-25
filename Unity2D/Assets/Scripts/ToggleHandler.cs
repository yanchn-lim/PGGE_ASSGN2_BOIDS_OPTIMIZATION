using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleHandler : MonoBehaviour
{
    [SerializeField]
    Toggle baseToggle;
    [SerializeField]
    Toggle jobsToggle;
    [SerializeField]
    Toggle csToggle;

    [SerializeField]
    FlockBehaviour baseFlock;
    [SerializeField]
    FlockHandler jobsFlock;
    [SerializeField]
    FlockCSHandler csFlock;

    [SerializeField]
    UI ui;

    public void BaseToggle()
    {
        if (baseToggle.isOn)
        {
            baseFlock.gameObject.SetActive(true);
            baseFlock.InitializeSimulation();
            
        }
        else
        {
            baseFlock.gameObject.SetActive(false);
        }

        ui.StartBase(baseToggle.isOn);
    }

    public void JobsToggle()
    {
        if (jobsToggle.isOn)
        {
            jobsFlock.gameObject.SetActive(true);
            jobsFlock.Initialize();
        }
        else
        {
            jobsFlock.gameObject.SetActive(false);
        }

        ui.StartJob(jobsToggle.isOn);
    }

    public void CSToggle()
    {


        ui.StartCS(csToggle.isOn);
    }
}
