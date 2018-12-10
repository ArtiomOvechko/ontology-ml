using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SageMakerRuntime;
using System.IO;
using Amazon.SageMakerRuntime.Model;
using Amazon.S3.Model;
using System.Threading;
using System.Text;

namespace AOvechko.Nure.OntoCloud.ConsoleApp
{
    public class Logger
    {
        public static void Info(string message)
        {
            Console.WriteLine(message);
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            string accesKeyId = "";
            string accessKeySecret = "";
            string trainingImage = "";
            string roleArn = "";

            string trainingJobName = $"ontology-training-job-{DateTime.UtcNow.Ticks}";
            string endpointName = $"ontology-endpoint-{DateTime.UtcNow.Ticks}";

            using (AmazonS3Client client = new AmazonS3Client(accesKeyId, accessKeySecret, Amazon.RegionEndpoint.EUCentral1))
            {
                TransferUtility fileTransferUtility = new TransferUtility(client);

                // upload our csv training\test files

                using (AmazonSageMakerClient awsSageMakerClient = new AmazonSageMakerClient(accesKeyId, accessKeySecret, Amazon.RegionEndpoint.EUCentral1))
                {
                    CreateTrainingJobResponse response = awsSageMakerClient.CreateTrainingJobAsync(new CreateTrainingJobRequest()
                    {
                        AlgorithmSpecification = new AlgorithmSpecification()
                        {
                            TrainingInputMode = TrainingInputMode.File,
                            TrainingImage = trainingImage
                        },
                        OutputDataConfig = new OutputDataConfig()
                        {
                            S3OutputPath = "https://s3.eu-central-1.amazonaws.com/sagemaker-ovechko/sagemaker/test-csv/output"
                        },
                        ResourceConfig = new ResourceConfig()
                        {
                            InstanceCount = 1,
                            InstanceType = TrainingInstanceType.MlM4Xlarge,
                            VolumeSizeInGB = 5
                        },
                        TrainingJobName = trainingJobName,
                        HyperParameters = new Dictionary<string, string>()
                        {
                            { "eta", "0.1" },
                            { "objective", "multi:softmax" },
                            { "num_round", "5" },
                            { "num_class", "3" }
                        },
                        StoppingCondition = new StoppingCondition()
                        {
                            MaxRuntimeInSeconds = 3600
                        },
                        RoleArn = roleArn,
                        InputDataConfig = new List<Channel>()
                        {
                            new Channel()
                            {
                                ChannelName = "train",
                                DataSource = new DataSource()
                                {
                                    S3DataSource = new S3DataSource()
                                    {
                                        S3DataType = S3DataType.S3Prefix,
                                        S3Uri = "https://s3.eu-central-1.amazonaws.com/sagemaker-ovechko/sagemaker/test-csv/train/",
                                        S3DataDistributionType = S3DataDistribution.FullyReplicated
                                    }
                                },
                                ContentType = "csv",
                                CompressionType = Amazon.SageMaker.CompressionType.None
                            },
                            new Channel()
                            {
                                ChannelName = "validation",
                                DataSource = new DataSource()
                                {
                                    S3DataSource = new S3DataSource()
                                    {
                                        S3DataType = S3DataType.S3Prefix,
                                        S3Uri = "https://s3.eu-central-1.amazonaws.com/sagemaker-ovechko/sagemaker/test-csv/validation/",
                                        S3DataDistributionType = S3DataDistribution.FullyReplicated
                                    }
                                },
                                ContentType = "csv",
                                CompressionType = Amazon.SageMaker.CompressionType.None
                            }
                        }
                    }).Result;

                    string modelName = $"{trainingJobName}-model";

                    DescribeTrainingJobResponse info = new DescribeTrainingJobResponse()
                    {
                        TrainingJobStatus = TrainingJobStatus.InProgress
                    };

                    while (info.TrainingJobStatus == TrainingJobStatus.InProgress)
                    {
                        info = awsSageMakerClient.DescribeTrainingJobAsync(new DescribeTrainingJobRequest()
                        {
                            TrainingJobName = trainingJobName
                        }).Result;

                        if (info.TrainingJobStatus == TrainingJobStatus.InProgress)
                        {
                            Logger.Info("Training job creation is in progress...");
                            Thread.Sleep(10000);
                        }
                    }

                    Logger.Info($"Training job creation has been finished. With status {info.TrainingJobStatus.ToString()}. {info.FailureReason}");

                    if (info.TrainingJobStatus == TrainingJobStatus.Completed)
                    {
                        CreateModelResponse modelCreationInfo = awsSageMakerClient.CreateModelAsync(new CreateModelRequest()
                        {
                            ModelName = modelName,
                            ExecutionRoleArn = roleArn,
                            PrimaryContainer = new ContainerDefinition()
                            {
                                ModelDataUrl = info.ModelArtifacts.S3ModelArtifacts,
                                Image = trainingImage
                            }
                        }).Result;

                        string endpointConfigName = $"{endpointName}-config";

                        awsSageMakerClient.CreateEndpointConfigAsync(new CreateEndpointConfigRequest()
                        {
                            EndpointConfigName = endpointConfigName,
                            ProductionVariants = new List<ProductionVariant>()
                        {
                            new ProductionVariant()
                            {
                                InstanceType = ProductionVariantInstanceType.MlM4Xlarge,
                                InitialVariantWeight = 1,
                                InitialInstanceCount = 1,
                                ModelName = modelName,
                                VariantName = "AllTraffic"
                            }
                        }
                        });

                        CreateEndpointResponse endpointCreationInfo = awsSageMakerClient.CreateEndpointAsync(new CreateEndpointRequest()
                        {
                            EndpointConfigName = endpointConfigName,
                            EndpointName = endpointName
                        }).Result;

                        EndpointStatus currentStatus = EndpointStatus.Creating;
                        while (currentStatus == EndpointStatus.Creating)
                        {
                            currentStatus = awsSageMakerClient.DescribeEndpointAsync(new DescribeEndpointRequest()
                            {
                                EndpointName = endpointName
                            }).Result.EndpointStatus;

                            if (currentStatus == EndpointStatus.Creating)
                            {
                                Logger.Info("Endpoint creation is in progress...");
                                Thread.Sleep(10000);
                            }
                        }

                        Logger.Info("Endpoint creation has been finished.");

                        if (currentStatus == EndpointStatus.InService)
                        {
                            using (AmazonSageMakerRuntimeClient sageMakerRuntimeClient = new AmazonSageMakerRuntimeClient(accesKeyId, accessKeySecret, Amazon.RegionEndpoint.EUCentral1))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    GetObjectResponse s3Response = client.GetObjectAsync("sagemaker-ovechko", "sagemaker/test-csv/test/test.csv").Result;
                                    s3Response.ResponseStream.CopyTo(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    using (StreamReader sr = new StreamReader(ms))
                                    {
                                        string csv = sr.ReadToEnd();
                                        csv = csv.Replace("ï»¿", string.Empty);
                                        using (MemoryStream ms2 = new MemoryStream(Encoding.ASCII.GetBytes(csv)))
                                        {
                                            InvokeEndpointResponse endpointResponseInfo = sageMakerRuntimeClient.InvokeEndpointAsync(new InvokeEndpointRequest()
                                            {
                                                ContentType = "text/csv",
                                                EndpointName = endpointName,
                                                Body = ms2,
                                            }).Result;
                                            using (StreamReader sr2 = new StreamReader(endpointResponseInfo.Body))
                                            {
                                                string endpointResponseBody = sr2.ReadToEnd();
                                                Logger.Info(endpointResponseBody);
                                            }
                                        }
                                    }
                                }

                                Logger.Info("Performing clean up...");

                                awsSageMakerClient.DeleteEndpointAsync(new DeleteEndpointRequest()
                                {
                                    EndpointName = endpointName
                                });

                                awsSageMakerClient.DeleteEndpointConfigAsync(new DeleteEndpointConfigRequest()
                                {
                                    EndpointConfigName = endpointConfigName
                                });

                                awsSageMakerClient.DeleteModelAsync(new DeleteModelRequest()
                                {
                                    ModelName = modelName
                                });

                                Logger.Info("Clean up finished.");
                            }
                        }
                    }
                }
            }
            Console.ReadLine();
        }
    }
}