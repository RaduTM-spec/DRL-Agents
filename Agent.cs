using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Text;
using System.Linq;

public class Agent : MonoBehaviour
{
    [Header("===== Agent Properties =====")]
    public BehaviourType behaviour = BehaviourType.Default;
    [SerializeField, Range(1, 15), Tooltip("The number of Inputs that the Agent will receive (-1,1)")] private int spaceSize = 1;
    [SerializeField, Range(1, 15), Tooltip("The number of Outputs that the Agent will return (-1,1)")] private int actionSize = 1;
    public NeuralNetwork network = null;

    
    [Space, Header("===== Network Properties =====")]
    [Tooltip("If path != null -> Has a model assigned + if(networkStatus) -> The model is also loaded")] public string path = null;
    [SerializeField, Range(1, 15), Tooltip("The number of Hidden Layers")] private int deep = 1;
    [SerializeField, Range(1, 100), Tooltip("The number of Neurons per Hidden Layer." + "[Usually you can set it as (spaceSize + actionSize)]")] private int neuronsPerLayer = 2;
    int[] layersFormat = null;
    

    [Space(40)]
    [Tooltip("If false -> network variable is null (If it has a saveFile, the network must be Loaded using SetNetworkFromFileFunction(this.path,this.network")]
    public bool networkStatus = false;
    public float currentNNFitness = 0f;
    [Space]
    [Tooltip("Instatiate NeuralNetwork + create a save file + path")] 
    public bool CreateBrain = false;
    [Tooltip("Resets the current NeuralNetwork data")]
    public bool ResetNeuralNetwork = false;


    protected float[] inputs = null;
    protected virtual void Awake()
    {
        UpdateLayersFormat();
        if (path!=null && path != "")
            SetNetworkFromFile(path, ref this.network);
    }
    //---------------------BASE FUNCTIONS------------------//
    protected virtual void Update()
    {
        if (network != null)
            currentNNFitness = network.GetFitness();
        UpdateLayersFormat();
        UpdateNetworkStatus();
        if(CreateBrain == true)
        {
            CreateNeuralNetwork(true,layersFormat);
            CreateBrain = false;
        }
        if(ResetNeuralNetwork == true)
        {
            CreateNeuralNetwork( false,layersFormat);
            ResetNeuralNetwork = false;
        }
        if (behaviour == BehaviourType.Learning)
            Learning(true);
        else if (behaviour == BehaviourType.Heuristic)
            Heuristic();
        else 
            Default();

        
        

        
    }


    //-----------------------FUNCTIONS-------------------//
    /// <summary>
    /// 
    /// </summary>
    /// <param name="needTxtFile"> true if need to create a new save file</param>
    /// <param name="format"> the format of the NeuralNetork in layers, is neccesary a int[]</param>
    /// <param name="copyOfBrain"> data contents from other NeuralNetwork that are copied on the new created NN</param>
    public void CreateNeuralNetwork(bool needTxtFile,int[] format, string[] copyOfBrain = null)
    {

        if (needTxtFile)
        CreateTextFileForNN();

        if (copyOfBrain == null)
        {
            network = new NeuralNetwork(format);
            inputs = new float[format[0]];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = 0;
            }
            UpdateTextFile();
        }
        else
        {
            
           
            while(true)
            {
                  try
                 {
                     File.WriteAllText(path, "");
                     foreach(string line in copyOfBrain)
                     {
                         File.AppendAllText(path, line + "\n");
                     }
                     break;
                 }
                 catch
                 {
                 }
            }
             SetNetworkFromFile(path, ref this.network);
        }
         
        

    }

    /// <summary>
    /// 
    /// Sets the Network for this Agent from another file. The Method Updates his current File with the new Network including Fitness
    /// </summary>
    /// <param name="path"> the path of the network you want to initialize this agent with</param>
    /// <param name="network"> the network of the agent you want to set ( usually this.network)</param>
    public void SetNetworkFromFile(string path, ref NeuralNetwork network)
    {
        if (new FileInfo(path).Length == 0)
        {
            Debug.Log("The Neural Network at the path " + path + " was empty, but we initialized a new Neural Network in it!");
            CreateNeuralNetwork(false, layersFormat);
            return;
        }
        List<string> fileLines = File.ReadAllLines(path).ToList();
        ///
        ///   The strings read from the txt may not be separed corectly. For Example in case of floats, the Line string[] has +2 more elements,
        ///   so in this case 
        ///
        

        //Instatiate Neural Network
        string[] line1 = fileLines[0].Split(',');
            int[] line1_32 = new int[line1.Length];//THIS LINE REPREZENTS THE LAYERS []
            ConvertStrArrToIntArr(line1, ref line1_32);
            network = new NeuralNetwork(line1_32);

            //Update Property in Inspector -- Unverified segment->considered correct
             spaceSize = line1_32[0];
             actionSize = line1_32[line1_32.Length-1];
            layersFormat = new int[line1_32.Length];
            for (int i = 0; i < line1_32.Length; i++)
            {
            layersFormat[i] = line1_32[i];
            }

            //Copy weights data
            List<float[][]> weightsList = new List<float[][]>();
            for (int i = 1; i < fileLines.Count - 1; i++)//First and last line are ignored ( first line is layerFormat, last line is Fitness)
            {
                //One line here are the weights on a single layer
                List<float[]> weightsOnLayer = new List<float[]>();

                string[] line = fileLines[i].Split(',');
                float[] line_32 = new float[line.Length];
                ConvertStrArrToFloatArr(line,ref line_32);

           
                //This array must be devided depeding on the previous layer number of neurons
                int numNeurOnPrevLayer = line1_32[i - 1];
                float[] weightsOnNeuron = new float[numNeurOnPrevLayer]; 
                int count = 0;

                for (int j = 0; j < line_32.Length; j++)
                {
        
                        ///Problema cand ajunge la line32.lenght -1, ultima adaugare nu o baga in weights on Layer
                        if (count < numNeurOnPrevLayer)
                        {   weightsOnNeuron[count] = line_32[j];
                            count += 1;
                        }
                        if (count == numNeurOnPrevLayer)//This is CORRECT -> if count surpassed the number of neurOnPrevLayer
                        {
                            weightsOnLayer.Add(weightsOnNeuron);
                            weightsOnNeuron = new float[numNeurOnPrevLayer];
                            count = 0;
                        }               
                }
                
                weightsList.Add(weightsOnLayer.ToArray());
            }
        network.SetWeights(weightsList.ToArray());//Final set


        float fit = new float();
        try
        {
            fit = float.Parse(fileLines[fileLines.Count - 1]);
        }
        catch (System.Exception e)
        {
            Debug.LogError("string : " + fileLines[fileLines.Count - 1].ToString() + " --> " + e);
        }
        
        network.SetFitness(fit);
        currentNNFitness = fit;
        UpdateTextFile();
    }
    private void CreateTextFileForNN()
    {
        StringBuilder SBtxtNN = new StringBuilder();
        SBtxtNN.Append(Application.streamingAssetsPath);
        SBtxtNN.Append("/Neural_Networks/");
        SBtxtNN.Append("NeuralNetworkID");
        SBtxtNN.Append((this.gameObject.GetInstanceID() * -1).ToString());
        SBtxtNN.Append(".txt");
        path = SBtxtNN.ToString();

        if (!Directory.Exists(Application.streamingAssetsPath + "/Neural_Networks/"))
                Directory.CreateDirectory(Application.streamingAssetsPath + "/Neural_Networks/");
        if (!File.Exists(path))
            File.Create(path);
        else
            Debug.Log("The new Network will be overriden in a path already existent!");

    }
    public void AssignNeuralNetworkFrom(string path)
    {
        //This Function Assigns a TXT file to this Agent, he can modify it
        this.path = path;
        this.network = null;
        SetNetworkFromFile(path, ref this.network);
    }
    public void CopyNetworkFrom(string pat)
    {
        if (pat == null && path == "")
        {
            Debug.LogError("Null Path");
        }
        File.Copy(pat, this.path, true);
    }
    public void AssignCopyOfNeuralNetworkFrom(string path)
    {
        string newPath = path.Substring(0, path.Length - 4) + "Copy.txt";
        if(File.Exists(newPath))
        {
            File.Create(newPath);
        }
        string contents = File.ReadAllText(path);
        File.WriteAllText(newPath, contents);
        this.path = newPath;


        //This Function Make a copy of the TXT file and assigns it to this Agent, he can modify the copy not the original

    }
    public void UpdateTextFile()
    {
        try
        {
        ///summary
        /// []layers
        /// [][][]weights
        File.WriteAllText(path, string.Empty);
        File.AppendAllText(path, string.Join(",", network.GetLayers()));
        File.AppendAllText(path, "\n");
        float[][][] weights = network.GetWeights();
        foreach (float[][] layWeights in weights)
        {
            for (int i = 0; i < layWeights.Length; i++)
            {
                File.AppendAllText(path, string.Join(",", layWeights[i]));
                if (i < layWeights.Length - 1)
                    File.AppendAllText(path, ",");
            }
            File.AppendAllText(path, "\n");
        }
        File.AppendAllText(path, network.GetFitness().ToString());
        }
        catch { }
        
    }
    private void UpdateNetworkStatus()
    {
        if (network == null)
            networkStatus = false;
        else
            networkStatus = true;
    }
    public void ResetFitnessTo(float value, bool alsoUpdateDataFile = true)
    {
        this.network.SetFitness(0);
        if(alsoUpdateDataFile)
              UpdateTextFile();
    }
   

    private void Default()
    {
        if (network == null)
            Heuristic();
        else
            Learning(false);
    }
    protected virtual void Heuristic()
    {

    }
    private void Learning(bool canTrain)
    {
        if(network == null)
        {
            Debug.Log("Agent " + this.name + " cannot inference because it doesn't have a NeuralNetwork assigned! The Behaviour Mode was changed to Default");
            behaviour = BehaviourType.Default;
                return;
        }
        UpdateInputs(out inputs);
        if(inputs != null)
        OnOutputsReceived(PullOutputsFrom(inputs));
    }

    protected virtual void UpdateInputs(out float[] SensorBuffer)
    {
        SensorBuffer = new float[network.GetLayers()[0]]; //Initialize with 0
        //THEN this function somehow must be overrided in player controller




    }
    private float[] PullOutputsFrom(float[] inputs)
    {
        if (network == null)
            return null;
        else if(inputs.Length != network.GetLayers()[0])
        {
            Debug.LogError("Number of Inputs Send to the Neural Network != Number of NN inputs");
            return null;
        }
        else
            return network.ForwardPropagation(inputs);
    }
    protected virtual bool OnOutputsReceived(float[] ActionBuffer) // Inference ONLY
    {
        if (this.behaviour != BehaviourType.Learning)
            return false;
        return true;
    }
    protected void AddReward(float reward, bool alsoUpdateDataFile = true)
    {
        if (network == null)
        {
            Debug.LogError("Cannot modify reward because neural network is null");
            return;
        }
        else
        {
            network.AddFitness(reward);
            UpdateTextFile();
        }
    }
    protected void SetReward(float reward, bool alsoUpdateDataFile = true)
    {
        if (network == null)
        {
            Debug.LogError("Cannot modify reward because neural network is null");
            return;
        }
        else
        {   
            network.SetFitness(reward);
            UpdateTextFile();
        }
    }

    //----------------------External use functions---------------//
    public void MutateHisBrain()
    {
       
        if (network != null)
        {
            network.MutateWeights();
            UpdateTextFile();
        }
        else
            Debug.Log("Cannot Mutate a Brainless Agent");
    }
    protected void EndEpisode()
    {
        behaviour = BehaviourType.Static;
        UpdateTextFile();        
    }
    protected virtual void OnDestroy()
    {
        UpdateTextFile();
    }

    //-----------------------Optional Methods-------------------//
    private void ConvertStrArrToIntArr(string[] str, ref int[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            try
            {
                arr[i] = int.Parse(str[i]);
            }
            catch (System.Exception e)
            {
                Debug.LogError(str[i] + " : " + e);
            }

        }
    }
    private void ConvertStrArrToFloatArr(string[] str, ref float[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            try
            {
                arr[i] = float.Parse(str[i]);
            }
            catch(System.Exception e)
            {
                Debug.LogError(str[i] + " : " + e);
            }

        }
    }
    private void UpdateLayersFormat()
    {
        if (layersFormat == null)
            layersFormat = new int[2 + deep];
        layersFormat[0] = spaceSize;
        layersFormat[layersFormat.Length - 1] = actionSize;

        for (int i = 1; i < layersFormat.Length-1; i++)
        {
            layersFormat[i] = neuronsPerLayer;
        }
    }

    //------------------------------------------Getters-------------------------------------//
    public int[] GetLayersFormat()
    {
        return layersFormat;
    }
    public string GetCurrentNetworkPath()
    {
        return path;
    }

}




public enum BehaviourType
{
    [Tooltip("Self Control and Keyboard Control are OFF.\n" +
        "Behaviour required for Episode end.\n" +
        "When the Episode resets, the behaviour is set to Learning")]
    Static,
    [Tooltip("if nn -> nn control without training\n" +
             "else  -> heuristic control")]
    Default,
    [Tooltip("Keyboard Control from Player")]
    Heuristic,
    [Tooltip("Self Control using the Network & Training")]
    Learning
}