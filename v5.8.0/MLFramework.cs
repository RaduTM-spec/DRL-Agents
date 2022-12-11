using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.IO;
using UnityEngine.UI;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using TMPro;
using System;
using System.Text.RegularExpressions;

namespace MLFramework
{
    // v5.8
    // eliminated path copying and inserting models directly using TextAsset
    // script auto turns off any cameraFollow custom scripts
    // minor bug fixes

    // v5.7.3.1
    // minor change

    // v5.7.2
    // slight modification to the tooltips/var names
    // minor bug fixes

    // v5.7.1 -> simplifcations update
    // single threaded mode removed
    // Batching mode simplified to the slider
    // Stochastic mode removed

    // v5.7 beta
    // momentum added
    // l2 regularization added

    // v5.6
    // process data multithreaded 
    // with multithreading works 2x times faster on 8threads

    // v5.5.2
    // min learnRate increased to 0.0001 (probably in the future it will be increased by 10)
    // Heuristic module changed to learn and collect (writing was removed)

    // v5.5.1
    // minor debug messages changes
    // neural network visualization to the heuristic training
    // colored weights per nn visualization
    // cross entropy slightly fixed

    // v5.5.0
    // batching split added
    // changed from ProcessOneEpoch to ProcessOneBatch
    // decreased data collection frequency to 1/3 our of 3 frames

    // v5.4.4
    // SetupTeam() method erased from the virtual methods. No more overriding because is mainly useless.
    // OnEpisodeBegin() method calls each environement separately, one GameObject is parsed at a time, similar to OnEpisodeEnd().

    // v5.4.3
    // +adds for sensor buffer
    // +one agent per environment update
    // +OnEpisodeBegin() updated
    // +training data file direct name added

    //5.4.2
    //softmax added
    public class NeuralNetwork
    {
        public static ActivationFunctionType activation = ActivationFunctionType.Tanh;
        public static ActivationFunctionType outputActivation = ActivationFunctionType.Tanh;
        public static MutationStrategy mutation = MutationStrategy.Classic;
        public static InitializationFunctionType initialization = InitializationFunctionType.StandardDistribution;

        protected int[] layers;
        protected float[][] neurons;
        protected float[][][] weights;
        protected float[][] biases;
        protected float fitness;

        public NeuralNetwork(int[] layers)
        {
            InitializeLayers(layers);
            InitializeNeuronsAndBiases(false);
            InitializeWeights(false);
            fitness = 0f;

        }
        public NeuralNetwork(NeuralNetwork copyNN)
        {
            InitializeLayers(copyNN.layers);
            InitializeNeuronsAndBiases(false);
            InitializeWeights(false);
            SetWeightsWith(copyNN.weights);
            SetBiasesWith(copyNN.biases);
            SetFitness(copyNN.GetFitness());
        }
        public NeuralNetwork(string fileText)
        {
            string[] fileLines = Regex.Split(fileText, "\n|\r|\r\n");

            //Get Layers Data
            string[] layersLineStr = fileLines[0].Split("n,");//one more read than neccesary
            int noLayers = layersLineStr.Length - 1;
            int[] layersLineInt = new int[noLayers];
            Functions.ArrayConversion.ConvertStrArrToIntArr(layersLineStr, ref layersLineInt);
            InitializeLayers(layersLineInt);
            InitializeNeuronsAndBiases(true);
            InitializeWeights(true);

            //Get Weights Data
            List<float[][]> weightsList = new List<float[][]>();
            for (int i = 1; i < noLayers; i++)//First and last lineStr are ignored ( first lineStr is layerFormat, last lineStr is Fitness)
            {
                //One lineStr here are the weightss on a single neuronsNumber
                List<float[]> weightsOnLayer = new List<float[]>();

                string[] lineStr = fileLines[i].Split("w,");// one more read than neccesary
                float[] lineInt = new float[lineStr.Length - 1];
                Functions.ArrayConversion.ConvertStrArrToFloatArr(lineStr, ref lineInt);


                //This probArr must be devided depeding on the previous neuronsNumber number of neurons
                int numNeurOnPrevLayer = layersLineInt[i - 1];
                float[] weightsOnNeuron = new float[numNeurOnPrevLayer];
                int count = 0;

                for (int j = 0; j < lineInt.Length; j++)
                {

                    ///Problema cand ajunge la line32.lenght -1, ultima adaugare nu o baga in weightss on Layer
                    if (count < numNeurOnPrevLayer)
                    {
                        weightsOnNeuron[count] = lineInt[j];
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
            this.SetWeightsWith(weightsList.ToArray());

            //Get Biases Data
            for (int i = noLayers, lay = 0; i < noLayers * 2; i++, lay++)
            {
                string[] lineStr = fileLines[i].Split("b,");//one more read than neccesary

                string str = lay.ToString() + ":";
                float[] lineInt = new float[lineStr.Length - 1];
                Functions.ArrayConversion.ConvertStrArrToFloatArr(lineStr, ref lineInt);
                this.biases[lay] = lineInt;
            }

            //Get Fitness Data
            string fitStr = fileLines[fileLines.Length - 1];
            fitStr = fitStr.Substring(0, fitStr.Length - 3);
            float fit = float.Parse(fitStr);
            this.SetFitness(fit);
        }
        public static void WriteBrain(in NeuralNetwork net, string path)
        {
            //Json serialization does not support multidimensionalArrays
            /*string json = JsonUtility.ToJson(net);
            path = path.Substring(0, path.Length - 4) + ".json";
            System.IO.File.WriteAllText(path, json);
            return;*/


            StringBuilder data = new StringBuilder();

            //AddLayers
            data.Append(string.Join("n,", net.GetLayers()));
            data.Append("n,\n");

            //AddWeights
            StringBuilder weightsSB = new StringBuilder();
            float[][][] weights = net.GetWeights();
            foreach (float[][] layWeights in weights)
            {
                for (int i = 0; i < layWeights.Length; i++)
                {
                    weightsSB.Append(string.Join("w,", layWeights[i]));
                    weightsSB.Append("w,");
                }
                weightsSB.Append("\n");
            }
            data.Append(weightsSB);

            //AddBiases
            StringBuilder biasesSB = new StringBuilder();
            float[][] biases = net.GetBiases();
            for (int i = 0; i < biases.Length; i++)
            {
                biasesSB.Append(string.Join("b,", biases[i]));
                if (i == 0)
                    biasesSB.Append("b, @input layer biases are never used\n");
                else biasesSB.Append("b,\n");
            }
            data.Append(biasesSB);

            data.Append(net.GetFitness().ToString());
            data.Append("fit");
            File.WriteAllText(path, data.ToString());
        }

        public float[] ForwardPropagation(float[] inputs)
        {
            SetInputs(inputs);

            for (int l = 1; l < layers.Length; l++)
            {
                for (int n = 0; n < neurons[l].Length; n++)
                {
                    float value = biases[l][n];
                    for (int p = 0; p < neurons[l - 1].Length; p++)
                    {
                        value += weights[l - 1][n][p] * neurons[l - 1][p];
                    }

                    neurons[l][n] = value;

                    if (l != layers.Length - 1)
                        neurons[l][n] = Activate(value, false);
                    else if (outputActivation != ActivationFunctionType.SoftMax)
                        neurons[l][n] = Activate(value, true);



                }
                if (l == layers.Length - 1 && outputActivation == ActivationFunctionType.SoftMax)
                    Functions.Activation.ActivationFunctionSoftMax(ref neurons[layers.Length - 1]);
            }

            return neurons[neurons.Length - 1]; //Return the last neurons layer with their values (OUTPUT)
        }

        //-----------------INITIALIZATION-----------------//
        protected void InitializeLayers(int[] layers)
        {
            this.layers = new int[layers.Length];
            SetLayersWith(layers);
        }
        protected void InitializeNeuronsAndBiases(bool emptyBiases)
        {
            List<float[]> neuronsList = new List<float[]>();
            List<float[]> biasesList = new List<float[]>();
            foreach (int neur in layers)
            {
                neuronsList.Add(new float[neur]);
                biasesList.Add(new float[neur]);
            }
            neurons = neuronsList.ToArray();

            if (!emptyBiases)
                for (int i = 1; i < biasesList.Count; i++)
                    Initialize(biasesList[i]);
            biases = biasesList.ToArray();
        }
        protected void InitializeWeights(bool empty)
        {
            List<float[][]> weightsList = new List<float[][]>();
            for (int i = 1; i < layers.Length; i++)
            {
                List<float[]> weightsOnLayerList = new List<float[]>();
                int neuronsInPreviousLayer = layers[i - 1];

                for (int n = 0; n < neurons[i].Length; n++)
                {
                    float[] neuronWeights = new float[neuronsInPreviousLayer];
                    if (!empty)
                        Initialize(neuronWeights);
                    weightsOnLayerList.Add(neuronWeights);
                }
                weightsList.Add(weightsOnLayerList.ToArray());
            }
            weights = weightsList.ToArray();
        }

        protected void Initialize(float[] axons)
        {

            switch (initialization)
            {
                case InitializationFunctionType.StandardDistribution:
                    for (int i = 0; i < axons.Length; i++)
                    {
                        axons[i] = Functions.Initialization.RandomInNormalDistribution(new System.Random(), 0, 1);
                    }
                    break;
                case InitializationFunctionType.Deviation1Distribution:
                    for (int i = 0; i < axons.Length; i++)
                    {
                        axons[i] = Functions.Initialization.RandomValueInCustomDeviationDistribution(0.15915f, 1.061f, 0.3373f);
                    }
                    break;
                case InitializationFunctionType.Deviation2Distribution:
                    for (int i = 0; i < axons.Length; i++)
                    {
                        axons[i] = Functions.Initialization.RandomValueInCustomDeviationDistribution(0.15915f, 2f, 0.3373f);
                    }
                    break;
                case InitializationFunctionType.RandomValue:
                    for (int i = 0; i < axons.Length; i++)
                    {
                        axons[i] = Functions.Initialization.RandomValue();
                    }
                    break;
                default:
                    for (int i = 0; i < axons.Length; i++)
                    {
                        axons[i] = Functions.Initialization.RandomValueInCustomDeviationDistribution(0.15915f, 2f, 0.3373f);
                    }
                    break;

            }
        }

        //--------------------MUTATIONS---------------------//
        public void MutateWeightsAndBiases()
        {
            for (int i = 0; i < weights.Length; i++)
                for (int j = 0; j < weights[i].Length; j++)
                    for (int k = 0; k < weights[i][j].Length; k++)
                        MutateOneWeightOrBias(ref weights[i][j][k]);

            for (int i = 0; i < biases.Length; i++)
                for (int j = 0; j < biases[i].Length; j++)
                    MutateOneWeightOrBias(ref biases[i][j]);
        }
        protected void MutateOneWeightOrBias(ref float weightOrBias)
        {

            switch (mutation)
            {
                case MutationStrategy.Classic:
                    Functions.Mutation.ClassicMutation(ref weightOrBias);
                    break;
                case MutationStrategy.LightPercentage:
                    Functions.Mutation.LightPercentageMutation(ref weightOrBias);
                    break;
                case MutationStrategy.LightValue:
                    Functions.Mutation.LightValueMutation(ref weightOrBias);
                    break;
                case MutationStrategy.StrongPercentage:
                    Functions.Mutation.StrongPercentagegMutation(ref weightOrBias);
                    break;
                case MutationStrategy.StrongValue:
                    Functions.Mutation.StrongValueMutation(ref weightOrBias);
                    break;
                case MutationStrategy.Chaotic:
                    Functions.Mutation.ChaoticMutation(ref weightOrBias);
                    break;
                default:
                    Functions.Mutation.ClassicMutation(ref weightOrBias);
                    break;
            }
        }


        //---------------------FITNESS------------------------//
        public int CompareTo(NeuralNetwork other)
        {
            if (other == null) return 1;

            if (this.fitness > other.fitness) return 1;
            if (this.fitness < other.fitness) return -1;
            return 0;
        }
        public void AddFitness(float fit)
        {
            this.fitness += fit;
        }
        public void SetFitness(float fit)
        {
            this.fitness = fit;
        }
        public float GetFitness()
        {
            return this.fitness;
        }

        //--------------------------ACTIVATION---------------------------//
        static float Activate(float value, bool isOutputLayer = false)
        {
            ActivationFunctionType where = isOutputLayer == true ? outputActivation : activation;

            switch (where)
            {
                case ActivationFunctionType.Tanh:
                    return Functions.Activation.ActivationFunctionTanh(value);
                case ActivationFunctionType.Sigmoid:
                    return Functions.Activation.ActivationFunctionSigmoid(value);
                case ActivationFunctionType.Relu:
                    return Functions.Activation.ActivationFunctionReLU(value);
                case ActivationFunctionType.LeakyRelu:
                    return Functions.Activation.ActivationFunctionLeakyReLU(value);
                case ActivationFunctionType.BinaryStep:
                    return Functions.Activation.ActivationFunctionBinaryStep(value);
                case ActivationFunctionType.Silu:
                    return Functions.Activation.ActivationFunctionSiLU(value);
                default:
                    return 0f;
            }
        }
        static float ActivationDerivative(float value, bool isOutputLayer)
        {
            ActivationFunctionType where = isOutputLayer == true ? outputActivation : activation;

            switch (where)
            {
                case ActivationFunctionType.Tanh:
                    return Functions.Derivatives.DerivativeTanh(value);
                case ActivationFunctionType.Sigmoid:
                    return Functions.Derivatives.DerivativeSigmoid(value);
                case ActivationFunctionType.Relu:
                    return Functions.Derivatives.DerivativeReLU(value);
                case ActivationFunctionType.LeakyRelu:
                    return Functions.Derivatives.DerivativeLeakyReLU(value);
                case ActivationFunctionType.BinaryStep:
                    return Functions.Derivatives.DerivativeBinaryStep(value);
                case ActivationFunctionType.Silu:
                    return Functions.Derivatives.DerivativeSiLU(value);
                default:
                    return 0;
            }


        }

        //-------------- GETTERS------------------//
        public int[] GetLayers()
        {
            return layers;
        }
        public float[][][] GetWeights()
        {
            return weights;
        }
        public float[][] GetBiases()
        {
            return biases;
        }
        public int GetInputsNumber()
        {
            return layers[0];
        }
        public int GetOutputsNumber()
        {
            return layers[layers.Length - 1];
        }

        public float GetWeight(int layerAnd1, int neuronIndex, int weightIndex)
        {
            return weights[layerAnd1][neuronIndex][weightIndex];
        }
        public float GetBias(int layerAnd1, int neuron)
        {
            return biases[layerAnd1][neuron];
        }
        public void AddToWeight(int layerAnd1, int neuronIndex, int weightIndex, float value)
        {
            weights[layerAnd1][neuronIndex][weightIndex] += value;
        }
        public void AddToBias(int layerAnd1, int neuron, float value)
        {
            biases[layerAnd1][neuron] += value;
        }
        //------------------SETTERS---------------//
        public void SetLayersWith(int[] layers)
        {
            for (int i = 0; i < this.layers.Length; i++)
            {
                this.layers[i] = layers[i];
            }
        }
        public void SetWeightsWith(float[][][] weightss)
        {
            for (int i = 0; i < weightss.Length; i++)
                for (int j = 0; j < weightss[i].Length; j++)
                    for (int k = 0; k < weightss[i][j].Length; k++)
                        this.weights[i][j][k] = weightss[i][j][k];
        }
        public void SetBiasesWith(float[][] biasess)
        {
            for (int i = 0; i < biasess.Length; i++)
                for (int j = 0; j < biasess[i].Length; j++)
                    this.biases[i][j] = biasess[i][j];
        }
        //------------------------------------------------------------------------HEURISTIC ------------------------------------------------------------------------------//
        #region HEURISTIC

        float[][][] weightsGradients = null;//same form as weights
        float[][] biasesGradients = null;//same form as biases (input layer biases are not counted)

        float[][][] weightsVelocities = null;
        float[][] biasesVelocities = null;
        class BatchError
        {
            //batch error is stored inside a object referenced type just to be locked in multithreading
            public float error = 0;
            public BatchError()
            {
                error = 0;
            }
        }
        BatchError batchError = new BatchError();
        public void InitMainGradientsArrays()
        {
            if (weightsGradients != null)
                return;
            ///Init Gradients ARRAYS

            weightsGradients = new float[weights.Length][][];
            biasesGradients = new float[biases.Length][];

            for (int i = 0; i < weights.Length; i++)
            {
                weightsGradients[i] = new float[weights[i].Length][];
                for (int j = 0; j < weights[i].Length; j++)

                    weightsGradients[i][j] = new float[weights[i][j].Length];
            }
            for (int i = 0; i < biases.Length; i++)
                biasesGradients[i] = new float[biases[i].Length];


            //Init Velocities ARRAYS
            if (weightsVelocities != null)
                return;

            weightsVelocities = new float[weights.Length][][];
            biasesVelocities = new float[biases.Length][];

            for (int i = 0; i < weights.Length; i++)
            {
                weightsVelocities[i] = new float[weights[i].Length][];
                for (int j = 0; j < weights[i].Length; j++)
                    weightsVelocities[i][j] = new float[weights[i][j].Length];
            }
            for (int i = 0; i < biases.Length; i++)
                biasesVelocities[i] = new float[biases[i].Length];

        }
        public void ApplyGradients(float learnRate, int batchSize, float momentum, float regularization)
        {
            //apply the weightsGradients and biasesGradients with the specific learnRate
            for (int i = 0; i < weights.Length; i++)
                ApplyGradientsOnLayer(i, learnRate / batchSize);

            void ApplyGradientsOnLayer(int weightLayerIndex, float modifiedLearnRate)
            {
                int inNeurons = layers[weightLayerIndex];
                int outNeurons = layers[weightLayerIndex + 1];
                int biasLayerIndex = weightLayerIndex + 1;

                //weight decay used in regularization
                float weightDecay = 1 - regularization * modifiedLearnRate;

                for (int i = 0; i < outNeurons; i++)
                {
                    for (int j = 0; j < inNeurons; j++)
                    {
                        //update velocity
                        weightsVelocities[weightLayerIndex][i][j] = weightsVelocities[weightLayerIndex][i][j] * momentum - weightsGradients[weightLayerIndex][i][j] * modifiedLearnRate;
                        //update weight
                        weights[weightLayerIndex][i][j] = weights[weightLayerIndex][i][j] * weightDecay + weightsVelocities[weightLayerIndex][i][j];
                        //reset gradient
                        weightsGradients[weightLayerIndex][i][j] = 0;
                    }
                    //update velocity
                    biasesVelocities[biasLayerIndex][i] = biasesVelocities[biasLayerIndex][i] * momentum - biasesGradients[biasLayerIndex][i] * modifiedLearnRate;
                    //update bias
                    biases[biasLayerIndex][i] += biasesVelocities[biasLayerIndex][i];
                    //reset gradient
                    biasesGradients[biasLayerIndex][i] = 0;
                }
            }
        }


        public void UpdateGradients(float[] inputs, float[] desiredOutputs, LossFunctionType lossFunc)
        {
            float[][] localNeurons;
            Node[][] localNodes;

            //------------------------------------------------------------------

            //init arrays
            InitLocalNeurArray_and_SetInputs_and_InitLocalNodes();

            //calculate outputs using localNeurons
            float[] outs = CalculatePropagatedOutputs();

            //calculate output cost and update gradient for last weights
            float sampleErr = CalculateOutputNodesCost(outs, desiredOutputs);
            lock (batchError)
            {
                batchError.error += sampleErr;
            }
            UpdateGradientsForLayer(layers.Length - 2);

            //update gradients foreach layer
            //Update gradients for hidden weights
            for (int weightLayer = layers.Length - 3; weightLayer >= 0; weightLayer--)//parse each weights layer from END to BEGINING
            {
                CalculateNodesCost(weightLayer + 1);//Calculate the valuesInDerivative of the nodes from the left
                UpdateGradientsForLayer(weightLayer);//Get the modifications needed for each weight and bias in that layer
            }

            //------------------------------------------------------------------
            //METHODS USED
            void InitLocalNeurArray_and_SetInputs_and_InitLocalNodes()
            {
                //init array - do not modify here
                localNeurons = new float[neurons.Length][];
                for (int i = 0; i < localNeurons.Length; i++)
                    localNeurons[i] = new float[neurons[i].Length];

                //init inputs inside
                if (inputs.Length != localNeurons[0].Length)
                    Debug.LogError("<color=red>The number of inputs received is " + inputs.Length + " and the number of input neurons is " + localNeurons[0].Length + "</color>.<color=grey> Expect some inputs to be ignored!</color>");
                for (int i = 0; i < localNeurons[0].Length; i++)
                {
                    localNeurons[0][i] = inputs[i];
                }

                //init local nodes
                localNodes = new Node[layers.Length][];
                localNodes[0] = new Node[inputs.Length];
                for (int i = 0; i < inputs.Length; i++)
                    localNodes[0][i].valueOut = inputs[i];

            }

            float[] CalculatePropagatedOutputs()
            {
                for (int l = 1; l < layers.Length; l++)
                {
                    localNodes[l] = new Node[localNeurons[l].Length];

                    for (int n = 0; n < localNeurons[l].Length; n++)
                    {

                        float value = biases[l][n];
                        int previousLayerNeuronsNumber = layers[l - 1];
                        for (int p = 0; p < previousLayerNeuronsNumber; p++)
                        {
                            value += weights[l - 1][n][p] * localNeurons[l - 1][p];
                        }

                        localNodes[l][n].valueIn = value;
                        localNeurons[l][n] = value;//is activated after

                        if (l < layers.Length - 1)
                            localNeurons[l][n] = Activate(value, false);
                        else if (outputActivation != ActivationFunctionType.SoftMax)
                            localNeurons[l][n] = Activate(value, true);

                        localNodes[l][n].valueOut = localNeurons[l][n];

                    }

                    ///SPECIAL CASE
                    if (l == layers.Length - 1 && outputActivation == ActivationFunctionType.SoftMax)
                    {
                        int neuronsOnLastLayer = layers[l];

                        //Get values In  (it works also for values out because the values are passed normally without Activation =: softMax activation is made after all node values are known)
                        float[] valuesIn = new float[neuronsOnLastLayer];

                        for (int n = 0; n < localNeurons[l].Length; n++)
                            valuesIn[n] = localNodes[l][n].valueIn;

                        //Activate them
                        Functions.Activation.ActivationFunctionSoftMax(ref valuesIn);

                        //Set values Out
                        for (int n = 0; n < localNodes[l].Length; n++)
                        {
                            localNeurons[l][n] = valuesIn[n];
                            localNodes[l][n].valueOut = localNeurons[l][n];
                        }


                    }

                }
                return localNeurons[localNeurons.Length - 1];
            }
            float CalculateOutputNodesCost(float[] outputs, float[] expectedOutputs)
            {
                //calculates average error of output nodes
                //calculates output nodes costValue

                if (outputActivation == ActivationFunctionType.SoftMax)
                    return CalculateOutputNodesCostForSoftMax(outputs, expectedOutputs);

                float cost = 0;
                for (int i = 0; i < outputs.Length; i++)
                {

                    if (lossFunc == LossFunctionType.Quadratic)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.QuadraticDerivative(outputs[i], expectedOutputs[i]) * ActivationDerivative(localNodes[localNodes.Length - 1][i].valueIn, true);
                        cost += Functions.Cost.Quadratic(outputs[i], expectedOutputs[i]);
                    }
                    else if (lossFunc == LossFunctionType.Absolute)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.AbsoluteDerivative(outputs[i], expectedOutputs[i]) * ActivationDerivative(localNodes[localNodes.Length - 1][i].valueIn, true);
                        cost += Functions.Cost.Absolute(outputs[i], expectedOutputs[i]);
                    }
                    else if (lossFunc == LossFunctionType.CrossEntropy)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.CrossEntropyDerivative(outputs[i], expectedOutputs[i]) * ActivationDerivative(localNodes[localNodes.Length - 1][i].valueIn, true);
                        float localCost = Functions.Cost.CrossEntropy(outputs[i], expectedOutputs[i]);
                        cost += float.IsNaN(localCost) ? 0 : localCost;
                    }

                }

                return cost / outputs.Length;//divided by the number of neurons
            }//LOSS FUNCTION
            float CalculateOutputNodesCostForSoftMax(float[] outputs, float[] expectedOutputs)
            {
                float cost = 0f;
                float[] derivatedInValues = new float[outputs.Length];
                for (int i = 0; i < derivatedInValues.Length; i++)
                    derivatedInValues[i] = localNodes[localNodes.Length - 1][i].valueIn;
                Functions.Derivatives.DerivativeSoftMax(ref derivatedInValues);

                for (int i = 0; i < outputs.Length; i++)
                {
                    if (lossFunc == LossFunctionType.Quadratic)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.QuadraticDerivative(outputs[i], expectedOutputs[i]) * derivatedInValues[i];
                        cost += Functions.Cost.Quadratic(outputs[i], expectedOutputs[i]);
                    }
                    else if (lossFunc == LossFunctionType.CrossEntropy)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.CrossEntropyDerivative(outputs[i], expectedOutputs[i]) * derivatedInValues[i];
                        float localCost = Functions.Cost.CrossEntropy(outputs[i], expectedOutputs[i]);
                        cost += float.IsNaN(localCost) ? 0 : localCost;
                    }
                    else if (lossFunc == LossFunctionType.Absolute)
                    {
                        localNodes[localNodes.Length - 1][i].costValue = Functions.Cost.AbsoluteDerivative(outputs[i], expectedOutputs[i]) * derivatedInValues[i];
                        cost += Functions.Cost.Absolute(outputs[i], expectedOutputs[i]);
                    }
                }

                return cost;
            }//LOSS FUNCTION USED WHEN SOFTMAX USED
            void CalculateNodesCost(int neuronsLayer)
            {
                //IT DOES NOT APPLY FOR OUTPUT NEURON LAYER and INPUT LAYER
                if (neuronsLayer == 0)
                    return;
                int nodesNum = localNodes[neuronsLayer].Length;
                int nextLayerNeuronsNum = layers[neuronsLayer + 1];

                //The node value is equal to:
                // = Sum(nextLayerNeuron * connectionWeight) * Activation'(nodeValue.valueIN);

                for (int i = 0; i < nodesNum; i++)
                {
                    localNodes[neuronsLayer][i].costValue = 0;
                    for (int j = 0; j < nextLayerNeuronsNum; j++)
                    {
                        localNodes[neuronsLayer][i].costValue += localNodes[neuronsLayer + 1][j].costValue * weights[neuronsLayer][j][i]; // sum of each nextNeuron*connectionWeight;
                    }

                    localNodes[neuronsLayer][i].costValue *= ActivationDerivative(localNodes[neuronsLayer][i].valueIn, false);
                }
            }
            void UpdateGradientsForLayer(int weightLayerIndex)
            {
                //this is a weight layer
                //node layer is always + 1 up
                int inNeurons = layers[weightLayerIndex];
                int outNeurons = layers[weightLayerIndex + 1];
                int biasLayerIndex = weightLayerIndex + 1;
                lock (weightsGradients)
                {
                    for (int i = 0; i < outNeurons; i++)
                        for (int j = 0; j < inNeurons; j++)
                            weightsGradients[weightLayerIndex][i][j] += localNodes[weightLayerIndex][j].valueOut * localNodes[weightLayerIndex + 1][i].costValue;
                }
                lock (biasesGradients)
                {
                    for (int i = 0; i < outNeurons; i++)
                        biasesGradients[biasLayerIndex][i] += 1 * localNodes[weightLayerIndex + 1][i].costValue;
                }
                return;
            }
        }

        public float GetError()
        {
            //also resets the error
            float whatToReturn = batchError.error;
            batchError.error = 0;
            return whatToReturn;
        }
        //------------------------------------------------------------------------END HEURISTIC ------------------------------------------------------------------------------//
        #endregion




        //--------------COMPLEMENTARY METHODS---------------//
        protected void SetInputs(float[] inputs)
        {
            if (inputs.Length != neurons[0].Length)
                Debug.LogError("<color=red>The number of inputs received is " + inputs.Length + " and the number of input neurons is " + neurons[0].Length + "</color>.<color=grey> Expect some inputs to be ignored!</color>");


            for (int i = 0; i < neurons[0].Length; i++)
            {
                neurons[0][i] = inputs[i];
            }
        }


    }
    public class AgentBase : UnityEngine.MonoBehaviour
    {
        [Header("===== Agent Properties =====")]
        public BehaviorType behavior = BehaviorType.Static;
        [Tooltip("@model used for this agent")] public TextAsset networkModel = null;
        [Tooltip("@if has brain: saves current brain data\n@else: creates a brain using Network Properties\n@folder: StreamingAssets/Neural_Networks. \n@default naming format or uses Path")] public bool SaveBrain = false;
        public NeuralNetwork network = null;
        List<PosAndRot> initialPosition = new List<PosAndRot>(); static int parseCounter = 0;

        [Header("===== Network Properties =====\n@Base Settings")]
        [SerializeField, Range(1, 50), Tooltip("The number of Inputs that the Agent will receive")] private int sensorSize = 2;
        [SerializeField, Range(1, 50), Tooltip("The number of Outputs that the Agent will return")] private int actionSize = 2;
        [SerializeField, Tooltip("Each element is a hidden layer\nEach value is the number of neurons\n@biases not count")] private List<uint> hiddenLayers;

        [Header("@Advanced Settings")]
        [Tooltip("@activation function used in hidden layers")]
        public ActivationFunctionType activationType = ActivationFunctionType.Tanh;
        [Tooltip("@activation function used for output layer\n@influences the actionBuffer values")]
        public ActivationFunctionType outputActivationType = ActivationFunctionType.Tanh;
        [Tooltip("@initializes weights and biases of a newly created network")]
        public InitializationFunctionType initializationType = InitializationFunctionType.StandardDistribution;


        [Header("===== Heuristic Properties =====\n@Base Settings")]
        public HeuristicModule module = HeuristicModule.Collect;
        [Tooltip("@file that holds the training data used for heuristic training")] public TextAsset trainingDataFile;
        [Range(1, 1000), Tooltip("@number of parsings through the training batch.")] public uint epochs = 100;
        [Range(0, 300), Tooltip("@data collection time in seconds.\n@33.33 samples per second")] public float sessionLength = 60;
        [Space(5)]
        [Tooltip("@reset the environment transforms when agent action ended")] public GameObject Environment;
        [Tooltip("@watch the progression of the error\n@if is noisy, decrease the learnRate\n@if stagnates, increase the learnRate")] public RectTransform errorGraph;
        [Space(5), SerializeField, Tooltip("@read only\n@average error of a batch")] private float error;

        [Header("@Advanced Settings")]
        [Tooltip("@do not append/write samples where expected outputs are null\n@expected outputs are considered null if all action vector elements are equal to 0")] public bool killStaticActions = true;
        [Range(0.0001f, 1f), Tooltip("@modification strength per iteration")] public float learnRate = 0.1f;
        [Tooltip("@improves gradient descent time"), Range(0, 0.9f)] public float momentum = 0.9f;
        [Tooltip("@impact to weight decay"), Range(0, 0.1f)] public float regularization = 0.001f;
        [Tooltip("@loss function type")] public LossFunctionType lossFunction = LossFunctionType.Quadratic;
        [Tooltip("@how whole training data is splitted into mini-batches.\n@if = 1 -> Full Batch\n@else -> Mini Batch"), Range(0.01f, 1.00f)] public float batchSplit = 0.10f;


        //ONLY HEURISTIC 
        private List<Sample> samplesCollected;//used only when appending/learning
        private List<List<Sample>> batches;//used only when learning
        int totalTrainingSamples = 0;
        //Environmental
        private List<PosAndRot> environmentInitialTransform;
        //ErrorStatistic
        bool callStatistic = false;
        float maxErrorFound = 0;
        uint startingEpochs = 0;
        List<Vector2> errorPoints = new List<Vector2>();

        //Mini batch help vars only
        int miniBatchSize;
        int miniBatchesNumber;
        int currentMiniBatchIndex;//used for data spliting in minibatches and also when training 1 batch at a time

        //Sampling collection
        int frameIndex;//frame index while collecting data

        protected virtual void Awake()
        {
            GetAllTransforms();
            //On heuristic and self the brains are made directly in CollectHeuristicData and SelfAction,this may cause in action lag

            startingEpochs = epochs;

            //Precaution
            if (activationType == ActivationFunctionType.SoftMax)
            {
                Debug.Log("<color=#4db8ff>SoftMax</color> cannot be an activation function for input or hidden layers. Now is set to <color=#4db8ff>Tanh</color>!");
                activationType = ActivationFunctionType.Tanh;
            }
            if (behavior == BehaviorType.Manual || behavior == BehaviorType.Heuristic)
                HeuristicPreparation();
        }
        protected virtual void Update()
        {
            //SmallChecks
            BUTTONSaveBrain();
            if (behavior == BehaviorType.Self)
                SelfAction();
            else if (behavior == BehaviorType.Manual)
                ManualAction();
            else if (behavior == BehaviorType.Heuristic)
                HeuristicAction();

        }

        void SelfAction()
        {
            if (network == null)
            {
                if (networkModel == null)
                {
                    Debug.LogError("<color=red>Cannot Self Control because the Brain Path uploaded is invalid</color>");
                    return;
                }
                this.network = new NeuralNetwork(networkModel.text);

                NeuralNetwork.activation = activationType;
                NeuralNetwork.outputActivation = outputActivationType;
                NeuralNetwork.initialization = initializationType;

            }
            SensorBuffer sensorBuffer = new SensorBuffer(network.GetInputsNumber());
            CollectObservations(ref sensorBuffer);
            ActionBuffer actionBuffer = new ActionBuffer(network.ForwardPropagation(sensorBuffer.GetBuffer()));
            OnActionReceived(in actionBuffer);
        }
        void ManualAction()
        {
            ActionBuffer buffer = new ActionBuffer(this.network.GetOutputsNumber());
            Heuristic(ref buffer);
            OnActionReceived(in buffer);
        }
        void HeuristicAction()
        {
            if (module == HeuristicModule.Learn)
                ProcessOneBatch();
            else
                CollectTrainingData();
        }

        //-------------------------------------------HEURISTIC TRAINING--------------------------------------------------//
        void CollectTrainingData()
        {
            sessionLength -= Time.deltaTime;
            if (sessionLength <= 0)
            {
                behavior = BehaviorType.Static;

                Debug.Log("<color=#64de18>Appending <color=#e405fc>" + samplesCollected.Count + " </color>training samples...</color>");
                File.AppendAllLines(
                                    trainingDataFile != null ?
                                    AssetDatabase.GetAssetPath(trainingDataFile) :
                                                                                (GetHeuristicSamplesPath() + "TrainingData_" + UnityEngine.Random.Range(0, 1000).ToString() + ".txt"),
                                    GetLinesFromBatchList()
                                    );


                Debug.Log("<color=#64de18>Data collected succesfully.</color>");

                return;
            }

            //Get inputs
            SensorBuffer inputs = new SensorBuffer(network.GetInputsNumber());
            CollectObservations(ref inputs);
            //Get userOutputs
            ActionBuffer desiredOutputs = new ActionBuffer(network.GetOutputsNumber());
            Heuristic(ref desiredOutputs);
            OnActionReceived(desiredOutputs);

            // //This part is used to save a sample less than 1 per frame -> 2/3 samples from 3 frames are deprecated
            frameIndex++;
            try
            {
                if (frameIndex % (int)(0.03f / Time.deltaTime) != 0)
                    return;
                //at 600 fps, deltaTime = 0.00166 -> (0.03/deltaTime) = 18 frames
                //a sample is collected every 18 frames -> 33.33 samples per second
                //at 100 fps, deltaTime = 0.01 -> (0.03/0.01) = 3 frames
                // a sample is collected every 3 frames -> 33.33 samples per second

                //If i want to change this, also change the debug.log from heuristic preparation where ~minutes are shown

                // 0.02 -> 50 samples per second
                // 0.03 -> 33.33 samples per second
                // 0.04 -> 25 samples per second

            }
            catch { /*divided by 0*/}


            //Creating sample
            Sample sample = new Sample();
            sample.inputs = inputs.GetBuffer();
            sample.expectedOutputs = desiredOutputs.GetBuffer();
            //Check if null inputs KILLABLE
            if (killStaticActions)
            {
                bool isStatic = true;
                foreach (var output in sample.expectedOutputs)
                    if (output != 0)
                        isStatic = false;
                if (isStatic == false)
                    samplesCollected.Add(sample);
            }
            else samplesCollected.Add(sample);


        }
        void HeuristicPreparation()
        {
            HeuristicEnvironmentSetup();
            HeuristicOnSceneReset();

            if (network == null)
            {
                if (networkModel == null)
                {
                    Debug.LogError("<color=red>Brain Path is invalid</color>");
                    return;
                }
                this.network = new NeuralNetwork(networkModel.text);

                NeuralNetwork.activation = activationType;
                NeuralNetwork.outputActivation = outputActivationType;
                NeuralNetwork.initialization = initializationType;

                HeuristicOnSceneReset();
            }

            if (behavior == BehaviorType.Manual)
                return;

            if (module == HeuristicModule.Learn)
            {
                batches = new List<List<Sample>>();

                //Debug.Log("<color=#64de18>Collecting data from file <color=grey>" + samplesPath + "</color>...</color>");
                if (trainingDataFile == null)
                {
                    Debug.Log("<color=red>TrainingData file is invalid.</color>");
                    behavior = BehaviorType.Static;
                    return;
                }

                string[] stringBatch = File.ReadAllLines(AssetDatabase.GetAssetPath(trainingDataFile));

                miniBatchesNumber = (int)(1f / batchSplit);
                miniBatchSize = stringBatch.Length / (2 * miniBatchesNumber);

                currentMiniBatchIndex = 0;

                for (int i = 0; i < stringBatch.Length / 2; i++)
                {
                    AddSampleToBatches(GetSampleFromData(stringBatch[i * 2], stringBatch[i * 2 + 1]));
                    totalTrainingSamples++;
                }
                currentMiniBatchIndex = 0;

                Debug.Log("<color=#64de18>Total samples: <color=#e405fc>" + totalTrainingSamples + "</color> = <color=#e405fc>"
                          + miniBatchesNumber + "</color> mini-batches <color=#e02810>X </color><color=#e405fc>" + miniBatchSize + "</color> samples " +
                          "(~<color=#e02810>" + totalTrainingSamples / 2000 + "</color> minutes of training data)" +
                          ". Agent is learning. Force Stop the simulation by sliding epochs to 0.</color>");
            }
            else if (module == HeuristicModule.Collect)
            {
                samplesCollected = new List<Sample>();
                frameIndex = 0;
                //case file exists
                try
                {
                    FileInfo fi = new FileInfo(AssetDatabase.GetAssetPath(trainingDataFile));
                    if (fi.Exists)
                    {
                        Debug.Log("<color=#64de18>Collecting gameplay from user...</color>");
                        return;
                    }
                }
                catch { }

                //case file do not exist
                Debug.Log("<color=64de18>Training data file created.</color>");
                Debug.Log("<color=#64de18>Collecting data from user...</color>");

            }
        }
        void AddSampleToBatches(Sample sample)
        {
            if (batches.Count == 0)
            {
                batches.Add(new List<Sample>() { sample });
                return;
            }
            if (batches[currentMiniBatchIndex].Count > miniBatchSize)
            {
                batches.Add(new List<Sample>());
                currentMiniBatchIndex++;
            }
            batches[currentMiniBatchIndex].Add(sample);

        }
        void ProcessOneBatch()
        {
            if (epochs > 0)
            {
                if (currentMiniBatchIndex > miniBatchesNumber - 1)
                { currentMiniBatchIndex = 0; epochs--; }


                network.InitMainGradientsArrays();

                //Update gradients multithreaded
                System.Threading.Tasks.Parallel.ForEach(batches[currentMiniBatchIndex], (sample) =>
                {
                    network.UpdateGradients(sample.inputs, sample.expectedOutputs, lossFunction);
                });

                //Apply gradients
                network.ApplyGradients(learnRate, batches[currentMiniBatchIndex].Count, momentum, regularization);

                error = network.GetError() / batches[currentMiniBatchIndex].Count;//is a mini batch error
                callStatistic = true;

                currentMiniBatchIndex++;
            }
            else
            {
                Debug.Log("<color=#4db8ff>Heuristic training has ended succesfully.</color><color=grey> Watch your agent current performance.</color>");
                NeuralNetwork.WriteBrain(network, AssetDatabase.GetAssetPath(networkModel));

                NeuralNetwork.activation = activationType;
                NeuralNetwork.outputActivation = outputActivationType;
                NeuralNetwork.initialization = initializationType;


                ResetEnvironmentToInitialPosition();
                ResetToInitialPosition();
                behavior = BehaviorType.Self;
            }
        }


        private void OnDrawGizmos()
        {
            if (errorGraph == null)
                return;
            //adds a point with the error everytime is called
            if (!callStatistic)
                return;
            float xSize = errorGraph.rect.width;
            float ySize = errorGraph.rect.height / 2;
            try
            {
                Gizmos.matrix = errorGraph.localToWorldMatrix;

                Vector2 zero = new Vector2(-xSize / 2, 0);
                float zeroX = -xSize / 2;

                //Draw AXIS
                Gizmos.color = Color.white;
                Gizmos.DrawLine(zero, new Vector2(zeroX, ySize));//up
                Gizmos.DrawLine(zero, new Vector2(-zeroX, 0f));//right
                Gizmos.DrawSphere(zero, 5f);
                //Draw Arrows
                float arrowLength = 10f;
                //Y
                Gizmos.DrawLine(new Vector2(zeroX, ySize), new Vector2(zeroX - arrowLength, ySize - arrowLength));
                Gizmos.DrawLine(new Vector2(zeroX, ySize), new Vector2(zeroX + arrowLength, ySize - arrowLength));
                //X
                Gizmos.DrawLine(new Vector2(-zeroX, 0), new Vector2(-zeroX - arrowLength, -arrowLength));
                Gizmos.DrawLine(new Vector2(-zeroX, 0), new Vector2(-zeroX - arrowLength, +arrowLength));
                float xUnit;
                float yUnit;
                if (error > maxErrorFound)
                {
                    float oldMaxError = maxErrorFound;
                    maxErrorFound = error;
                    xUnit = xSize / (startingEpochs * miniBatchesNumber);
                    yUnit = ySize / maxErrorFound;
                    for (int i = 0; i < errorPoints.Count; i++)
                    {
                        errorPoints[i] = new Vector2(zeroX + xUnit * i, errorPoints[i].y * (oldMaxError / maxErrorFound));
                    }
                }
                else
                {
                    xUnit = xSize / (startingEpochs * miniBatchesNumber);
                    yUnit = ySize / maxErrorFound;
                }


                Vector2 newErrorPoint = new Vector2(zeroX + xUnit * errorPoints.Count, yUnit * error);
                errorPoints.Add(newErrorPoint);

                //Draw Dots
                Gizmos.color = Color.blue;
                foreach (Vector2 point in errorPoints)
                    Gizmos.DrawSphere(point, .5f);

                //Draw Lines
                Gizmos.color = Color.green;
                //Gizmos.DrawLine(zero, errorPoints[0]);
                for (int i = 0; i < errorPoints.Count - 1; i++)
                    Gizmos.DrawLine(errorPoints[i], errorPoints[i + 1]);
            }
            catch { }
            //draw network
            try
            {
                float SCALE = .05f;
                Color emptyNeuron = Color.yellow;
                Color biasColor = Color.green;
                NeuralNetwork nety = network;



                int[] layers = nety.GetLayers();
                float[][][] weights = nety.GetWeights();
                float[][] biases = nety.GetBiases();

                Vector2[][] neuronsPosition = new Vector2[layers.Length][];//starts from up-left
                Vector2[] biasesPosition = new Vector2[layers.Length - 1];//one for each layer

                float maxNeuronsInLayers = layers.Max();
                float scale = 1 / (layers.Length * maxNeuronsInLayers) * SCALE;
                float xOffset = -xSize / 2;
                float yOffset = -ySize / 2;

                float layerDistanceUnit = xSize / (layers.Length - 1);
                float neuronDistanceUnit = ySize / (maxNeuronsInLayers) / 2;
                neuronDistanceUnit -= neuronDistanceUnit * 0.15f;//substract 10% to make it a bit smaller - also not substract 1  form maxNeuronsInLayers beacause is one more bias

                //FIND POSITIONS
                for (int layerNum = 0; layerNum < layers.Length; layerNum++)//take each layer individually
                {
                    //float layerYstartPose = (maxNeuronsInLayers - layers[layerNum]) / 2 * neuronDistanceUnit;
                    float layerYStartPose = -(maxNeuronsInLayers - layers[layerNum]) / 2 * neuronDistanceUnit - 50f;//substract 30f to not interact with the graph
                    neuronsPosition[layerNum] = new Vector2[layers[layerNum]];
                    for (int neuronNum = 0; neuronNum < layers[layerNum]; neuronNum++)
                        neuronsPosition[layerNum][neuronNum] = new Vector2(layerNum * layerDistanceUnit + xOffset, layerYStartPose - neuronNum * neuronDistanceUnit);
                    if (layerNum < layers.Length - 1)
                        biasesPosition[layerNum] = new Vector2(layerNum * layerDistanceUnit + xOffset, layerYStartPose - layers[layerNum] * neuronDistanceUnit);
                }

                //Draw biases weights with their normal values
                for (int i = 1; i < neuronsPosition.Length; i++)
                {
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                    {
                        float weightValue = biases[i][j];
                        if (weightValue > 0)
                            Gizmos.color = new Color(0, 0, weightValue);
                        else Gizmos.color = new Color(-weightValue, 0, 0);
                        Gizmos.DrawLine(biasesPosition[i - 1], neuronsPosition[i][j]);
                    }
                }

                //Draw empty weights with their normal values 
                for (int i = 1; i < neuronsPosition.Length; i++)//start from the second layer** keep in mind
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                        for (int backNeuron = 0; backNeuron < neuronsPosition[i - 1].Length; backNeuron++)
                        {
                            float weightValue = weights[i - 1][j][backNeuron];
                            if (weightValue > 0)
                                Gizmos.color = new Color(0, 0, weightValue);
                            else
                                Gizmos.color = new Color(-weightValue, 0, 0);
                            Gizmos.DrawLine(neuronsPosition[i][j], neuronsPosition[i - 1][backNeuron]);

                        }

                //Draw Neurons
                Gizmos.color = emptyNeuron;
                for (int i = 0; i < neuronsPosition.Length; i++)
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                        Gizmos.DrawSphere(neuronsPosition[i][j], scale * 4000f);

                //Draw Biases
                Gizmos.color = biasColor;
                for (int i = 0; i < biasesPosition.Length; i++)
                {
                    Gizmos.DrawSphere(biasesPosition[i], scale * 4000f);
                }


            }
            catch { }
            callStatistic = false;
        }
        private void HeuristicEnvironmentSetup()
        {
            if (Environment == null)
                return;

            environmentInitialTransform = new List<PosAndRot>();
            GetAllTransforms(Environment.transform, ref environmentInitialTransform);
        }
        private void ResetEnvironmentToInitialPosition()
        {
            if (Environment == null)
                return;
            ApplyAllTransforms(ref Environment, environmentInitialTransform);
        }
        private Sample GetSampleFromData(string inputs, string expectedOuputs)
        {
            Sample newSample = new Sample();

            string[] inp = inputs.Split(',');
            string[] eouts = expectedOuputs.Split(',');

            newSample.expectedOutputs = new float[eouts.Length];
            newSample.inputs = new float[inp.Length];

            ConvertStrArrToFloatArr(inp, ref newSample.inputs);
            ConvertStrArrToFloatArr(eouts, ref newSample.expectedOutputs);
            return newSample;
        }
        private string[] GetLinesFromBatchList()
        {
            string[] lines = new string[samplesCollected.Count * 2];
            int i = 0;
            foreach (Sample sample in samplesCollected)
            {
                StringBuilder LINE = new StringBuilder();
                foreach (float item in sample.inputs)
                {
                    LINE.Append(item);
                    LINE.Append(",");
                }
                LINE.Length--;
                lines[i++] = LINE.ToString();
                LINE.Clear();
                foreach (float item in sample.expectedOutputs)
                {
                    LINE.Append(item);
                    LINE.Append(",");
                }
                LINE.Length--;
                lines[i++] = LINE.ToString();

            }
            return lines;
        }
        //-------------------------------------------FOR USE BY USER----------------------------------------------//
        /// <summary>
        /// Method to override in order to use Heuristic or Manual mode. Fulfill the ActionBuffer parameter
        /// by your keyboard/mouse inputs with any float values.
        /// <para>Method to use: SetAction(index, value)</para>
        /// </summary>
        /// <param name="actionsOut"></param>
        protected virtual void Heuristic(ref ActionBuffer actionsOut)
        {

        }
        /// <summary>
        /// Method to override to make your agent observe the environment. Fulfill the SensorBuffer with
        /// any values that resembles float type, like Vector3, Transform, Quaternion etc.
        /// <para>Note: The ActionBuffer is a float array. Depending on what kind of observation you append, it can occupy a different space size.
        /// For instance, a Vector3 has 3 floats, so it occupies 3, a Quaternion occupies 4 and so on.</para>
        /// <para>Method to use: AddObservation(Type observation)</para>
        /// </summary>
        /// <param name="sensorBuffer"></param>
        protected virtual void CollectObservations(ref SensorBuffer sensorBuffer)
        {

        }
        /// <summary>
        /// Method to override to make your agent do actions with respect to the observations. Extract each value from the buffer and assign actions for each one.
        /// <para>Methods to use: GetAction(index), GetBuffer(), GetIndexOfMaxValue() -> generally used aside SoftMax</para>
        /// </summary>
        /// <param name="actionBuffer"></param>
        protected virtual void OnActionReceived(in ActionBuffer actionBuffer)
        {

        }
        /// <summary>
        /// General purpose is to move scene objects if needed (getting object references through this script)
        /// <para>Auto called after EndAction() on Heuristic/Manual mode.</para>
        /// </summary>
        protected virtual void HeuristicOnSceneReset()
        {

        }

        /// <summary>
        /// Adds reward to the agent with Self behavior.
        /// <para>Can be added to Static agents when the 'evenIfActionEnded' parameter is true. False by default.</para>
        /// </summary>
        /// <param name="reward"></param>
        /// <param name="evenIfActionEnded">Force rewarding a static agent</param>
        public void AddReward(float reward, bool evenIfActionEnded = false)
        {
            if (behavior == BehaviorType.Manual || behavior == BehaviorType.Heuristic)
                return;
            if (evenIfActionEnded == false && behavior == BehaviorType.Static)
                return;
            if (network == null)
            {
                Debug.LogError("Cannot <color=#18de95>AddReward</color> because neural network is null");
                return;
            }
            network.AddFitness(reward);
        }
        /// <summary>
        /// Sets the reward if the agent with Self behavior.
        /// <para>Can force set to Static agents when the 'evenIfActionEnded' parameter is true.</para>
        /// </summary>
        /// <param name="reward"></param>
        /// <param name="evenIfActionEnded">Force rewarding a static agent. False by default.</param>
        public void SetReward(float reward, bool evenIfActionEnded = false)
        {
            if (behavior == BehaviorType.Manual || behavior == BehaviorType.Heuristic)
                return;
            if (evenIfActionEnded == false && behavior == BehaviorType.Static)
                return;
            if (network == null)
            {
                Debug.LogError("Cannot <color=#18de95>SetReward</color> because neural network is null");
                return;
            }
            network.SetFitness(reward);
        }
        /// <summary>
        /// Sets the behavior to static.
        /// <para>If the behavior was previously Manual or Heuristic, the entire scene resets.</para>
        /// </summary>
        public void EndAction()
        {
            if (behavior == BehaviorType.Self)
                behavior = BehaviorType.Static;
            else if (behavior == BehaviorType.Manual || behavior == BehaviorType.Heuristic)
            {
                ResetToInitialPosition();
                ResetEnvironmentToInitialPosition();
                HeuristicOnSceneReset();
            }
        }

        //--------------------------------------------POSITIONING-----------------------------------------------//
        public void ResetToInitialPosition()
        {
            parseCounter = 1;
            ApplyTransform(initialPosition[0]);
            ApplyChildsTransforms(gameObject, initialPosition);

        } //Only method used externaly

        private void GetAllTransforms()
        {
            parseCounter = 1;
            initialPosition.Add(new PosAndRot(transform.position, transform.localScale, transform.rotation));
            GetChildsTransforms(this.transform);
        }
        private void GetChildsTransforms(UnityEngine.Transform obj)
        {
            foreach (UnityEngine.Transform child in obj.transform)
            {
                PosAndRot tr = new PosAndRot(child.position, child.localScale, child.rotation);
                initialPosition.Add(tr);
                GetChildsTransforms(child);
            }
        }
        static void ApplyChildsTransforms(GameObject obj, in List<PosAndRot> list)
        {
            ///PARSE COUNTER USED SEPARATELY <IT MUST BE INITIALIZED WITH 0></IT>
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                GameObject child = obj.transform.GetChild(i).gameObject;
                ApplyTransformTo(ref child, list[parseCounter]);
                parseCounter++;
                ApplyChildsTransforms(child, list);
            }
        }
        private void ApplyTransform(PosAndRot trnsfrm)
        {
            this.transform.position = trnsfrm.position;
            this.transform.localScale = trnsfrm.scale;
            this.transform.rotation = trnsfrm.rotation;
        }
        static private void ApplyTransformTo(ref GameObject obj, in PosAndRot trnsfrm)
        {
            obj.transform.position = trnsfrm.position;
            obj.transform.localScale = trnsfrm.scale;
            obj.transform.rotation = trnsfrm.rotation;
        }

        #region ENVIRONMENT POSITIONING
        public void GetAllTransforms(UnityEngine.Transform obj, ref List<PosAndRot> inList)
        {
            parseCounter = 1;
            inList.Add(new PosAndRot(obj.position, obj.localScale, obj.rotation));
            GetChildsTransforms(ref inList, obj);
        }
        public void ApplyAllTransforms(ref GameObject obj, in List<PosAndRot> fromList)
        {
            parseCounter = 1;
            ApplyTransform(ref obj, fromList[0]);
            AddChildsInitialTransform(ref obj, fromList);
        }

        public void GetChildsTransforms(ref List<PosAndRot> list, UnityEngine.Transform obj)
        {
            foreach (UnityEngine.Transform child in obj)
            {
                PosAndRot tr = new PosAndRot(child.position, child.localScale, child.rotation);
                list.Add(new PosAndRot(child.position, child.localScale, child.rotation));
                GetChildsTransforms(ref list, child);
            }
        }
        public void AddChildsInitialTransform(ref GameObject obj, List<PosAndRot> list)
        {
            ///PARSE COUNTER USED SEPARATELY <IT MUST BE INITIALIZED WITH 0></IT>
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                GameObject child = obj.transform.GetChild(i).gameObject;
                ApplyTransform(ref child, list[parseCounter]);
                parseCounter++;
                AddChildsInitialTransform(ref child, list);
            }
        }
        public void ApplyTransform(ref GameObject obj, PosAndRot trnsfrm)
        {
            obj.transform.position = trnsfrm.position;
            obj.transform.localScale = trnsfrm.scale;
            obj.transform.rotation = trnsfrm.rotation;
        }
        #endregion


        //--------------------------------------SETTERS AND GETTERS--------------------------------------------//
        public void ForcedSetFitnessTo(float value)
        {
            this.network.SetFitness(0);
        }
        public float GetFitness()
        {
            if (network != null)
                return network.GetFitness();
            else return 0f;
        }
        public string GetPathWithName(string specificName = null)
        {
            //Creates a full path from streaming assets to a file with the parameter name
            StringBuilder pathsb = new StringBuilder();
            pathsb.Append(Application.streamingAssetsPath);
            pathsb.Append("/Neural_Networks/");

            if (!Directory.Exists(pathsb.ToString()))
                Directory.CreateDirectory(pathsb.ToString());
            if (specificName == null)
            {
                pathsb.Append("NetID");
                pathsb.Append(((int)this.gameObject.GetInstanceID()) * (-1));
                pathsb.Append(".txt");
            }
            else
            {
                pathsb.Append(specificName);
                pathsb.Append(".txt");
            }


            return pathsb.ToString();
        }
        private string GetHeuristicSamplesPath()
        {
            string path = Application.streamingAssetsPath;
            path += "/Heuristic_Samples/";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }
        public List<PosAndRot> GetInitialPosition()
        {
            return initialPosition;
        }

        //---------------------------------------------OPTIONAL---------------------------------------------------//
        void BUTTONSaveBrain()
        {
            if (SaveBrain == false)
                return;

            SaveBrain = false;
            if (network != null)
                NeuralNetwork.WriteBrain(network, GetPathWithName());
            else
            {
                //Set hyperparameters
                NeuralNetwork.activation = activationType;
                NeuralNetwork.outputActivation = outputActivationType;
                NeuralNetwork.initialization = initializationType;

                //reateBrain and Write it
                List<int> lay = new List<int>();
                lay.Add(sensorSize);

                if (hiddenLayers != null)
                    foreach (int neuronsNumber in hiddenLayers)
                    {
                        lay.Add(neuronsNumber);
                    }

                lay.Add(actionSize);
                this.network = new NeuralNetwork(lay.ToArray());
                NeuralNetwork.WriteBrain(network, GetPathWithName(networkModel == null ?
                                                                                         null : AssetDatabase.GetAssetPath(networkModel)));

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
                catch (System.Exception e)
                {
                    Debug.LogError(str[i] + " : " + e);
                }

            }
        }
    }
    public class TrainerBase : UnityEngine.MonoBehaviour
    {
        [Header("===== Models =====")]
        [Tooltip("Agent model gameObject used as the ai")] public GameObject agentModel;
        [Tooltip("@network model used to start the training with")] public TextAsset networkModel;
        [Tooltip("@resets the dynamic environmental object's positions")] public TrainingType interactionType = TrainingType.NotSpecified;

        [Space(20)]
        [Tooltip("The model used updates in the first next generation\n@tip: use a copy of the brain")] public bool resetBrainModelFitness = true;
        [Tooltip("@save networks of best Ai's before moving to the next generation.\n@number of saves = cbrt(Team Size).\n@folder: /Saves/.\n@last file saved is the best AI")] public bool saveBrains = false;

        [Space, Header("===== Training Properties =====\n@Base Settings")]
        [Range(3, 500), Tooltip("@number of clones used\n@more clones means faster reinforcement but slow performance")] public int teamSize = 10;//IT cannot be 1 or 2, otherwise strategies will not work (if there are not 3, strategy 2 causes trouble)
        [Range(1, 10), Tooltip("Episodes needed to run until passing to the next Generation\n@TIP: divide the reward given by this number")] public int episodesPerGeneration = 1;
        [Range(1, 1000), Tooltip("Total Episodes in this Training Session")] public int maxEpisodes = 100; private int currentEpisode = 1;
        [Range(1, 60), Tooltip("Maximum time allowed per Episode\n@don't confuse with 'per Generation'")] public float episodeLength = 25f; float timeLeft;

        [Space, Header("@Advanced Settings")]
        [Tooltip("@in the beggining use Strategy1.\n@if AI's performance decreases, switch to Strategy2.\n@finetune the final Brain using Strategy3.")]
        public TrainingStrategy trainingStrategy = TrainingStrategy.Strategy1;
        [Tooltip("@mutates the weights and biases following certain rules")]
        public MutationStrategy mutationStrategy = MutationStrategy.Classic;

        [Space, Header("===== Statistics Display =====")]
        [Tooltip("@finds and uses 1st ObjectOfType<Camera> \n@turn off any camera scripts")] public bool cameraFollowsBestAI = true; GameObject cam; bool isOrtographic; Vector3 perspectiveOffset;
        [Tooltip("Load a Canvas TMPro to watch the current performance of AI's")] public TMPro.TMP_Text Labels = null;
        [Tooltip("Load a Canvas RectTransform to watch a Gizmos graph in SceneEditor")] public RectTransform Graph = null;
        List<float> bestResults;//memorize best results for every episode
        List<float> averageResults;//memorize avg results for every episode




        private NeuralNetwork net;
        protected AI[] team;
        private GameObject[] Environments;

        private int currentEnvironment = 0;
        protected List<PosAndRot>[] environmentsInitialTransform;//Every item is the position of a single environment. The item is a list with positions of all environment items
        protected List<PosAndRot>[] agentsInitialTransform;//Every item is the position for a single environment. The item is the AI's initial position for environment i

        int parseCounter = 0;
        bool startTraining = true;


        protected virtual void Awake()
        {
            CreateDir();
            timeLeft = episodeLength;
            bestResults = new List<float>();
            averageResults = new List<float>();


            //Cam related
            cam = FindObjectOfType<Camera>().gameObject;
            if (cam.GetComponent<Camera>().orthographic == true)
                isOrtographic = true;
            else
            { isOrtographic = false; perspectiveOffset = cam.transform.position - agentModel.transform.position; }

            //deactivate other camera components like custom scripts
            MonoBehaviour[] comps = cam.GetComponents<MonoBehaviour>();
            foreach (var item in comps)
                item.enabled = false;
        }
        protected virtual void Start()
        {
            if (!TrainingPreparation())
            {
                startTraining = false;
                return;
            }
            EnvironmentSetup();
            SetupTeam();

            for (int i = 0; i < Environments.Length; i++)//do not modify this positions
                OnEpisodeBegin(ref Environments[i]);


        }
        protected virtual void Update()
        {
            NeuralNetwork.mutation = mutationStrategy;
            if (startTraining)
                Train();
        }
        protected virtual void LateUpdate()
        {
            if (cameraFollowsBestAI)
            {
                if (isOrtographic)
                    OrtographicCameraFollowsBestAI();
                else PerspectiveCameraFollowsBestAI();
            }
        }
        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------//
        //------------------------------------------TRAINING SETUP-----------------------------------------//
        void CreateDir()
        {
            if (!Directory.Exists(Application.streamingAssetsPath + "/Neural_Networks/"))
                Directory.CreateDirectory(Application.streamingAssetsPath + "/Neural_Networks/");
        }
        bool TrainingPreparation()
        {
            if (agentModel == null)
            {
                Debug.LogError("The training cannot start! Reason: <color=#f27602>No AI Model uploaded</color>");
                return false;
            }
            if (agentModel.GetComponent<Agent>() == null)
            {
                Debug.LogError("The training cannot start! Reason:  <color=#f27602>AI Model does not contain Agent component</color>");
                return false;
            }
            if (networkModel == null)
            {
                Debug.LogError("The training cannot start! Reason:  <color=#f27602>Network Model not uploaded</color>");
                return false;
            }
            net = new NeuralNetwork(networkModel.text);
            if (resetBrainModelFitness)
                net.SetFitness(0f);
            return true;
        }
        /// <summary>
        /// Requires base.SetupTeam() to be called in the beggining.
        /// <para>Can be overridden for pre-training setup, like coloring the agents differently.
        /// </para>
        /// <para>They are auto colored differently if they have a SpriteRenderer component.</para>
        /// </summary>
        void SetupTeam()
        {
            //Instatiate AI
            team = new AI[teamSize];
            agentModel.GetComponent<Agent>().behavior = BehaviorType.Static;
            if (interactionType == TrainingType.OneAgentPerEnvironment)
                for (int i = 0; i < Environments.Length; i++)
                {
                    Agent ag = (Agent)Environments[i].transform.GetComponentInChildren(typeof(Agent), true);
                    team[i].agent = ag.gameObject;
                    team[i].agent.SetActive(true);
                    team[i].script = ag;
                    team[i].fitness = 0f;
                }
            else
                for (int i = 0; i < team.Length; i++)
                {
                    GameObject member = Instantiate(agentModel, agentModel.transform.position, agentModel.transform.rotation);
                    team[i].agent = member;
                    team[i].agent.SetActive(true);
                    team[i].script = member.GetComponent<Agent>() as Agent;
                    team[i].fitness = 0f;
                }

            NeuralNetwork.activation = team[0].script.activationType;
            NeuralNetwork.outputActivation = team[0].script.outputActivationType;
            NeuralNetwork.initialization = team[0].script.initializationType;

            //Initialize AI
            for (int i = 0; i < team.Length; i++)
            {
                var script = team[i].script;
                script.network = new NeuralNetwork(networkModel.text);
                script.ForcedSetFitnessTo(0f);

                //Mutate Half Of them in the beggining
                if (i % 2 == 0)
                    script.network.MutateWeightsAndBiases();

                script.behavior = BehaviorType.Self;

            }


            ResetAgentsTransform();

            //Colorize AI's if possible
            foreach (var item in team)
            {
                if (item.agent.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                {
                    if (spriteRenderer == null)
                        break;
                    spriteRenderer.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                }
            }

            //Turn Off the model
            agentModel.SetActive(false);
            UpdateDisplay();
        }

        //--------------------------------------------TRAINING PROCESS--------------------------------------//
        void Train()
        {
            timeLeft -= Time.deltaTime;

            UpdateFitnessInArray();

            if (AreAllDead() || timeLeft <= 0)
                ResetEpisode();

            if (currentEpisode >= maxEpisodes)
            {
                SaveBrains();
                Debug.Log("<color=#027af2>Training Session Ended!</color>");
                foreach (var item in team)
                    item.script.behavior = BehaviorType.Static;
                startTraining = false;
            }
            UpdateDisplay();
        }
        /// <summary>
        /// Adds actions to the environment [objects] referenced to this script. Use Time.deltaTime if needed.
        /// </summary>
        bool AreAllDead()
        {
            foreach (AI item in team)
                if (item.script.behavior == BehaviorType.Self)
                    return false;
            return true;
        }
        protected void ResetEpisode()
        {
            for (int i = 0; i < team.Length; i++)
                OnEpisodeEnd(ref team[i]);//it makes sense to be here

            UpdateFitnessInArray();
            SortTeam();

            timeLeft = episodeLength;
            //Next Gen
            if (currentEpisode % episodesPerGeneration == 0)
            {
                //Graph Related
                bestResults.Add(team[team.Length - 1].fitness);
                averageResults.Add(FindAverageResult());

                if (saveBrains == true)
                    SaveBrains();
                switch (trainingStrategy)
                {
                    case TrainingStrategy.Strategy1:
                        NextGenStrategy1();
                        break;
                    case TrainingStrategy.Strategy2:
                        NextGenStrategy2();
                        break;
                    case TrainingStrategy.Strategy3:
                        NextGenStrategy3();
                        break;
                    default:
                        Debug.LogError("Training Strategy is NULL");
                        break;

                }
                ResetFitEverywhere();
            }
            if (interactionType != TrainingType.NotSpecified)
                NextEnvironment();
            ResetEnvironmentTransform();
            ResetAgentsTransform();



            //From static, move to self
            foreach (var item in team)
                item.script.behavior = BehaviorType.Self;

            currentEpisode++;


            for (int i = 0; i < Environments.Length; i++)//do not modify this positions
                OnEpisodeBegin(ref Environments[i]);

        }
        /// <summary>
        /// Adds actions after episode restting. Use-case: flags activations, environment repositioning etc.
        /// <para>This method is called for each Environment separately. In Environment transform, search for the object needed and modify it.</para>
        /// </summary>
        /// <param name="Environments">Environment gameObject</param>
        protected virtual void OnEpisodeBegin(ref GameObject Environment)
        {

        }
        /// <summary>
        /// Actions before episode resetting. Use-case: post-action rewards, when the agents become static.
        /// <para>This method is called for each AI separately.</para>
        /// <para>AI parameter has 3 different fields: agent, script and fitness. All are described by hovering over them.</para>
        /// </summary>
        /// <param name="ai">The Agent</param>
        protected virtual void OnEpisodeEnd(ref AI ai)
        {
            ///Is called at the beggining of ResetEpisode()
        }
        //----------------------------------------------ENVIRONMENTAL----------------------------------------//
        private void EnvironmentSetup()
        {
            // This function will search for a Start object that has the same tag as the AI.
            // If the AI has a Default tag, the first object in the environment will be considered Start and this will be a bug. **Always TAG AI's**
            if (interactionType == TrainingType.NotSpecified)
            {
                Environments = new GameObject[1];
                return;
            }

            try
            {
                Environments = GameObject.FindGameObjectsWithTag("Environment");
            }
            catch { Debug.LogError("Environment tag doesn't exist. Please create a tag called Environment and assign to an environment!"); interactionType = TrainingType.NotSpecified; return; }


            environmentsInitialTransform = new List<PosAndRot>[Environments.Length];
            agentsInitialTransform = new List<PosAndRot>[Environments.Length];
            for (int i = 0; i < Environments.Length; i++)
            {
                environmentsInitialTransform[i] = new List<PosAndRot>();
                agentsInitialTransform[i] = new List<PosAndRot>();
            }

            if (Environments.Length == 0)
            {
                Debug.Log("There is no Environment found. Make sure your environments have Environment tag.");
                return;
            }
            if (Environments.Length == 1)
            {

                //GetEnvironment transform
                GetAllTransforms(Environments[0].transform, ref environmentsInitialTransform[0]);

                //GetStart transform
                UnityEngine.Transform Start = null;
                foreach (Transform child in Environments[0].transform)
                {
                    if (child.GetComponent<Agent>() == true)
                    { Start = child; break; }
                }
                if (Start == null)//If the monoenvironment doesn't have a start, take as start the AIModel
                    GetAllTransforms(agentModel.transform, ref agentsInitialTransform[0]);
                else
                {
                    GetAllTransforms(Start, ref agentsInitialTransform[0]);
                    Start.gameObject.SetActive(false);
                }

            }
            else
            {
                for (int i = 0; i < Environments.Length; i++)
                {
                    GetAllTransforms(Environments[i].transform, ref environmentsInitialTransform[i]);

                    UnityEngine.Transform StartTransform = null;

                    foreach (Transform child in Environments[i].transform)
                    {
                        if (child.GetComponent<Agent>() == true)
                        { StartTransform = child; break; }
                    }

                    if (StartTransform == null)
                    {
                        Debug.Log("<color=red><color=#a9fc03>" + Environments[i].name + "</color> does not have an agent model inside.</color>");
                        return;
                    }

                    GetAllTransforms(StartTransform, ref agentsInitialTransform[i]);
                    StartTransform.gameObject.SetActive(false);
                }
                if (interactionType == TrainingType.MoreAgentsPerEnvironment)
                    episodesPerGeneration *= Environments.Length;
            }

            if (interactionType == TrainingType.OneAgentPerEnvironment)
                teamSize = Environments.Length;

        }
        private void NextEnvironment()
        {
            currentEnvironment++;
            if (currentEnvironment == Environments.Length)
                currentEnvironment = 0;
        }
        private void ResetEnvironmentTransform()
        {
            if (interactionType == TrainingType.NotSpecified)
                return;
            //SingleLayer Environment - reset only current env
            if (interactionType == TrainingType.MoreAgentsPerEnvironment)
                ApplyAllTransforms(ref Environments[currentEnvironment], in environmentsInitialTransform[currentEnvironment]);
            //MultiLayer Environment - reset all environments
            else
                for (int i = 0; i < Environments.Length; i++)
                    ApplyAllTransforms(ref Environments[i], in environmentsInitialTransform[i]);
        }
        private void ResetAgentsTransform()
        {
            if (interactionType == TrainingType.NotSpecified)
                for (int i = 0; i < team.Length; i++)
                    team[i].script.ResetToInitialPosition();
            else if (interactionType == TrainingType.MoreAgentsPerEnvironment)
                for (int i = 0; i < team.Length; i++)
                    ApplyAllTransforms(ref team[i].agent, in agentsInitialTransform[currentEnvironment]);
            else
                for (int i = 0; i < team.Length; i++)
                    ApplyAllTransforms(ref team[i].agent, in agentsInitialTransform[i]);
        }
        ///---------POSITIONING---------//
        internal void GetAllTransforms(UnityEngine.Transform obj, ref List<PosAndRot> inList)
        {
            parseCounter = 1;
            inList.Add(new PosAndRot(obj.position, obj.localScale, obj.rotation));
            GetChildsTransforms(ref inList, obj);
        }
        internal void ApplyAllTransforms(ref GameObject obj, in List<PosAndRot> fromList)
        {
            parseCounter = 1;
            ApplyTransform(ref obj, fromList[0]);
            AddChildsInitialTransform(ref obj, in fromList);
        }

        internal void GetChildsTransforms(ref List<PosAndRot> list, UnityEngine.Transform obj)
        {
            foreach (UnityEngine.Transform child in obj)
            {
                PosAndRot tr = new PosAndRot(child.position, child.localScale, child.rotation);
                list.Add(new PosAndRot(child.position, child.localScale, child.rotation));
                GetChildsTransforms(ref list, child);
            }
        }
        internal void AddChildsInitialTransform(ref GameObject obj, in List<PosAndRot> list)
        {
            ///PARSE COUNTER USED SEPARATELY <IT MUST BE INITIALIZED WITH 0></IT>
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                GameObject child = obj.transform.GetChild(i).gameObject;
                ApplyTransform(ref child, list[parseCounter]);
                parseCounter++;
                AddChildsInitialTransform(ref child, list);
            }
        }
        internal void ApplyTransform(ref GameObject obj, PosAndRot trnsfrm)
        {
            obj.transform.position = trnsfrm.position;
            obj.transform.localScale = trnsfrm.scale;
            obj.transform.rotation = trnsfrm.rotation;
        }
        //-----------------------------------------------STATISTICS---------------------------------------------//
        void OrtographicCameraFollowsBestAI()
        {
            Vector3 cameraPosition = cam.transform.position;
            Vector3 targetPosition = team[team.Length - 1].agent.transform.position;
            Vector3 desiredPosition = new Vector3(targetPosition.x, targetPosition.y, cameraPosition.z);

            float smoothness = 0.125f;
            Vector3 smoothPosition = Vector3.Lerp(cameraPosition, desiredPosition, smoothness);

            cam.transform.position = smoothPosition;
        }
        void PerspectiveCameraFollowsBestAI()
        {
            Vector3 cameraPosition = cam.transform.position;
            Vector3 targetPosition = team[team.Length - 1].agent.transform.position;
            Vector3 desiredPosition = targetPosition + perspectiveOffset;

            float smoothness = 0.0625f;
            Vector3 smoothPosition = Vector3.Lerp(cameraPosition, desiredPosition, smoothness);

            cam.transform.position = smoothPosition;
        }
        void UpdateDisplay()
        {
            //Update is called after every EpisodeReset
            if (Labels == null)
                return;

            SortTeam();
            string statColor;
            StringBuilder statData = new StringBuilder();

            {
                Color color = Color.Lerp(Color.green, Color.red, currentEpisode / maxEpisodes);
                Color32 color32 = new Color32();
                ColorConvertor.ConvertColorToColor32(color, ref color32);
                statColor = ColorConvertor.GetRichTextColorFromColor32(color32);
            }
            statData.Append("<b>|Episode: <color=" + statColor + ">");
            statData.Append(currentEpisode);
            statData.Append("</color>\n");

            statData.Append("<b>|Generation: ");
            statData.Append((currentEpisode - 1) / episodesPerGeneration);
            statData.Append("\n");
            {//Colorize
                Color tlcolor = Color.Lerp(Color.red, Color.green, timeLeft / episodeLength);
                Color32 tlcolor32 = new Color32();
                ColorConvertor.ConvertColorToColor32(tlcolor, ref tlcolor32);
                statColor = ColorConvertor.GetRichTextColorFromColor32(tlcolor32);
            }
            statData.Append("<b>|Timeleft: <color=" + statColor + ">");
            statData.Append(timeLeft.ToString("0.000"));
            statData.Append("</color>\n");

            statData.Append("|Goal: ");
            statData.Append(net.GetFitness().ToString("0.000"));
            statData.Append("</b>\n\n");
            for (int i = team.Length - 1; i >= 0; --i)
            {
                AI item = team[i];
                StringBuilder line = new StringBuilder();

                //Try COLORIZE
                bool hasColor = true;
                try
                {
                    Color color = item.agent.GetComponent<SpriteRenderer>().color;
                    Color32 color32 = new Color32();
                    ColorConvertor.ConvertColorToColor32(color, ref color32);
                    StringBuilder colorString = new StringBuilder();
                    colorString.Append("#");
                    colorString.Append(ColorConvertor.GetHexFrom(color32.r));
                    colorString.Append(ColorConvertor.GetHexFrom(color32.g));
                    colorString.Append(ColorConvertor.GetHexFrom(color32.b));

                    line.Append("<color=" + colorString.ToString() + ">");
                }
                catch { hasColor = false; }

                line.Append("ID: ");
                line.Append((((int)item.agent.GetInstanceID()) * (-1)).ToString());
                //IF COLORIZED
                if (hasColor)
                    line.Append("</color>");

                line.Append(" | Fitness: ");
                line.Append(item.script.GetFitness().ToString("0.000"));

                if (item.script.behavior == BehaviorType.Self)
                    line.Append(" | <color=green>@</color>");
                else line.Append(" | <color=red>X</color>");
                line.Append("\n");
                statData.AppendLine(line.ToString());
            }
            Labels.text = statData.ToString();
        }
        private void OnDrawGizmos()
        {
            //Draw Graph
            if (Graph == null || net == null)
                return;
            try
            {
                float goal = net.GetFitness();
                Gizmos.matrix = Graph.localToWorldMatrix;
                float xSize = Graph.rect.width;
                float ySize = Graph.rect.height / 2;

                Vector2 zero = new Vector2(-xSize / 2, 0);
                float zeroX = -xSize / 2;

                //Draw AXIS
                Gizmos.color = Color.white;
                Gizmos.DrawLine(zero, new Vector2(zeroX, ySize));//up
                Gizmos.DrawLine(zero, new Vector2(-zeroX, 0f));//right
                Gizmos.DrawSphere(zero, 5f);
                //Draw Arrows
                float arrowLength = 10f;
                //Y
                Gizmos.DrawLine(new Vector2(zeroX, ySize), new Vector2(zeroX - arrowLength, ySize - arrowLength));
                Gizmos.DrawLine(new Vector2(zeroX, ySize), new Vector2(zeroX + arrowLength, ySize - arrowLength));
                //X
                Gizmos.DrawLine(new Vector2(-zeroX, 0), new Vector2(-zeroX - arrowLength, -arrowLength));
                Gizmos.DrawLine(new Vector2(-zeroX, 0), new Vector2(-zeroX - arrowLength, +arrowLength));


                float xUnit = xSize / currentEpisode;
                float yUnit = ySize / goal;

                //Draw Best Dots
                Gizmos.color = Color.yellow;
                List<Vector3> pointsPositions = new List<Vector3>();
                pointsPositions.Add(zero);
                for (int i = 0; i < bestResults.Count; i++)
                {

                    float xPos;
                    float yPos;

                    xPos = zeroX + (i + 1) * xUnit * episodesPerGeneration; //episodesPerEvolution is added, otherwise the graph will remain to short on Xaxis
                    yPos = bestResults[i] * yUnit;   //fitness
                    Vector3 dotPos = new Vector3(xPos, yPos, 0f);
                    pointsPositions.Add(dotPos);
                    Gizmos.DrawSphere(dotPos, 5f);
                }

                //Draw Dots Connection
                Gizmos.color = Color.green;
                for (int i = 0; i < pointsPositions.Count - 1; i++)
                {
                    Gizmos.DrawLine(pointsPositions[i], pointsPositions[i + 1]);
                }



                pointsPositions.Clear();
                //Draw Average Dots
                pointsPositions.Add(zero);
                Gizmos.color = Color.blue;
                for (int i = 0; i < averageResults.Count; i++)
                {
                    float xPos;
                    float yPos;

                    xPos = zeroX + (i + 1) * xUnit * episodesPerGeneration;    //step
                    yPos = averageResults[i] * yUnit;   //fitness
                    Vector3 dotPos = new Vector3(xPos, yPos, 0f);
                    pointsPositions.Add(dotPos);
                    Gizmos.DrawSphere(dotPos, 5f);
                }
                //Draw Dots Connection
                Gizmos.color = Color.grey;
                for (int i = 0; i < pointsPositions.Count - 1; i++)
                {
                    Gizmos.DrawLine(pointsPositions[i], pointsPositions[i + 1]);
                }
            }
            catch { }
            //Draw Neural Network Shape
            try
            {
                float SCALE = .05f;
                Color emptyNeuron = Color.yellow;
                Color biasColor = Color.green;
                NeuralNetwork nety = team[team.Length - 1].script.network;
                try { emptyNeuron = team[team.Length - 1].agent.GetComponent<SpriteRenderer>().color; } catch { }


                int[] layers = nety.GetLayers();
                float[][][] weights = nety.GetWeights();
                float[][] biases = nety.GetBiases();

                Vector2[][] neuronsPosition = new Vector2[layers.Length][];//starts from up-left
                Vector2[] biasesPosition = new Vector2[layers.Length - 1];//one for each layer

                float xSize = Graph.rect.width;
                float ySize = Graph.rect.height;
                float maxNeuronsInLayers = layers.Max();
                float scale = 1 / (layers.Length * maxNeuronsInLayers) * SCALE;
                float xOffset = -xSize / 2;
                float yOffset = -ySize / 2;

                float layerDistanceUnit = xSize / (layers.Length - 1);
                float neuronDistanceUnit = ySize / (maxNeuronsInLayers) / 2;
                neuronDistanceUnit -= neuronDistanceUnit * 0.15f;//substract 10% to make it a bit smaller - also not substract 1  form maxNeuronsInLayers beacause is one more bias

                //FIND POSITIONS
                for (int layerNum = 0; layerNum < layers.Length; layerNum++)//take each layer individually
                {
                    //float layerYstartPose = (maxNeuronsInLayers - layers[layerNum]) / 2 * neuronDistanceUnit;
                    float layerYStartPose = -(maxNeuronsInLayers - layers[layerNum]) / 2 * neuronDistanceUnit - 50f;//substract 30f to not interact with the graph
                    neuronsPosition[layerNum] = new Vector2[layers[layerNum]];
                    for (int neuronNum = 0; neuronNum < layers[layerNum]; neuronNum++)
                        neuronsPosition[layerNum][neuronNum] = new Vector2(layerNum * layerDistanceUnit + xOffset, layerYStartPose - neuronNum * neuronDistanceUnit);
                    if (layerNum < layers.Length - 1)
                        biasesPosition[layerNum] = new Vector2(layerNum * layerDistanceUnit + xOffset, layerYStartPose - layers[layerNum] * neuronDistanceUnit);
                }

                //Draw biases weights with their normal values
                for (int i = 1; i < neuronsPosition.Length; i++)
                {
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                    {
                        float weightValue = biases[i][j];
                        if (weightValue > 0)
                            Gizmos.color = new Color(0, 0, weightValue);
                        else Gizmos.color = new Color(-weightValue, 0, 0);
                        Gizmos.DrawLine(biasesPosition[i - 1], neuronsPosition[i][j]);
                    }
                }

                //Draw empty weights with their normal values 
                for (int i = 1; i < neuronsPosition.Length; i++)//start from the second layer** keep in mind
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                        for (int backNeuron = 0; backNeuron < neuronsPosition[i - 1].Length; backNeuron++)
                        {
                            float weightValue = weights[i - 1][j][backNeuron];
                            if (weightValue > 0)
                                Gizmos.color = new Color(0, 0, weightValue);
                            else
                                Gizmos.color = new Color(-weightValue, 0, 0);
                            Gizmos.DrawLine(neuronsPosition[i][j], neuronsPosition[i - 1][backNeuron]);

                        }

                //Draw Neurons
                Gizmos.color = emptyNeuron;
                for (int i = 0; i < neuronsPosition.Length; i++)
                    for (int j = 0; j < neuronsPosition[i].Length; j++)
                        Gizmos.DrawSphere(neuronsPosition[i][j], scale * 4000f);

                //Draw Biases
                Gizmos.color = biasColor;
                for (int i = 0; i < biasesPosition.Length; i++)
                {
                    Gizmos.DrawSphere(biasesPosition[i], scale * 4000f);
                }


            }
            catch { }
        }
        float FindAverageResult()
        {
            float result = 0f;
            foreach (AI aI in team)
            {
                result += aI.fitness;
            }
            return result / team.Length;
        }
        //--------------------------------------------TRAINING STRATEGY----------------------------------//
        void NextGenStrategy1()
        {
            /// <summary>
            /// Half worst AI's are replaced with the a single copy of half best AI's, only the copy is mutated
            /// </summary>
            SortTeam();
            //BUILD STATISTIC
            StringBuilder statistic = new StringBuilder();
            statistic.Append("Step: ");
            statistic.Append(currentEpisode);
            statistic.Append(" TEAM: <color=#4db8ff>");
            for (int i = team.Length - 1; i >= 0; i--)
            {
                if (i == team.Length / 2 - 1)
                    statistic.Append(" |</color><color=red>");

                statistic.Append(" | ");
                statistic.Append(team[i].fitness);
            }
            statistic.Append(" |</color>");
            float thisGenerationBestFitness = team[team.Length - 1].fitness;
            if (thisGenerationBestFitness < this.net.GetFitness())
            {
                statistic.Append("\n                    Evolution - NO  | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" < ");
                statistic.Append(this.net.GetFitness());
            }
            else
            {
                statistic.Append("\n                    Evolution - YES | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" > ");
                statistic.Append(this.net.GetFitness());
                //update ModelBrain
                net = new NeuralNetwork(team[team.Length - 1].script.network);
                NeuralNetwork.WriteBrain(in net, AssetDatabase.GetAssetPath(networkModel));

            }
            Debug.Log(statistic.ToString());


            //BUILD NEXT GENERATION
            int halfCount = team.Length / 2;
            if (team.Length % 2 == 0)//If Even team Size
                for (int i = 0; i < halfCount; i++)
                {
                    var script = team[i].script;
                    script.network = new NeuralNetwork(team[i + halfCount].script.network);
                    script.network.MutateWeightsAndBiases();
                }
            else
                for (int i = 0; i <= halfCount; i++)
                {
                    var script = team[i].script;
                    script.network = new NeuralNetwork(team[i + halfCount].script.network);
                    script.network.MutateWeightsAndBiases();
                }
        }
        void NextGenStrategy2()
        {
            /// <summary>
            /// 1/3 of the AI's (the worst) receive best brain and get mutated, for the rest 2/3 the first strategy applies
            /// </summary>
            SortTeam();
            //BUILD STATISTIC
            StringBuilder statistic = new StringBuilder();
            statistic.Append("Step: ");
            statistic.Append(currentEpisode);
            statistic.Append(" TEAM: <color=#4db8ff>");

            int somevar = team.Length % 3;
            if (somevar != 0)
                somevar = 1;
            for (int i = team.Length - 1; i >= 0; i--)
            {
                if (i == team.Length - team.Length / 3 - somevar - 1)
                    statistic.Append(" |</color><color=red>");
                else if (i == team.Length / 3 - 1)
                    statistic.Append(" |</color><color=#90a2a2>");
                statistic.Append(" | ");
                statistic.Append(team[i].fitness);
            }
            statistic.Append(" |</color>");
            float thisGenerationBestFitness = team[team.Length - 1].fitness;
            if (thisGenerationBestFitness < this.net.GetFitness())
            {
                statistic.Append("\n                    Evolution - NO  | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" < ");
                statistic.Append(this.net.GetFitness());
            }
            else
            {
                statistic.Append("\n                    Evolution - YES | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" > ");
                statistic.Append(this.net.GetFitness());
                //update ModelBrain
                net = new NeuralNetwork(team[team.Length - 1].script.network);
                NeuralNetwork.WriteBrain(in net, AssetDatabase.GetAssetPath(networkModel));
            }
            Debug.Log(statistic.ToString());

            for (int i = 0; i <= team.Length / 3; i++)
            {
                var script = team[i].script;
                script.network = new NeuralNetwork(net);
                script.network.MutateWeightsAndBiases();
            }
            int mod = team.Length % 3;
            if (mod == 0)
                for (int i = team.Length / 3; i < team.Length / 3 * 2; i++)
                {
                    var script = team[i].script;
                    script.network = new NeuralNetwork(team[i + team.Length / 3].script.network);
                }
            else if (mod == 1)
                for (int i = team.Length / 3; i <= team.Length / 3 * 2; i++)
                {
                    var script = team[i].script;
                    script.network = new NeuralNetwork(team[i + team.Length / 3].script.network);
                }
            else if (mod == 2)
                for (int i = team.Length / 3; i <= team.Length / 3 * 2; i++)
                {
                    var script = team[i].script;
                    script.network = new NeuralNetwork(team[i + team.Length / 3 + 1].script.network);
                }
        }
        void NextGenStrategy3()
        {
            /// <summary>
            /// Best AI is reproduced, all of his clones are mutated
            /// </summary>
            SortTeam();
            //BUILD STATISTIC
            StringBuilder statistic = new StringBuilder();
            statistic.Append("Step: ");
            statistic.Append(currentEpisode);

            //Place best AI in yellow
            statistic.Append(" TEAM: <color=#e6e600>");
            statistic.Append(" | ");
            statistic.Append(team[team.Length - 1].fitness);

            //Add rest of Ai's with grey
            statistic.Append(" |</color><color=#4db8ff>");
            for (int i = team.Length - 2; i >= 0; i--)
            {
                statistic.Append(" | ");
                statistic.Append(team[i].fitness);
            }
            statistic.Append(" |</color>");

            float thisGenerationBestFitness = team[team.Length - 1].fitness;
            if (thisGenerationBestFitness < this.net.GetFitness())
            {
                statistic.Append("\n                    Evolution - NO  | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" < ");
                statistic.Append(this.net.GetFitness());
            }
            else
            {
                statistic.Append("\n                    Evolution - YES | This generation Max Fitness: ");
                statistic.Append(thisGenerationBestFitness);
                statistic.Append(" > ");
                statistic.Append(this.net.GetFitness());
                //update ModelBrain
                net = new NeuralNetwork(team[team.Length - 1].script.network);
                NeuralNetwork.WriteBrain(in net, AssetDatabase.GetAssetPath(networkModel));
            }
            Debug.Log(statistic.ToString());

            NeuralNetwork bestAINet = team[team.Length - 1].script.network;
            for (int i = 0; i < team.Length - 1; i++)
            {
                var script = team[i].script;
                script.network = new NeuralNetwork(bestAINet);
                script.network.MutateWeightsAndBiases();
            }
        }
        //-------------------------------------------------SORTING------------------------------------//
        void SortTeam()
        {//InsertionSort
            for (int i = 1; i < team.Length; i++)
            {
                var key = team[i];
                int j = i - 1;
                while (j >= 0 && team[j].fitness > key.fitness)
                {
                    team[j + 1] = team[j];
                    j--;
                }
                team[j + 1] = key;
            }
        }
        //------------------------------------------COMPLEMENTARY METHODS-----------------------------------// 
        private void ResetFitEverywhere()
        {
            for (int i = 0; i < team.Length; i++)
            {
                team[i].script.ForcedSetFitnessTo(0f);
                team[i].fitness = 0f;
            }
        }
        private void UpdateFitnessInArray()
        {
            //Update fitness in team probArr
            for (int i = 0; i < team.Length; i++)
                team[i].fitness = team[i].script.GetFitness();
        }
        //-------------------------------------------------BUTTONS------------------------------------------//
        void SaveBrains()
        {
            saveBrains = false;

            //mainDir is the main Saves directory
            //saveDir is the directory made for this specific save, it is included in the main Saves directory
            string mainDir = Application.streamingAssetsPath + "/Saves";

            while (true)
            {
                string xmainDir = mainDir + "/Save_" + UnityEngine.Random.Range(100, 1000).ToString();
                if (!Directory.Exists(xmainDir))
                {
                    Directory.CreateDirectory(xmainDir);
                    mainDir = xmainDir;
                    break;
                }
            }

            int howMany = (int)((float)team.Length - Mathf.Pow(team.Length, 1f / 3f));

            Mathf.Pow(team.Length, 0.33f);
            for (int i = howMany; i <= team.Length - 1; i++)
            {
                string name = "/Ag[" + i + "]_Fit[" + team[i].script.GetFitness().ToString("0.00") + "].txt";
                NeuralNetwork net = new NeuralNetwork(team[i].script.network);//Here was made a copy due to some weird write access error
                NeuralNetwork.WriteBrain(net, (mainDir + name));
            }
            string color = ColorConvertor.GetRichTextColorFromColor32(new Color32((byte)0, (byte)255, (byte)38, (byte)1));

            StringBuilder message = new StringBuilder();
            message.Append("<color=");
            message.Append(color);
            message.Append(">");
            message.Append((team.Length - howMany));
            message.Append(" neural networks have been saved in </color><i>");
            message.Append(mainDir);
            message.Append("</i>");
            Debug.Log(message.ToString());
        }
    }
    internal readonly struct Functions
    {
        internal readonly struct Activation
        {
            static public float ActivationFunctionBinaryStep(float value)
            {
                if (value < 0)
                    return 0;
                else return 1;
            }
            static public float ActivationFunctionSigmoid(float value)
            {
                //values range [0,1]
                // Function is x = 1/(1 + e^(-x))
                return (float)1f / (1f + Mathf.Exp(-value));
            }
            static public float ActivationFunctionTanh(float value)
            {
                return (float)System.Math.Tanh((double)value);
                /*
                 //Other variant is to shift the sigmoid function
                   return (float)2f / (1f + Mathf.Exp(-2*value)) - 1;


                 */
            }
            static public float ActivationFunctionReLU(float value)
            {
                return Mathf.Max(0, value);
            }
            static public float ActivationFunctionLeakyReLU(float value, float alpha = 0.2f)
            {
                if (value > 0)
                    return value;
                else return value * alpha;
            }
            static public float ActivationFunctionSiLU(float value)
            {
                return value * ActivationFunctionSigmoid(value);
            }
            static public void ActivationFunctionSoftMax(ref float[] values)
            {
                float sum = 0f;
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Mathf.Exp(values[i]);
                    sum += values[i];
                }
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] /= sum;
                }
            }
        }
        internal readonly struct Derivatives
        {
            static public float DerivativeTanh(float value)
            {
                return 1f - (float)Math.Pow(Math.Tanh(value), 2);
            }
            static public float DerivativeSigmoid(float value)
            {
                return Activation.ActivationFunctionSigmoid(value) * (1 - Activation.ActivationFunctionSigmoid(value));
            }
            static public float DerivativeBinaryStep(float value)
            {
                return 0;
            }
            static public float DerivativeReLU(float value)
            {
                if (value < 0)
                    return 0;
                else return 1;
            }
            static public float DerivativeLeakyReLU(float value, float alpha = 0.2f)
            {
                if (value < 0)
                    return alpha;
                else return 1;
            }
            static public float DerivativeSiLU(float value)
            {
                return (1 + Mathf.Exp(-value) + value * Mathf.Exp(-value)) / Mathf.Pow((1 + Mathf.Exp(-value)), 2);
                //return ActivationFunctionSigmoid(value) * (1 + value * (1 - ActivationFunctionSigmoid(value))); -> works the same
            }
            static public void DerivativeSoftMax(ref float[] values)
            {
                float sum = 0f;

                foreach (float item in values)
                    sum += Mathf.Exp(item);


                for (int i = 0; i < values.Length; i++)
                {
                    float ePowI = Mathf.Exp(values[i]);
                    values[i] = (ePowI * sum - ePowI * ePowI) / (sum * sum);
                }
            }
        }
        internal readonly struct Cost
        {
            static public float Quadratic(float outputActivation, float expectedOutput)
            {
                float error = outputActivation - expectedOutput;
                return error * error;
            }
            static public float QuadraticDerivative(float outputActivation, float expectedOutput)
            {
                return 2 * (outputActivation - expectedOutput);
            }
            static public float Absolute(float outputActivation, float expectedOutput)
            {
                return Mathf.Abs(outputActivation - expectedOutput);
            }
            static public float AbsoluteDerivative(float outputActivation, float expectedOutput)
            {
                if ((outputActivation - expectedOutput) > 0)
                    return 1;
                else return -1;
            }
            static public float CrossEntropy(float outputActivation, float expectedOutput)
            {
                double v = (expectedOutput == 1) ? -System.Math.Log(outputActivation) : -System.Math.Log(1 - outputActivation);
                return double.IsNaN(v) ? 0 : (float)v;
            }
            static public float CrossEntropyDerivative(float outputActivation, float expectedOutput)
            {
                if (outputActivation == 0 || outputActivation == 1)
                    return 0;
                return (-outputActivation + expectedOutput) / (outputActivation * (outputActivation - 1));
            }
        }
        internal readonly struct Mutation
        {
            static public void ClassicMutation(ref float weight)
            {
                float randNum = UnityEngine.Random.Range(0f, 10f);

                if (randNum <= 2f)//20% chance of flip sign of the weightOrBias
                {
                    weight *= -1f;
                }
                else if (randNum <= 4f)//20% chance of fully randomize weightOrBias
                {
                    weight = UnityEngine.Random.Range(-.5f, .5f);
                }
                else if (randNum <= 6f)//20% chance of increase to 100 - 200 %
                {
                    float factor = UnityEngine.Random.value + 1f;
                    weight *= factor;
                }
                else if (randNum <= 8f)//20% chance of decrease in range 0 - 100 %
                {
                    float factor = UnityEngine.Random.value;
                    weight *= factor;
                }
                else
                {
                }//20% chance of NO MUTATION

            }
            static public void LightPercentageMutation(ref float weight)
            {
                //increase/decrease all to a max of 50%
                float sign = UnityEngine.Random.value;
                float factor;
                if (sign > .5f)
                {
                    factor = UnityEngine.Random.Range(1f, 1.5f);
                }
                else
                {
                    factor = UnityEngine.Random.Range(.5f, 1f);
                }
                weight *= factor;
            }
            static public void StrongPercentagegMutation(ref float weight)
            {
                //increase/decrease all to a max of 100%

                float sign = UnityEngine.Random.value;
                float factor;
                if (sign > .5f)//increase
                {
                    factor = UnityEngine.Random.value + 1f;
                }
                else//decrease
                {
                    factor = UnityEngine.Random.value;

                }
                weight *= factor;

            }
            static public void LightValueMutation(ref float weight)
            {
                // + 0 -> .5f or  - 0 -> .5f
                float randNum = UnityEngine.Random.Range(-.5f, .5f);
                weight += randNum;
            }
            static public void StrongValueMutation(ref float weight)
            {
                float randNum = UnityEngine.Random.Range(-1f, 1f);
                weight += randNum;
            }
            static public void ChaoticMutation(ref float weight)
            {
                float chance = UnityEngine.Random.value;
                if (chance < .125f)
                    weight = Functions.Initialization.RandomValueInCustomDeviationDistribution(0.15915f, 2f, 0.3373f);
                else if (chance < .3f)
                    ClassicMutation(ref weight);
                else if (chance < .475f)
                    LightPercentageMutation(ref weight);
                else if (chance < .65f)
                    StrongPercentagegMutation(ref weight);
                else if (chance < .825f)
                    LightValueMutation(ref weight);
                else
                    StrongValueMutation(ref weight);

            }
        }
        internal readonly struct Initialization
        {
            /// <summary>
            /// Return a random value [-1,1] != 0
            /// </summary>
            /// <returns></returns>
            static public float RandomValue()
            {
                if (UnityEngine.Random.value > 0.5f)
                    return UnityEngine.Random.value;
                else
                    return -UnityEngine.Random.value;
            }
            static public float RandomValueInCustomDeviationDistribution(float l, float k, float z)
            {
                float x = UnityEngine.Random.value;
                float sign = UnityEngine.Random.value;
                if (sign > .5f)
                    return (float)Mathf.Pow(-Mathf.Log(2f * l * Mathf.PI * Mathf.Pow(x, 2f)) * z, 1f / k);
                else
                    return (float)-Mathf.Pow(-Mathf.Log(2f * l * Mathf.PI * Mathf.Pow(x, 2f)) * z, 1f / k);


            }
            static public float RandomInNormalDistribution(System.Random rng, float mean, float standardDeviation)
            {
                float x1 = (float)(1 - rng.NextDouble());
                float x2 = (float)(1 - rng.NextDouble());

                float y1 = Mathf.Sqrt(-2.0f * Mathf.Log(x1)) * Mathf.Cos(2.0f * (float)Math.PI * x2);
                return y1 * standardDeviation + mean;
            }

        }
        internal readonly struct ArrayConversion
        {
            static public void ConvertStrArrToIntArr(string[] str, ref int[] arr)
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
            static public void ConvertStrArrToFloatArr(string[] str, ref float[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    try
                    {
                        arr[i] = float.Parse(str[i]);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError(str[i] + " : " + e);
                    }

                }
            }
        }

    }
    internal readonly struct ColorConvertor
    {
        public static void ConvertColorToColor32(Color color, ref Color32 color32)
        {
            color32.r = System.Convert.ToByte(color.r * 255f);
            color32.g = System.Convert.ToByte(color.g * 255f);
            color32.b = System.Convert.ToByte(color.b * 255f);
            color32.a = System.Convert.ToByte(color.a * 255f);
        }
        public static string GetRichTextColorFromColor32(Color32 color)
        {
            string clr = "#";
            clr += GetHexFrom(color.r);
            clr += GetHexFrom(color.g);
            clr += GetHexFrom(color.b);
            return clr;
        }
        public static string GetHexFrom(int value)
        {
            ///The format of the Number is returned in XX Format
            int firstValue = value;

            StringBuilder hexCode = new StringBuilder();
            int remainder;

            while (value > 0)
            {
                remainder = value % 16;
                value -= remainder;
                value /= 16;

                hexCode.Append(GetHexDigFromIntDig(remainder));
            }
            if (firstValue <= 15)
                hexCode.Append("0");
            if (firstValue == 0)//Case 0, we need to return 00
                hexCode.Append("0");

            string hex = hexCode.ToString();
            ReverseString(ref hex);
            return hex;
        }
        public static string GetHexDigFromIntDig(int value)
        {
            if (value < 0 || value > 15)
            {
                Debug.LogError("Value Parsed is not a Digit in HexaDecimal");
                return null;
            }
            if (value < 10)
                return value.ToString();
            else if (value == 10)
                return "A";
            else if (value == 11)
                return "B";
            else if (value == 12)
                return "C";
            else if (value == 13)
                return "D";
            else if (value == 14)
                return "E";
            else if (value == 15)
                return "F";
            else return null;
        }
        public static void ReverseString(ref string str)
        {
            char[] charArray = str.ToCharArray();
            System.Array.Reverse(charArray);
            str = new string(charArray);
        }
    }

    public struct AI
    {
        /// <summary>
        /// Agent gameobject.
        /// </summary>
        public GameObject agent;
        /// <summary>
        /// Agent script component of your agent.
        /// </summary>
        public Agent script;
        /// <summary>
        /// Current agent fitness of your agent.
        /// </summary>
        public float fitness;
    }
    public struct PosAndRot
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public PosAndRot(Vector3 pos, Vector3 scl, Quaternion rot)
        {
            position = pos;
            scale = scl;
            rotation = rot;
        }
        public PosAndRot(UnityEngine.Transform transform)
        {
            position = transform.position;
            scale = transform.localScale;
            rotation = transform.rotation;
        }
    }
    public struct SensorBuffer
    {
        private float[] buffer;
        private int sizeIndex;
        public SensorBuffer(int capacity)
        {
            buffer = new float[capacity];
            for (int i = 0; i < capacity; i++)
                buffer[i] = 0;
            sizeIndex = 0;
        }
        /// <summary>
        /// Returns the array that contains all the input values .
        /// </summary>
        /// <returns>float[] with all values</returns>
        public float[] GetBuffer()
        {
            return buffer;
        }
        public int GetBufferCapacity()
        {
            if (buffer == null)
                return 0;
            else return buffer.Length;
        }


        /// <summary>
        /// Appends a float value to the SensorBuffer.
        /// </summary>
        /// <param name="observation1">Value of the observation</param>
        public void AddObservation(float observation1)
        {
            if (sizeIndex == buffer.Length)
            {
                Debug.Log("SensorBuffer is full. Increase the space size or remove this observation.");
                return;
            }
            buffer[sizeIndex++] = observation1;
        }
        /// <summary>
        ///  Appends an int value to the SensorBuffer.
        /// </summary>
        /// <param name="observation1">Value of the observation</param>
        public void AddObservation(int observation1)
        {
            if (sizeIndex == buffer.Length)
            {
                Debug.Log("SensorBuffer is full. Increase the space size or remove this observation.");
                return;
            }
            buffer[sizeIndex++] = observation1;
        }
        /// <summary>
        /// Appends an unsigned int value to the SensorBuffer.
        /// </summary>
        /// <param name="observation1">Value of the observation</param>
        public void AddObservation(uint observation1)
        {
            if (sizeIndex == buffer.Length)
            {
                Debug.Log("SensorBuffer is full. Increase the space size or remove this observation.");
                return;
            }
            buffer[sizeIndex++] = observation1;
        }
        /// <summary>
        /// Appends a Vector2 value to the SensorBuffer.
        /// </summary>
        /// <param name="observation2">Value of the observation</param>
        public void AddObservation(Vector2 observation2)
        {
            if (buffer.Length - sizeIndex < 2)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Vector2 observation of size 2 is too large.");
                return;
            }
            buffer[sizeIndex++] = observation2.x;
            buffer[sizeIndex++] = observation2.y;
        }
        /// <summary>
        /// Appends a Vector3 value to the SensorBuffer.
        /// </summary>
        /// <param name="observation3">Value of the observation</param>
        public void AddObservation(Vector3 observation3)
        {

            if (buffer.Length - sizeIndex < 3)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Vector3 observation of size 3 is too large.");
                return;
            }
            buffer[sizeIndex++] = observation3.x;
            buffer[sizeIndex++] = observation3.y;
            buffer[sizeIndex++] = observation3.z;
        }
        /// <summary>
        /// Appends a Vector4 value to the SensorBuffer.
        /// </summary>
        /// <param name="observation4">Value of the observation</param>
        public void AddObservation(Vector4 observation4)
        {

            if (buffer.Length - sizeIndex < 4)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Vector4 observation of size 4 is too large.");
                return;
            }

            buffer[sizeIndex++] = observation4.x;
            buffer[sizeIndex++] = observation4.y;
            buffer[sizeIndex++] = observation4.z;
            buffer[sizeIndex++] = observation4.w;
        }
        /// <summary>
        /// Appends a Quaternion value to the SensorBuffer.
        /// </summary>
        /// <param name="observation4">Value of the observation</param>
        public void AddObservation(Quaternion observation4)
        {
            if (buffer.Length - sizeIndex < 4)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Quaternion observation of size 4 is too large.");
                return;
            }
            buffer[sizeIndex++] = observation4.x;
            buffer[sizeIndex++] = observation4.y;
            buffer[sizeIndex++] = observation4.z;
            buffer[sizeIndex++] = observation4.w;
        }
        /// <summary>
        /// Appends a Transform value to the SensorBuffer.
        /// </summary>
        /// <param name="observation10">Value of the observation</param>
        public void AddObservation(UnityEngine.Transform obsevation10)
        {
            if (buffer.Length - sizeIndex < 10)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Transform observation of size 10 is too large.");
                return;
            }
            AddObservation(obsevation10.position);
            AddObservation(obsevation10.localScale);
            AddObservation(obsevation10.rotation);
        }
        /// <summary>
        /// Appends an array of float values to the SensorBuffer.
        /// </summary>
        /// <param name="observations">Values of the observations</param>
        public void AddObservation(float[] observations)
        {
            if (buffer.Length - sizeIndex < observations.Length)
            {
                Debug.Log("SensorBuffer available space is " + (buffer.Length - sizeIndex) + ". Float array observations is too large.");
                return;
            }
            foreach (var item in observations)
            {
                AddObservation(item);
            }
        }
    }
    public struct ActionBuffer
    {
        private float[] buffer;
        public ActionBuffer(float[] actions)
        {
            buffer = actions;
        }
        public ActionBuffer(int capacity)
        {
            buffer = new float[capacity];
        }

        /// <summary>
        /// Get the buffer array with every action values.
        /// <para>Can be used instead of using GetAction() method.</para>
        /// </summary>
        /// <returns>float[] copy of the buffer</returns>
        public float[] GetBuffer()
        {
            return buffer;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns>Total actions number</returns>
        public int GetBufferCapacity()
        {
            return buffer != null ? buffer.Length : 0;
        }
        /// <summary>
        /// Returns the value from the index parameter.
        /// </summary>
        /// <param name="index">The index of the action from ActionBuffer.</param>
        /// <returns>float</returns>
        public float GetAction(uint index)
        {
            try
            {
                return buffer[index];
            }
            catch { Debug.LogError("Action index out of range."); }
            return 0;
        }
        /// <summary>
        /// Sets the action from ActionBuffer with a specific value.
        /// </summary>
        /// <param name="index">The index of the action from ActionBuffer</param>
        /// <param name="action1">The value of the action to be set</param>
        public void SetAction(uint index, float action1)
        {
            buffer[index] = action1;
        }
        /// <summary>
        /// Returns the index of the max value from ActionBuffer.
        /// <para>Usually used when SoftMax is the output activation function.</para>
        /// </summary>
        /// <returns>The index or -1 if all elements are equal.</returns>
        public int GetIndexOfMaxValue()
        {
            float max = float.MinValue;
            int index = -1;
            bool equal = true;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i > 0 && buffer[i] != buffer[i - 1])
                    equal = false;

                if (buffer[i] > max)
                {
                    max = buffer[i];
                    index = i;
                }
            }
            return equal == true ? -1 : index;

        }
    }
    internal struct Node
    {
        public float valueIn,//before activation
                     valueOut,//after activation
                     costValue;
    }
    internal struct Sample
    {
        public float[] inputs;
        public float[] expectedOutputs;
    }

    public enum TrainingType
    {
        [Tooltip("@static environment\n@single environment\n@multiple agents\n@agent model is used as a starting position")]
        NotSpecified,

        //Agents overlap eachother, environmental objects are common
        [Tooltip("@agents are overlapping in the same environment(s)\n@if no starting model inside the environment, agent model is used as a starting position")]
        MoreAgentsPerEnvironment,

        //Agents train separately, environmental objects are personal for each agent
        [Tooltip("@one agent per each environment found\n@usually used to let just 1 agent interact with the environment")]
        OneAgentPerEnvironment,
    }

    public enum BehaviorType
    {
        [Tooltip("Doesn't move")]
        Static,
        [Tooltip("Can move only by user input\n@override Heuristic()\n@override OnActionReceived()")]
        Manual,
        [Tooltip("Moves independently\n@override CollectObservations()\n@override OnActionReceived()")]
        Self,
        [Tooltip("Trains by user input\nNo Trainer required\n@override CollectObservations()\n@override Heuristic()\n@override OnActionReceived()")]
        Heuristic,


    }
    public enum TrainingStrategy
    {
        [Tooltip("@(1/2) best AI reproduce\n@(1/2) copies + mutated")]
        Strategy1,
        [Tooltip("@(1/3) best AI reproduce\n@(1/3)copies + mutation\n@(1/3) worst AI get best brain + mutation")]
        Strategy2,
        [Tooltip("@(1)best AI reproduce\n@(Rest) copies + mutation")]
        Strategy3,

    }
    public enum MutationStrategy
    {
        [Tooltip("20% -> * (-1) " +
            "\n20% -> +.5f | -.5f" +
            "\n20% -> + 0%~100%" +
            "\n20% -> - 0%~100%" +
            "\n20% -> no mutation")]
        Classic,
        [Tooltip("50% -> -(0%~50%)" +
            "\n50% -> +(0%~50%)" +
            "\n@no sign change" +
            "\n@best for finetuning")]
        LightPercentage,
        [Tooltip("50% -> -(0f~.5f)" +
            "\n50% -> +(0f~.5f)")]
        LightValue,
        [Tooltip("50% -> -(0%~100%)" +
            "\n50% -> +(0%~100%)" +
            "\n@no sign change" +
            "\n@best for deeptuning")]
        StrongPercentage,
        [Tooltip("50% -> -(0f~1f)" +
                  "\n50% -> +(0f~1f)")]
        StrongValue,
        [Tooltip("12.5% -> New value from normal distribution" +
            "\n17.5% -> Classic mutation" +
            "\n17.5% -> LightPercentage mutation" +
            "\n17.5% -> LightValue mutation" +
            "\n17.5% -> StrongPercentage mutation" +
            "\n17.5% -> StrongValue mutation")]
        Chaotic,

    }
    public enum ActivationFunctionType
    {
        //NO REAL TIME MODIFICATION
        [Tooltip("@output: 0 or 1\n" +
                 "@good for output layer - binary value")]
        BinaryStep,
        [Tooltip("@output: (0, 1)\n" +
                 "@good for output layer - good value range (positive)")]
        Sigmoid,
        [Tooltip("@output: (-1, 1)\n" +
                 "@best for output layer - good value range")]
        Tanh,
        [Tooltip("@output: [0, +inf)\n" +
                 "@good for hidden layers - low computation")]
        Relu,
        [Tooltip("@output: (-inf*,+inf)\n" +
                 "@best for hidden layers - low computation")]
        LeakyRelu,
        [Tooltip("@output: [-0.278, +inf)\n" +
                 "@smooth ReLU - higher computation")]
        Silu,
        [Tooltip("@output: [0, 1]\n" +
                 "@output activation ONLY\n" +
                 "@good for decisional output")]
        SoftMax,

    }
    public enum InitializationFunctionType
    {
        [Tooltip("@value: [-1, 1]")]
        RandomValue,
        [Tooltip("@Box-Muller method in Standard Normal Distribution")]
        StandardDistribution,
        [Tooltip("@value: average 0.673\n" +
           "@l = 0.15915f\n" +
           "@k = 1.061f\n" +
           "@z = 0.3373f")]
        Deviation1Distribution,
        [Tooltip("@value: average 0.725\n" +
            "@l = 0.15915f\n" +
            "@k = 2f\n" +
            "@z = 0.3373f")]
        Deviation2Distribution,
    }
    public enum LossFunctionType
    {
        [Tooltip("@(output - expectedOutput)^2")]
        Quadratic,
        [Tooltip("@abs(output - expectedOutput)")]
        Absolute,
        [Tooltip("@only for SoftMax outputActivation\nonly when ActionBuffer has only values of 0 with one value of 1")]
        CrossEntropy
    }
    public enum HeuristicModule
    {
        [Tooltip("@append data to the file below\n@creates a file if doesn't exist using the name from Samples Path")]
        Collect,
        [Tooltip("@use data from the file below")]
        Learn,
    }
}