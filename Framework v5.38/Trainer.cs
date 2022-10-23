using UnityEngine;
using MLFramework;
public class Trainer : TrainerBase
{
    [Space, Header("===== Other =====")]
    public GameObject goal;

    protected override void Awake()
    {
        Application.runInBackground = true;
        base.Awake();
    }
    protected override void Start()
    {
        base.Start();
    }
    protected override void SetupTeam()
    {
        base.SetupTeam();
        //Add different colors for your AI's
        //Color add is automatic for agents with SpriteRenderer on the parent gameobject
    }
    protected override void EnvironmentAction()
    {
        //Add actions for your environment, this method is called in Update - Tip: use Time.deltaTime
    }
    protected override void OnEpisodeBegin()
    {
        //Actions after Reseting Episode - Example: Activate reward flags/ Modify the environment randomly
    }
    protected override void OnEpisodeEnd(ref AI ai)
    {
        //Modify each AI before Reseting Episode - Ex: Add postActions reward
        // ai.agent - used to access the agent gameobject
        // ai.script - used to acces Agent Script 
        // ai.fitness - used to get it's current fitness

        //To add reward even if Ai's action ended, use ai.script.AddFitness(value,true) [reward will be added even if AI behaviour becomes Static]
    }
}
