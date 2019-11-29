﻿using AzurePipelinesToGitHubActionsConverter.Core.AzurePipelines;
using AzurePipelinesToGitHubActionsConverter.Core.GitHubActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AzurePipelinesToGitHubActionsConverter.Core
{
    public class AzurePipelinesStepsProcessing
    {
        //This section is very much in Alpha. It has long way to go.
        public GitHubActions.Step ProcessStep(AzurePipelines.Step step)
        {
            if (step.task != null)
            {
                GitHubActions.Step gitHubStep;
                switch (step.task)
                {
                    case "AzureAppServiceManage@0":
                        gitHubStep = CreateAzureAppServiceManageStep(step);
                        break;
                    //case "AzureResourceGroupDeployment@2":
                    //    gitHubStep = CreateAzureManageResourcesStep(step);
                    //    break;
                    case "AzureRmWebAppDeployment@3":
                        gitHubStep = CreateScriptStep("powershell", step);
                        break;
                    case "CmdLine@2":
                        gitHubStep = CreateScriptStep("cmd", step);
                        break;
                    case "CopyFiles@2":
                        //Use PowerShell to copy files
                        step.script = "Copy " + step.inputs["SourceFolder"] + "/" + step.inputs["Contents"] + " " + step.inputs["TargetFolder"];
                        gitHubStep = CreateScriptStep("powershell", step);
                        break;
                    case "DotNetCoreCLI@2":
                        gitHubStep = CreateDotNetCommandStep(step);
                        break;
                    case "DownloadBuildArtifacts@0":
                        gitHubStep = CreateScriptStep("powershell", step);
                        break;
                    case "PowerShell@2":
                        gitHubStep = CreateScriptStep("powershell", step);
                        break;
                    case "PublishBuildArtifacts@1":
                        gitHubStep = CreatePublishBuildArtifactsStep(step);
                        break;
                    case "UseDotNet@2":
                        gitHubStep = new GitHubActions.Step
                        {
                            name = step.displayName,
                            uses = "actions/setup-dotnet@v1",
                            with = new Dictionary<string, string>
                            {
                                {"dotnet-version", step.inputs["version"] }
                            }
                        };
                        //Pipelines
                        //- task: UseDotNet@2
                        //  displayName: 'Use .NET Core sdk'
                        //  inputs:
                        //    packageType: sdk
                        //    version: 2.2.203
                        //    installationPath: $(Agent.ToolsDirectory)/dotnet

                        //Actions
                        //- uses: actions/setup-dotnet@v1
                        //  with:
                        //    dotnet-version: '2.2.103' # SDK Version to use.
                        break;
                    case "VSTest@2":
                        step.script = @" |
                            $vsTestConsoleExe = ""C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\Common7\\IDE\\Extensions\\TestPlatform\\vstest.console.exe""
                            $targetTestDll = """ + step.inputs["testAssemblyVer2"] + @"""
                            $testRunSettings = ""/Settings:`""" + step.inputs["runSettingsFile"] + @"`"" ""
                            $parameters = "" -- TestEnvironment=""Beta123""  ServiceUrl=""https://featureflags-data-eu-service-staging.azurewebsites.net/"" WebsiteUrl=""https://featureflags-data-eu-web-staging.azurewebsites.net/"" ""
                            #Note that the `"" is an escape character to quote strings, and the `& is needed to start the command
                            $command = ""`& `""$vsTestConsoleExe`"" `""$targetTestDll`"" $testRunSettings $parameters ""                             
                            Write-Host ""$command""
                            Invoke-Expression $command
                            ";
                        gitHubStep = CreateScriptStep("powershell", step);

                        //- task: VSTest@2
                        //displayName: 'Run functional smoke tests on website and web service'
                        //inputs:
                        //  searchFolder: '$(build.artifactstagingdirectory)'
                        //  testAssemblyVer2: |
                        //    **\FeatureFlags.FunctionalTests\FeatureFlags.FunctionalTests.dll
                        //  uiTests: true
                        //  runSettingsFile: '$(build.artifactstagingdirectory)/drop/FunctionalTests/FeatureFlags.FunctionalTests/test.runsettings'
                        //  overrideTestrunParameters: |
                        //   -ServiceUrl "https://$(WebServiceName)-staging.azurewebsites.net/" 
                        //   -WebsiteUrl "https://$(WebsiteName)-staging.azurewebsites.net/" 
                        //   -TestEnvironment "$(AppSettings.Environment)" 
                        break;


                    default:
                        gitHubStep = CreateScriptStep("powershell", step);
                        gitHubStep.name = "***This step could not be migrated***: " + step.displayName;
                        string newYaml = Global.WriteYAMLFile<AzurePipelines.Step>(step);
                        string[] newYamlSplit = newYaml.Split(Environment.NewLine);
                        StringBuilder yamlBuilder = new StringBuilder();
                        for (int i = 0; i < newYamlSplit.Length; i++)
                        {
                            string line = newYamlSplit[i];
                            if (line.Trim().Length > 0)
                            {
                                yamlBuilder.Append("#");
                                yamlBuilder.Append(line);
                                if (i < newYamlSplit.Length - 1)
                                {
                                    yamlBuilder.Append(Environment.NewLine);
                                }
                            }
                        }
                        gitHubStep.run = yamlBuilder.ToString();
                        break;
                }

                return gitHubStep;
            }
            else if (step.script != null)
            {
                return new GitHubActions.Step
                {
                    name = step.displayName,
                    run = step.script,
                    with = step.inputs
                };
            }
            else if (step.pwsh != null)
            {
                return CreateScriptStep("pwsh", step);
            }
            else if (step.powershell != null)
            {
                return CreateScriptStep("powershell", step);
            }
            else if (step.bash != null)
            {
                return CreateScriptStep("bash", step);
            }
            else
            {
                return null;
            }
        }

        private GitHubActions.Step CreateDotNetCommandStep(AzurePipelines.Step step)
        {

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = step.displayName,
                run = "dotnet " +
                    step.inputs["command"] + " " +
                    step.inputs["projects"] + " " +
                    step.inputs["arguments"]
            };

            //Remove the new line characters
            gitHubStep.run = gitHubStep.run.Replace("\n", "");

            return gitHubStep;
        }

        private GitHubActions.Step CreateScriptStep(string shellType, AzurePipelines.Step step)
        {
            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = step.displayName,
                run = step.script,
                shell = shellType//,
                //with = step.inputs
            };

            if (gitHubStep.run == null)
            {
                if (step.powershell != null)
                {
                    gitHubStep.run = step.powershell;
                }
                else if (step.pwsh != null)
                {
                    gitHubStep.run = step.pwsh;
                }
                else if (step.bash != null)
                {
                    gitHubStep.run = step.bash;
                }
                else
                {
                    if (step.inputs != null)
                    {
                        step.inputs.TryGetValue("script", out string value);
                        gitHubStep.run = value;
                    }
                }
            }

            return gitHubStep;
        }

        private GitHubActions.Step CreateAzureLoginStep()
        {
            //https://github.com/Azure/github-actions/tree/master/login
            // action "Azure Login" {
            //  uses = "Azure/github-actions/login@master"
            //  env = {
            //    AZURE_SUBSCRIPTION = "Subscription Name"
            //  }
            //  secrets = ["AZURE_SERVICE_APP_ID", "AZURE_SERVICE_PASSWORD", "AZURE_SERVICE_TENANT"]
            //}
            return null;
        }

        private GitHubActions.Step CreateAzureManageResourcesStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: AzureResourceGroupDeployment@2
            //  displayName: 'Deploy ARM Template to resource group'
            //  inputs:
            //    azureSubscription: 'SamLearnsAzure connection to Azure Portal'
            //    resourceGroupName: $(ResourceGroupName)
            //    location: '[resourceGroup().location]'
            //    csmFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.json'
            //    csmParametersFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.parameters.json'
            //    overrideParameters: '-environment $(AppSettings.Environment) -locationShort $(ArmTemplateResourceGroupLocation)'

            //Going to:
            //https://github.com/Azure/github-actions/tree/master/arm
            //action "Manage Azure Resources" {
            //  uses = "Azure/github-actions/arm@master"
            //  env = {
            //    AZURE_RESOURCE_GROUP = "<Resource Group Name"
            //    AZURE_TEMPLATE_LOCATION = "<URL or Relative path in your repository>"
            //    AZURE_TEMPLATE_PARAM_FILE = "<URL or Relative path in your repository>"
            //  }
            //  needs = ["Azure Login"]
            //}}

            step.inputs.TryGetValue("resourceGroupName", out string resourceGroup);
            step.inputs.TryGetValue("csmFile", out string armTemplateFile);
            step.inputs.TryGetValue("csmParametersFile", out string armTemplateParametersFile);

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = step.displayName,
                uses = "Azure/github-actions/arm@master",
                env = new Dictionary<string, string>
                {
                    { "AZURE_RESOURCE_GROUP", resourceGroup},
                    { "AZURE_TEMPLATE_LOCATION", armTemplateFile},
                    { "AZURE_TEMPLATE_PARAM_FILE", armTemplateParametersFile},
                }
            };

            return null;
        }
        private GitHubActions.Step CreateAzureAppServiceManageStep(AzurePipelines.Step step)
        {
            //https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/deploy/azure-app-service-manage?view=azure-devops
            //coming from:
            //- task: AzureAppServiceManage@0
            //displayName: 'Swap Slots: web service'
            //inputs:
            //  azureSubscription: 'SamLearnsAzure connection to Azure Portal'
            //  WebAppName: $(WebServiceName)
            //  ResourceGroupName: $(ResourceGroupName)
            //  SourceSlot: 'staging'

            //Going to:
            //- name: Swap web service staging slot to production
            //  uses: Azure/cli@v1.0.0
            //  with:
            //    inlineScript: az webapp deployment slot swap --resource-group SamLearnsAzureFeatureFlags --name featureflags-data-eu-service --slot staging --target-slot production


            step.inputs.TryGetValue("resourceGroupName", out string resourceGroup);
            step.inputs.TryGetValue("webappname", out string webAppName);
            step.inputs.TryGetValue("sourceslot", out string sourceSlot);
            step.inputs.TryGetValue("targetslot", out string targetSlot);
            if (targetSlot == null)
            {
                targetSlot = "production";
            }
            //TODO: Add the other properties

            string script = "az webapp deployment slot swap --resource-group " + resourceGroup +
                " --name " + webAppName +
                " --slot " + sourceSlot +
                " --target-slot " + targetSlot + "";

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = step.displayName,
                uses = "Azure/cli@v1.0.0",
                with = new Dictionary<string, string>
                {
                    { "inlineScript", script}
                }
            };

            return null;
        }

        private GitHubActions.Step CreatePublishBuildArtifactsStep(AzurePipelines.Step step)
        {
            //# Publish the artifacts
            //- task: PublishBuildArtifacts@1
            //  displayName: 'Publish Artifact'
            //  inputs:
            //    PathtoPublish: '$(build.artifactstagingdirectory)'";

            //- name: publish build artifacts back to GitHub
            //  uses: actions/upload-artifact@master
            //  with:
            //    name: console exe
            //    path: /home/runner/work/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter.ConsoleApp/bin/Release/netcoreapp3.0

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = step.displayName,
                uses = "actions/upload-artifact@master",
                with = new Dictionary<string, string>
                {
                    {"path", step.inputs["PathtoPublish"] }
                }
            };
            //In publish task, I we need to delete any usage of build.artifactstagingdirectory variable as it's implied in github actions, and therefore not needed (Adding it adds the path twice)
            gitHubStep.with["path"].Replace("$(build.artifactstagingdirectory)", "");

            return gitHubStep;
        }
    }
}