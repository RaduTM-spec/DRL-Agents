using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLFramework;
public class Agent : AgentBase
{
    protected override void Update()
    {
        base.Update();
    }
    protected override void Manual()
    {
        //Implement a keyboard input for your AI - test only
    }
    protected override void CollectObservations(ref float[] SensorBuffer)
    {
        //Fullfill SensorBuffer with observations
    }
    protected override void OnActionReceived(in float[] ActionBuffer)
    {
        //ActionBuffer outputs are in [-1f,1f] range
    }

    //Usefull Methods, use them in CollisionCollider2D, CollisionTrigger2D, Update(), etc. 
    //AddReward()
    //SetReward()
    //EndAction()
    //GetFitness()
   
}
