using AzurePipelinesToGitHubActionsConverter.Core;
using AzurePipelinesToGitHubActionsConverter.Core.Conversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AzurePipelinesToGitHubActionsConverter.Tests
{
    [TestClass]
    public class CompletePipelineTests
    {

        [TestMethod]
        public void LargePipelineIsFullyProcessedTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
# ASP.NET Core
# Build and test ASP.NET Core projects targeting .NET Core.
# Add steps that run tests, create a NuGet package, deploy, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/dotnet-core
trigger:
- master
pr:
  branches:
    include:
    - '*'  
variables:
  vmImage: 'windows-latest'
  buildConfiguration: 'Release'
  buildPlatform: 'Any CPU'
  buildNumber: '1.1.0.0'
stages:
- stage: Build
  displayName: 'Build/Test Stage'
  jobs:
  - job: Build
    displayName: 'Build job'
    pool:
      vmImage: $(vmImage)
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET Core sdk'
      inputs:
        packageType: sdk
        version: 2.2.203
    - task: PowerShell@2
      displayName: 'Generate build version number'
      inputs:
        targetType: 'inline'
        script: |
         Write-Host ""Generating Build Number""
         #Get the version from the csproj file
         $xml = [Xml] (Get-Content FeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj)
         $initialVersion = [Version] $xml.Project.PropertyGroup.Version
         Write-Host ""Initial Version: "" $version
         $splitVersion = $initialVersion -Split ""\.""
         #Get the build number (number of days since January 1, 2000)
         $baseDate = [datetime]""01/01/2000""
         $currentDate = $(Get-Date)
         $interval = (NEW-TIMESPAN -Start $baseDate -End $currentDate)
         $buildNumber = $interval.Days
         #Get the revision number (number seconds (divided by two) into the day on which the compilation was performed)
         $StartDate=[datetime]::Today
         $EndDate=(GET-DATE)
         $revisionNumber = [math]::Round((New-TimeSpan -Start $StartDate -End $EndDate).TotalSeconds / 2,0)
         #Final version number
         $finalBuildVersion = ""$($splitVersion[0]).$($splitVersion[1]).$($buildNumber).$($revisionNumber)""
         Write-Host ""Major.Minor,Build,Revision""
         Write-Host ""Final build number: "" $finalBuildVersion
         #Writing final version number back to Azure DevOps variable
         Write-Host ""##vso[task.setvariable variable=buildNumber]$finalBuildVersion""

    - task: CopyFiles@2
      displayName: 'Copy environment ARM template files to: $(build.artifactstagingdirectory)'
      inputs:
        SourceFolder: '$(system.defaultworkingdirectory)\FeatureFlags\FeatureFlags.ARMTemplates'
        Contents: '**\*' # **\* = Copy all files and all files in sub directories
        TargetFolder: '$(build.artifactstagingdirectory)\ARMTemplates'

    - task: DotNetCoreCLI@2
      displayName: 'Test dotnet code projects'
      inputs:
        command: test
        projects: |
         FeatureFlags/FeatureFlags.Tests/FeatureFlags.Tests.csproj
        arguments: '--configuration $(buildConfiguration) --logger trx --collect ""Code coverage"" --settings:$(Build.SourcesDirectory)\FeatureFlags\FeatureFlags.Tests\CodeCoverage.runsettings'

    - task: DotNetCoreCLI@2
      displayName: 'Publish dotnet core projects'
      inputs:
        command: publish
        publishWebProjects: false
        projects: |
         FeatureFlags/FeatureFlags.Service/FeatureFlags.Service.csproj
         FeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj
        arguments: '--configuration $(buildConfiguration) --output $(build.artifactstagingdirectory) -p:Version=$(buildNumber)'
        zipAfterPublish: true

    - task: DotNetCoreCLI@2
      displayName: 'Publish dotnet core functional tests project'
      inputs:
        command: publish
        publishWebProjects: false
        projects: |
         FeatureFlags/FeatureFlags.FunctionalTests/FeatureFlags.FunctionalTests.csproj
        arguments: '--configuration $(buildConfiguration) --output $(build.artifactstagingdirectory)/FunctionalTests'
        zipAfterPublish: false

    - task: CopyFiles@2
      displayName: 'Copy Selenium Files to: $(build.artifactstagingdirectory)/FunctionalTests/FeatureFlags.FunctionalTests'
      inputs:
        SourceFolder: 'FeatureFlags/FeatureFlags.FunctionalTests/bin/$(buildConfiguration)/netcoreapp3.0'
        Contents: '*chromedriver.exe*'
        TargetFolder: '$(build.artifactstagingdirectory)/FunctionalTests/FeatureFlags.FunctionalTests'

    # Publish the artifacts
    - task: PublishBuildArtifacts@1
      displayName: 'Publish Artifact'
      inputs:
        PathtoPublish: '$(build.artifactstagingdirectory)'";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
  pull-request:
    branches:
    - '*'
env:
  vmImage: windows-latest
  buildConfiguration: Release
  buildPlatform: Any CPU
  buildNumber: 1.1.0.0
jobs:
  Build_Stage_Build:
    name: Build job
    runs-on: ${{ env.vmImage }}
    steps:
    - uses: actions/checkout@v2
    - name: Use .NET Core sdk
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 2.2.203
    - name: Generate build version number
      run: |
        Write-Host ""Generating Build Number""
        #Get the version from the csproj file
        $xml = [Xml] (Get-Content FeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj)
        $initialVersion = [Version] $xml.Project.PropertyGroup.Version
        Write-Host ""Initial Version: "" $version
        $splitVersion = $initialVersion -Split ""\.""
        #Get the build number (number of days since January 1, 2000)
        $baseDate = [datetime]""01/01/2000""
        $currentDate = ${{ env.Get-Date }}
        $interval = (NEW-TIMESPAN -Start $baseDate -End $currentDate)
        ${{ env.buildNumber }} = $interval.Days
        #Get the revision number (number seconds (divided by two) into the day on which the compilation was performed)
        $StartDate=[datetime]::Today
        $EndDate=(GET-DATE)
        $revisionNumber = [math]::Round((New-TimeSpan -Start $StartDate -End $EndDate).TotalSeconds / 2,0)
        #Final version number
        $finalBuildVersion = ""${{ env.$splitVersion[0] }}.${{ env.$splitVersion[1] }}.$(${{ env.buildNumber }}).${{ env.$revisionNumber }}""
        Write-Host ""Major.Minor,Build,Revision""
        Write-Host ""Final build number: "" $finalBuildVersion
        #Writing final version number back to Azure DevOps variable
        Write-Host ""##vso[task.setvariable variable=buildNumber]$finalBuildVersion""
      shell: powershell
    - name: 'Copy environment ARM template files to: ${GITHUB_WORKSPACE}'
      run: Copy '${{ env.system.defaultworkingdirectory }}\FeatureFlags\FeatureFlags.ARMTemplates/**\*' '${GITHUB_WORKSPACE}\ARMTemplates'
      shell: powershell
    - name: Test dotnet code projects
      run: dotnet test FeatureFlags/FeatureFlags.Tests/FeatureFlags.Tests.csproj --configuration ${{ env.buildConfiguration }} --logger trx --collect ""Code coverage"" --settings:${{ env.Build.SourcesDirectory }}\FeatureFlags\FeatureFlags.Tests\CodeCoverage.runsettings
    - name: Publish dotnet core projects
      run: dotnet publish FeatureFlags/FeatureFlags.Service/FeatureFlags.Service.csprojFeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj --configuration ${{ env.buildConfiguration }} --output ${GITHUB_WORKSPACE} -p:Version=${{ env.buildNumber }}
    - name: Publish dotnet core functional tests project
      run: dotnet publish FeatureFlags/FeatureFlags.FunctionalTests/FeatureFlags.FunctionalTests.csproj --configuration ${{ env.buildConfiguration }} --output ${GITHUB_WORKSPACE}/FunctionalTests
    - name: 'Copy Selenium Files to: ${GITHUB_WORKSPACE}/FunctionalTests/FeatureFlags.FunctionalTests'
      run: Copy 'FeatureFlags/FeatureFlags.FunctionalTests/bin/${{ env.buildConfiguration }}/netcoreapp3.0/*chromedriver.exe*' '${GITHUB_WORKSPACE}/FunctionalTests/FeatureFlags.FunctionalTests'
      shell: powershell
    - name: Publish Artifact
      uses: actions/upload-artifact@master
      with:
        path: ${GITHUB_WORKSPACE}
";
            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void LargeMultiStagePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
# ASP.NET Core
# Build and test ASP.NET Core projects targeting .NET Core.
# Add steps that run tests, create a NuGet package, deploy, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/dotnet-core

trigger:
- master
pr:
  branches:
    include:
    - '*'

variables:
  vmImage: 'windows-latest'
  buildConfiguration: 'Release'
  buildPlatform: 'Any CPU'
  buildNumber: '1.1.0.0'

stages:
- stage: Build
  displayName: 'Build/Test Stage'
  jobs:
  - job: Build
    displayName: 'Build job'
    pool:
      vmImage: $(vmImage)
    steps:
    - task: PowerShell@2
      displayName: 'Generate build version number'
      inputs:
        targetType: FilePath
        filePath: MyProject/BuildVersion.ps1
        arguments: -ProjectFile ""MyProject/MyProject.Web/MyProject.Web.csproj""

    - task: CopyFiles@2
      displayName: 'Copy environment ARM template files to: $(build.artifactstagingdirectory)'
      inputs:
        SourceFolder: '$(system.defaultworkingdirectory)\FeatureFlags\FeatureFlags.ARMTemplates'
        Contents: '**\*' # **\* = Copy all files and all files in sub directories
        TargetFolder: '$(build.artifactstagingdirectory)\ARMTemplates'

    - task: DotNetCoreCLI@2
      displayName: 'Test dotnet code projects'
      inputs:
        command: test
        projects: |
         FeatureFlags/FeatureFlags.Tests/FeatureFlags.Tests.csproj
        arguments: '--configuration $(buildConfiguration) --logger trx --collect ""Code coverage"" --settings:$(Build.SourcesDirectory)\FeatureFlags\FeatureFlags.Tests\CodeCoverage.runsettings'

    - task: DotNetCoreCLI@2
      displayName: 'Publish dotnet core projects'
      inputs:
        command: publish
        publishWebProjects: false
        projects: |
         FeatureFlags/FeatureFlags.Service/FeatureFlags.Service.csproj
         FeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj
        arguments: '--configuration $(buildConfiguration) --output $(build.artifactstagingdirectory) -p:Version=$(buildNumber)'
        zipAfterPublish: true

    - task: DotNetCoreCLI@2
      displayName: 'Publish dotnet core functional tests project'
      inputs:
        command: publish
        publishWebProjects: false
        projects: |
         FeatureFlags/FeatureFlags.FunctionalTests/FeatureFlags.FunctionalTests.csproj
        arguments: '--configuration $(buildConfiguration) --output $(build.artifactstagingdirectory)/FunctionalTests'
        zipAfterPublish: false

    - task: CopyFiles@2
      displayName: 'Copy Selenium Files to: $(build.artifactstagingdirectory)/FunctionalTests/FeatureFlags.FunctionalTests'
      inputs:
        SourceFolder: 'FeatureFlags/FeatureFlags.FunctionalTests/bin/$(buildConfiguration)/netcoreapp3.0'
        Contents: '*chromedriver.exe*'
        TargetFolder: '$(build.artifactstagingdirectory)/FunctionalTests/FeatureFlags.FunctionalTests'

    # Publish the artifacts
    - task: PublishBuildArtifacts@1
      displayName: 'Publish Artifact'
      inputs:
        PathtoPublish: '$(build.artifactstagingdirectory)'

- stage: Deploy
  displayName: 'Deploy Prod'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/master'))
  jobs:
  - job: Deploy
    displayName: 'Deploy job'
    pool:
      vmImage: $(vmImage)   
    variables:
      AppSettings.Environment: 'data'
      ArmTemplateResourceGroupLocation: 'eu'
      ResourceGroupName: 'MyProjectFeatureFlags'
      WebsiteName: 'featureflags-data-eu-web'
      WebServiceName: 'featureflags-data-eu-service'
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download the build artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(build.artifactstagingdirectory)'
    - task: AzureResourceGroupDeployment@2
      displayName: 'Deploy ARM Template to resource group'
      inputs:
        azureSubscription: 'Connection to Azure Portal'
        resourceGroupName: $(ResourceGroupName)
        location: '[resourceGroup().location]'
        csmFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.json'
        csmParametersFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.parameters.json'
        overrideParameters: '-environment $(AppSettings.Environment) -locationShort $(ArmTemplateResourceGroupLocation)'
    - task: AzureRmWebAppDeployment@3
      displayName: 'Azure App Service Deploy: web service'
      inputs:
        azureSubscription: 'Connection to Azure Portal'
        WebAppName: $(WebServiceName)
        DeployToSlotFlag: true
        ResourceGroupName: $(ResourceGroupName)
        SlotName: 'staging'
        Package: '$(build.artifactstagingdirectory)/drop/FeatureFlags.Service.zip'
        TakeAppOfflineFlag: true
        JSONFiles: '**/appsettings.json'
    - task: AzureRmWebAppDeployment@3
      displayName: 'Azure App Service Deploy: web site'
      inputs:
        azureSubscription: 'Connection to Azure Portal'
        WebAppName: $(WebsiteName)
        DeployToSlotFlag: true
        ResourceGroupName: $(ResourceGroupName)
        SlotName: 'staging'
        Package: '$(build.artifactstagingdirectory)/drop/FeatureFlags.Web.zip'
        TakeAppOfflineFlag: true
        JSONFiles: '**/appsettings.json'        
    - task: VSTest@2
      displayName: 'Run functional smoke tests on website and web service'
      inputs:
        searchFolder: '$(build.artifactstagingdirectory)'
        testAssemblyVer2: '**\FeatureFlags.FunctionalTests\FeatureFlags.FunctionalTests.dll'
        uiTests: true
        runSettingsFile: '$(build.artifactstagingdirectory)/drop/FunctionalTests/FeatureFlags.FunctionalTests/test.runsettings'
        overrideTestrunParameters: |
         -ServiceUrl ""https://$(WebServiceName)-staging.azurewebsites.net/"" 
         -WebsiteUrl ""https://$(WebsiteName)-staging.azurewebsites.net/"" 
         -TestEnvironment ""$(AppSettings.Environment)"" 
    - task: AzureAppServiceManage@0
      displayName: 'Swap Slots: web service'
      inputs:
        azureSubscription: 'Connection to Azure Portal'
        WebAppName: $(WebServiceName)
        ResourceGroupName: $(ResourceGroupName)
        SourceSlot: 'staging'
    - task: AzureAppServiceManage@0
      displayName: 'Swap Slots: website'
      inputs:
        azureSubscription: 'Connection to Azure Portal'
        WebAppName: $(WebsiteName)
        ResourceGroupName: $(ResourceGroupName)
        SourceSlot: 'staging'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets
on:
  push:
    branches:
    - master
  pull-request:
    branches:
    - '*'
env:
  vmImage: windows-latest
  buildConfiguration: Release
  buildPlatform: Any CPU
  buildNumber: 1.1.0.0
jobs:
  Build_Stage_Build:
    name: Build job
    runs-on: ${{ env.vmImage }}
    steps:
    - uses: actions/checkout@v2
    - name: Generate build version number
      shell: powershell
    - name: 'Copy environment ARM template files to: ${GITHUB_WORKSPACE}'
      run: Copy '${{ env.system.defaultworkingdirectory }}\FeatureFlags\FeatureFlags.ARMTemplates/**\*' '${GITHUB_WORKSPACE}\ARMTemplates'
      shell: powershell
    - name: Test dotnet code projects
      run: dotnet test FeatureFlags/FeatureFlags.Tests/FeatureFlags.Tests.csproj --configuration ${{ env.buildConfiguration }} --logger trx --collect ""Code coverage"" --settings:${{ env.Build.SourcesDirectory }}\FeatureFlags\FeatureFlags.Tests\CodeCoverage.runsettings
    - name: Publish dotnet core projects
      run: dotnet publish FeatureFlags/FeatureFlags.Service/FeatureFlags.Service.csprojFeatureFlags/FeatureFlags.Web/FeatureFlags.Web.csproj --configuration ${{ env.buildConfiguration }} --output ${GITHUB_WORKSPACE} -p:Version=${{ env.buildNumber }}
    - name: Publish dotnet core functional tests project
      run: dotnet publish FeatureFlags/FeatureFlags.FunctionalTests/FeatureFlags.FunctionalTests.csproj --configuration ${{ env.buildConfiguration }} --output ${GITHUB_WORKSPACE}/FunctionalTests
    - name: 'Copy Selenium Files to: ${GITHUB_WORKSPACE}/FunctionalTests/FeatureFlags.FunctionalTests'
      run: Copy 'FeatureFlags/FeatureFlags.FunctionalTests/bin/${{ env.buildConfiguration }}/netcoreapp3.0/*chromedriver.exe*' '${GITHUB_WORKSPACE}/FunctionalTests/FeatureFlags.FunctionalTests'
      shell: powershell
    - name: Publish Artifact
      uses: actions/upload-artifact@master
      with:
        path: ${GITHUB_WORKSPACE}
  Deploy_Stage_Deploy:
    name: Deploy job
    runs-on: ${{ env.vmImage }}
    env:
      AppSettings.Environment: data
      ArmTemplateResourceGroupLocation: eu
      ResourceGroupName: MyProjectFeatureFlags
      WebsiteName: featureflags-data-eu-web
      WebServiceName: featureflags-data-eu-service
    if: and(success(),eq(github.ref, 'refs/heads/master'))
    steps:
    - uses: actions/checkout@v2
    - #: ""Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets""
      name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_SP }}
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
    - name: Deploy ARM Template to resource group
      uses: Azure/github-actions/arm@master
      env:
        AZURE_RESOURCE_GROUP: ${{ env.ResourceGroupName }}
        AZURE_TEMPLATE_LOCATION: ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.json
        AZURE_TEMPLATE_PARAM_FILE: ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.parameters.json
    - name: 'Azure App Service Deploy: web service'
      uses: Azure/webapps-deploy@v2
      with:
        app-name: ${{ env.WebServiceName }}
        package: ${GITHUB_WORKSPACE}/drop/FeatureFlags.Service.zip
        slot-name: staging
    - name: 'Azure App Service Deploy: web site'
      uses: Azure/webapps-deploy@v2
      with:
        app-name: ${{ env.WebsiteName }}
        package: ${GITHUB_WORKSPACE}/drop/FeatureFlags.Web.zip
        slot-name: staging
    - name: Run functional smoke tests on website and web service
      run: |
        $vsTestConsoleExe = ""C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe""
        $targetTestDll = ""**\FeatureFlags.FunctionalTests\FeatureFlags.FunctionalTests.dll""
        $testRunSettings = ""/Settings:`""${GITHUB_WORKSPACE}/drop/FunctionalTests/FeatureFlags.FunctionalTests/test.runsettings`"" ""
        $parameters = "" -- ServiceUrl=""https://${{ env.WebServiceName }}-staging.azurewebsites.net/"" WebsiteUrl=""https://${{ env.WebsiteName }}-staging.azurewebsites.net/"" TestEnvironment=""${{ env.AppSettings.Environment }}"" ""
        #Note that the `"" is an escape character to quote strings, and the `& is needed to start the command
        $command = ""`& `""$vsTestConsoleExe`"" `""$targetTestDll`"" $testRunSettings $parameters ""
        Write-Host ""$command""
        Invoke-Expression $command
      shell: powershell
    - name: 'Swap Slots: web service'
      uses: Azure/cli@v1.0.0
      with:
        inlineScript: az webapp deployment slot swap --resource-group ${{ env.ResourceGroupName }} --name ${{ env.WebServiceName }} --slot staging --target-slot production
    - name: 'Swap Slots: website'
      uses: Azure/cli@v1.0.0
      with:
        inlineScript: az webapp deployment slot swap --resource-group ${{ env.ResourceGroupName }} --name ${{ env.WebsiteName }} --slot staging --target-slot production
";
            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void StagingPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
stages:
- stage: Build
  displayName: 'Build Stage'
  jobs:
  - job: Build
    displayName: 'Build job'
    pool:
      vmImage: windows-latest
    steps:
    - task: PowerShell@2
      inputs:
        targetType: 'inline'
        script: |
         Write-Host ""Hello world!""

  - job: Build2
    displayName: 'Build job 2'
    pool:
      vmImage: windows-latest
    steps:
    - task: PowerShell@2
      inputs:
        targetType: 'inline'
        script: Write-Host ""Hello world 2!""
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
jobs:
  Build_Stage_Build:
    name: Build job
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - run: Write-Host ""Hello world!""
      shell: powershell
  Build_Stage_Build2:
    name: Build job 2
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - run: Write-Host ""Hello world 2!""
      shell: powershell
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void NuGetPackagePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
resources:
- repo: self
  containers:
  - container: test123

trigger:
- master

pool:
  vmImage: 'windows-latest'

variables:
  BuildConfiguration: 'Release'
  BuildPlatform : 'Any CPU'
  BuildVersion: 1.1.$(Build.BuildId)

steps:
- task: DotNetCoreCLI@2
  displayName: Restore
  inputs:
    command: restore
    projects: MyProject/MyProject.Models/MyProject.Models.csproj

- task: DotNetCoreCLI@2
  displayName: Build
  inputs:
    projects: MyProject/MyProject.Models/MyProject.Models.csproj
    arguments: '--configuration $(BuildConfiguration)'

- task: DotNetCoreCLI@2
  displayName: Publish
  inputs:
    command: publish
    publishWebProjects: false
    projects: MyProject/MyProject.Models/MyProject.Models.csproj
    arguments: '--configuration $(BuildConfiguration) --output $(build.artifactstagingdirectory)'
    zipAfterPublish: false

- task: DotNetCoreCLI@2
  displayName: 'dotnet pack'
  inputs:
    command: pack
    packagesToPack: MyProject/MyProject.Models/MyProject.Models.csproj
    versioningScheme: byEnvVar
    versionEnvVar: BuildVersion

- task: PublishBuildArtifacts@1
  displayName: 'Publish Artifact'
  inputs:
    PathtoPublish: '$(build.artifactstagingdirectory)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  BuildConfiguration: Release
  BuildPlatform: Any CPU
  BuildVersion: 1.1.${{ env.Build.BuildId }}
jobs:
  build:
    runs-on: windows-latest
    container: {}
    steps:
    - uses: actions/checkout@v2
    - name: Restore
      run: dotnet restore MyProject/MyProject.Models/MyProject.Models.csproj
    - name: Build
      run: dotnet MyProject/MyProject.Models/MyProject.Models.csproj --configuration ${{ env.BuildConfiguration }}
    - name: Publish
      run: dotnet publish MyProject/MyProject.Models/MyProject.Models.csproj --configuration ${{ env.BuildConfiguration }} --output ${GITHUB_WORKSPACE}
    - name: dotnet pack
      run: dotnet pack
    - name: Publish Artifact
      uses: actions/upload-artifact@master
      with:
        path: ${GITHUB_WORKSPACE}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void ConditionOnStagePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

stages:
- stage: Deploy
  displayName: 'Deploy Prod'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/master'))
  jobs:
  - job: Deploy
    displayName: 'Deploy job'
    pool:
      vmImage: ubuntu-latest  
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download the build artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(build.artifactstagingdirectory)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  Deploy_Stage_Deploy:
    name: Deploy job
    runs-on: ubuntu-latest
    if: and(success(),eq(github.ref, 'refs/heads/master'))
    steps:
    - uses: actions/checkout@v2
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void ResourcesContainersPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-16.04'

container: 'mcr.microsoft.com/dotnet/core/sdk:2.2'

resources:
  containers:
  - container: redis
    image: redis
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#TODO: Container conversion not yet done: https://github.com/samsmithnz/AzurePipelinesToGitHubActionsConverter/issues/39
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-16.04
    container:
      image: redis
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void DotNetDesktopPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/microsoft/azure-pipelines-yaml/blob/master/templates/.net-desktop.yml
            string yaml = @"
# .NET Desktop
# Build and run tests for .NET Desktop or Windows classic desktop solutions.
# Add steps that publish symbols, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/apps/windows/dot-net

trigger:
- master

pool:
  vmImage: 'windows-latest'

variables:
  solution: 'WindowsFormsApp1.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

steps:
- task: NuGetToolInstaller@1
- task: NuGetCommand@2
  inputs:
    restoreSolution: '$(solution)'
- task: VSBuild@1
  inputs:
    solution: '$(solution)'
    platform: '$(buildPlatform)'
    configuration: '$(buildConfiguration)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  solution: WindowsFormsApp1.sln
  buildPlatform: Any CPU
  buildConfiguration: Release
jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - uses: microsoft/setup-msbuild@v1.0.0
    - #: 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  ${{ env.solution }}
      shell: powershell
    - run: msbuild '${{ env.solution }}' /p:configuration='${{ env.buildConfiguration }}' /p:platform='${{ env.buildPlatform }}'
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void AspDotNetFrameworkPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/microsoft/azure-pipelines-yaml/blob/master/templates/asp.net-core-.net-framework.yml
            string yaml = @"
# ASP.NET Core (.NET Framework)
# Build and test ASP.NET Core projects targeting the full .NET Framework.
# Add steps that publish symbols, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/dotnet-core

trigger:
- master

pool:
  vmImage: 'windows-latest'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

steps:
- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '$(solution)'

- task: VSBuild@1
  inputs:
    solution: '$(solution)'
    msbuildArgs: '/p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:SkipInvalidConfigurations=true /p:DesktopBuildPackageLocation=""$(build.artifactStagingDirectory)\WebApp.zip"" /p:DeployIisAppPath=""Default Web Site""'
    platform: '$(buildPlatform)'
    configuration: '$(buildConfiguration)'

#- task: VSTest@2
#  inputs:
#    platform: '$(buildPlatform)'
#    configuration: '$(buildConfiguration)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  solution: '**/*.sln'
  buildPlatform: Any CPU
  buildConfiguration: Release
jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - uses: microsoft/setup-msbuild@v1.0.0
    - #: 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  ${{ env.solution }}
      shell: powershell
    - run: msbuild '${{ env.solution }}' /p:configuration='${{ env.buildConfiguration }}' /p:platform='${{ env.buildPlatform }}' /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:SkipInvalidConfigurations=true /p:DesktopBuildPackageLocation=""${{ env.build.artifactStagingDirectory }}\WebApp.zip"" /p:DeployIisAppPath=""Default Web Site""
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void DotNetCoreCDPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/microsoft/azure-pipelines-yaml/blob/master/templates/asp.net-core-.net-framework.yml
            string yaml = @"
trigger:
- master

variables:
  buildConfiguration: 'Release'
  buildPlatform: 'Any CPU'

stages:
- stage: Deploy
  displayName: 'Deploy Prod'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/master'))
  jobs:
  - job: Deploy
    displayName: 'Deploy job'
    pool:
      vmImage: ubuntu-latest  
    variables:
      AppSettings.Environment: 'data'
      ArmTemplateResourceGroupLocation: 'eu'
      ResourceGroupName: 'MyProjectRG'
      WebsiteName: 'myproject-web'
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download the build artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(build.artifactstagingdirectory)'
    - task: AzureRmWebAppDeployment@3
      displayName: 'Azure App Service Deploy: web site'
      inputs:
        azureSubscription: 'connection to Azure Portal'
        WebAppName: $(WebsiteName)
        DeployToSlotFlag: true
        ResourceGroupName: $(ResourceGroupName)
        SlotName: 'staging'
        Package: '$(build.artifactstagingdirectory)/drop/MyProject.Web.zip'
        TakeAppOfflineFlag: true
        JSONFiles: '**/appsettings.json'        
    - task: AzureAppServiceManage@0
      displayName: 'Swap Slots: website'
      inputs:
        azureSubscription: 'connection to Azure Portal'
        WebAppName: $(WebsiteName)
        ResourceGroupName: $(ResourceGroupName)
        SourceSlot: 'staging'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets
on:
  push:
    branches:
    - master
env:
  buildConfiguration: Release
  buildPlatform: Any CPU
jobs:
  Deploy_Stage_Deploy:
    name: Deploy job
    runs-on: ubuntu-latest
    env:
      AppSettings.Environment: data
      ArmTemplateResourceGroupLocation: eu
      ResourceGroupName: MyProjectRG
      WebsiteName: myproject-web
    if: and(success(),eq(github.ref, 'refs/heads/master'))
    steps:
    - uses: actions/checkout@v2
    - #: ""Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets""
      name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_SP }}
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
    - name: 'Azure App Service Deploy: web site'
      uses: Azure/webapps-deploy@v2
      with:
        app-name: ${{ env.WebsiteName }}
        package: ${GITHUB_WORKSPACE}/drop/MyProject.Web.zip
        slot-name: staging
    - name: 'Swap Slots: website'
      uses: Azure/cli@v1.0.0
      with:
        inlineScript: az webapp deployment slot swap --resource-group ${{ env.ResourceGroupName }} --name ${{ env.WebsiteName }} --slot staging --target-slot production
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void DockerBuildPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/microsoft/azure-pipelines-yaml/blob/master/templates/docker-build.yml
            string yaml = @"
# Docker
# Build a Docker image 
# https://docs.microsoft.com/azure/devops/pipelines/languages/docker

trigger:
- master

resources:
- repo: self

variables:
  tag: '$(Build.BuildId)'
  dockerfilePath: '[MyDockerPath]'

stages:
- stage: Build
  displayName: Build image
  jobs:  
  - job: Build
    displayName: Build
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: Docker@2
      displayName: Build an image
      inputs:
        command: build
        dockerfile: '$(dockerfilePath)'
        tags: $(tag)
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  tag: ${{ env.Build.BuildId }}
  dockerfilePath: '[MyDockerPath]'
jobs:
  Build_Stage_Build:
    name: Build
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Build an image
      run: docker build . --file ${{ env.dockerfilePath }} --tag ${{ env.tag }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void AndroidPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/android.yml
            string yaml = @"
# Android
# Build your Android project with Gradle.
# Add steps that test, sign, and distribute the APK, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/android

trigger:
- master

pool:
  vmImage: 'macos-latest'

steps:
- task: Gradle@2
  inputs:
    workingDirectory: ''
    gradleWrapperFile: 'gradlew'
    gradleOptions: '-Xmx3072m'
    publishJUnitResults: false
    testResultsFiles: '**/TEST-*.xml'
    tasks: 'assembleDebug'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: chmod +x gradlew
    - run: ./gradlew build
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void GoPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/go.yml
            string yaml = @"
# Go
# Build your Go project.
# Add steps that test, save build artifacts, deploy, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/go

trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

variables:
  GOBIN:  '$(GOPATH)/bin' # Go binaries path
  GOROOT: '/usr/local/go1.11' # Go installation path
  GOPATH: '$(system.defaultWorkingDirectory)/gopath' # Go workspace path
  modulePath: '$(GOPATH)/src/github.com/$(build.repository.name)' # Path to the module's code

steps:
- script: |
    mkdir -p '$(GOBIN)'
    mkdir -p '$(GOPATH)/pkg'
    mkdir -p '$(modulePath)'
    shopt -s extglob
    shopt -s dotglob
    mv !(gopath) '$(modulePath)'
    echo '##vso[task.prependpath]$(GOBIN)'
    echo '##vso[task.prependpath]$(GOROOT)/bin'
  displayName: 'Set up the Go workspace'

- script: |
    go version
    go get -v -t -d ./...
    if [ -f Gopkg.toml ]; then
        curl https://raw.githubusercontent.com/golang/dep/master/install.sh | sh
        dep ensure
    fi
    go build -v .
  workingDirectory: '$(modulePath)'
  displayName: 'Get dependencies, then build'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  GOBIN: ${{ env.GOPATH }}/bin
  GOROOT: /usr/local/go1.11
  GOPATH: ${{ env.system.defaultWorkingDirectory }}/gopath
  modulePath: ${{ env.GOPATH }}/src/github.com/${{ env.build.repository.name }}
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Set up the Go workspace
      run: |
        mkdir -p '${{ env.GOBIN }}'
        mkdir -p '${{ env.GOPATH }}/pkg'
        mkdir -p '${{ env.modulePath }}'
        shopt -s extglob
        shopt -s dotglob
        mv !(gopath) '${{ env.modulePath }}'
        echo '##vso[task.prependpath]${{ env.GOBIN }}'
        echo '##vso[task.prependpath]${{ env.GOROOT }}/bin'
    - name: Get dependencies, then build
      run: |
        go version
        go get -v -t -d ./...
        if [ -f Gopkg.toml ]; then
            curl https://raw.githubusercontent.com/golang/dep/master/install.sh | sh
            dep ensure
        fi
        go build -v .
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void PythonPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'
strategy:
  matrix:
    Python35:
      PYTHON_VERSION: '3.5'
    Python36:
      PYTHON_VERSION: '3.6'
    Python37:
      PYTHON_VERSION: '3.7'
  maxParallel: 3

steps:
- task: UsePythonVersion@0
  inputs:
    versionSpec: '$(PYTHON_VERSION)'
    addToPath: true
    architecture: 'x64'
- task: PythonScript@0
  inputs:
    scriptSource: 'filePath'
    scriptPath: 'Python/Hello.py'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        PYTHON_VERSION:
        - 3.5
        - 3.6
        - 3.7
      max-parallel: 3
    steps:
    - uses: actions/checkout@v2
    - name: Setup Python ${{ matrix.PYTHON_VERSION }}
      uses: actions/setup-python@v1
      with:
        python-version: ${{ matrix.PYTHON_VERSION }}
    - run: python Python/Hello.py
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void MavenPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: Maven@3
  inputs:
    mavenPomFile: 'Maven/pom.xml'
    mavenOptions: '-Xmx3072m'
    javaHomeOption: 'JDKVersion'
    jdkVersionOption: '1.8'
    jdkArchitectureOption: 'x64'
    publishJUnitResults: true
    testResultsFiles: '**/surefire-reports/TEST-*.xml'
    goals: 'package'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: mvn -B package --file Maven/pom.xml
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void NodeJSPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: NodeTool@0
  inputs:
    versionSpec: '10.x'
  displayName: 'Install Node.js'

- script: |
    npm install
    npm start
  displayName: 'npm install and start'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Install Node.js
      uses: actions/setup-node@v1
      with:
        node-version: 10.x
    - name: npm install and start
      run: |
        npm install
        npm start
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void XamariniOSPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/xamarin.ios.yml
            string yaml = @"
# Xamarin.iOS
# Build a Xamarin.iOS project.
# Add steps that install certificates, test, sign, and distribute an app, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/xamarin

trigger:
- master

pool:
  vmImage: 'macos-latest'

steps:
# To manually select a Xamarin SDK version on the Microsoft-hosted macOS agent,
# configure this task with the *Mono* version that is associated with the
# Xamarin SDK version that you need, and set the ""enabled"" property to true.
# See https://go.microsoft.com/fwlink/?linkid=871629
- script: sudo $AGENT_HOMEDIRECTORY/scripts/select-xamarin-sdk.sh 5_12_0
  displayName: 'Select the Xamarin SDK version'
  enabled: false

- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '**/*.sln'

- task: XamariniOS@2
  inputs:
    solutionFile: '**/*.sln'
    configuration: 'Release'
    buildForSimulator: true
    packageApp: false
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - name: Select the Xamarin SDK version
      run: sudo $AGENT_HOMEDIRECTORY/scripts/select-xamarin-sdk.sh 5_12_0
    - #: 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  **/*.sln
      shell: powershell
    - run: |
        cd Blank
        nuget restore
        cd Blank.Android
        msbuild  /verbosity:normal /t:Rebuild /p:Platform=iPhoneSimulator /p:Configuration=Release
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void AzureFunctionAppContainerPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/xamarin.ios.yml
            string yaml = @"
# Docker image, Azure Container Registry, and Azure Functions app
# Build a Docker image, push it to an Azure Container Registry, and deploy it to an Azure Functions app.
# https://docs.microsoft.com/azure/devops/pipelines/languages/docker

trigger:
- master

resources:
- repo: self

variables:
  # ========================================================================
  #                          Mandatory variables 
  # ========================================================================

 # Update Azure.ResourceGroupName value with Azure resource group name.
  Azure.ResourceGroupName: '{{#toAlphaNumericString repositoryName 50}}{{/toAlphaNumericString}}'

  # Update Azure.ServiceConnectionId value with AzureRm service endpoint.
  Azure.ServiceConnectionId: '{{ azureServiceConnectionId }}'

  # Update Azure.Location value with Azure Location.
  Azure.Location: 'eastus'

  # Update ACR.Name value with ACR name. Please note ACR names should be all lower-case and alphanumeric only.
  ACR.Name: '{{#toAlphaNumericString repositoryName 46}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  
  # Update FunctionApp.Name value with a name that identifies your new function app. Valid characters are a-z, 0-9, and -.
  FunctionApp.Name: '{{#toAlphaNumericString repositoryName 46}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  
  # Update StorageAccount.Name value with Storage account name. Storage account names must be between 3 and 24 characters in length and use numbers and lower-case letters only.
  StorageAccount.Name: '{{#toAlphaNumericString repositoryName 20}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  
  # Update ServicePlan.Name value with a name of the app service plan.
  ServicePlan.Name: '{{#toAlphaNumericString repositoryName 45}}{{/toAlphaNumericString}}-plan'

  # ========================================================================
  #                           Optional variables 
  # ========================================================================

  ACR.ImageName: '$(ACR.Name):$(Build.BuildId)'
  ACR.FullName: '$(ACR.Name).azurecr.io'
  ACR.Sku: 'Standard'
  Azure.CreateResources: 'true' # Update Azure.CreateResources to false if you have already created resources like resource group and azure container registry.
  System.Debug: 'false'

jobs:

- job: CreateResources
  displayName: Create resources
  condition: and(succeeded(), eq(variables['Azure.CreateResources'], 'true'))

  pool:
    vmImage: 'ubuntu-latest'

  steps:
  - task: AzureResourceGroupDeployment@2
    displayName: 'Azure Deployment:Create Azure Container Registry, Azure WebApp Service'
    inputs:
      azureSubscription: '$(Azure.ServiceConnectionId)'
      resourceGroupName: '$(Azure.ResourceGroupName)'
      location: '$(Azure.Location)'
      templateLocation: 'URL of the file'
      csmFileLink: 'https://raw.githubusercontent.com/Microsoft/azure-pipelines-yaml/master/templates/resources/arm/functionapp.json'
      overrideParameters: '-registryName ""$(ACR.Name)"" -registryLocation ""$(Azure.Location)"" -functionAppName ""$(FunctionApp.Name)"" -hostingPlanName ""$(ServicePlan.Name)"" -storageAccountName ""$(StorageAccount.Name)""'

- job: BuildImage
  displayName: Build
  dependsOn: CreateResources
  condition: or(succeeded(), ne(variables['Azure.CreateResources'], 'true'))

  pool:
    vmImage: 'ubuntu-latest'

  steps:
  - task: Docker@1
    displayName: 'Build an image'
    inputs:
      azureSubscriptionEndpoint: '$(Azure.ServiceConnectionId)'
      azureContainerRegistry: '$(ACR.FullName)'
      imageName: '$(ACR.ImageName)'
      command: build
      dockerFile: '**/Dockerfile'

  - task: Docker@1
    displayName: 'Push an image'
    inputs:
      azureSubscriptionEndpoint: '$(Azure.ServiceConnectionId)'
      azureContainerRegistry: '$(ACR.FullName)'
      imageName: '$(ACR.ImageName)'
      command: push

- job: DeployApp
  displayName: Deploy
  dependsOn: BuildImage
  condition: succeeded()

  pool:
    vmImage: 'ubuntu-latest'

  steps:
  - task: AzureFunctionAppContainer@1
    displayName: 'Azure Function App on Container Deploy: $(FunctionApp.Name)'
    inputs:
      azureSubscription: '$(Azure.ServiceConnectionId)'
      appName: $(FunctionApp.Name)
      imageName: '$(ACR.FullName)/$(ACR.ImageName)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets
on:
  push:
    branches:
    - master
env:
  Azure.ResourceGroupName: '{{#toAlphaNumericString repositoryName 50}}{{/toAlphaNumericString}}'
  Azure.ServiceConnectionId: '{{ azureServiceConnectionId }}'
  Azure.Location: eastus
  ACR.Name: '{{#toAlphaNumericString repositoryName 46}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  FunctionApp.Name: '{{#toAlphaNumericString repositoryName 46}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  StorageAccount.Name: '{{#toAlphaNumericString repositoryName 20}}{{/toAlphaNumericString}}{{#shortGuid}}{{/shortGuid}}'
  ServicePlan.Name: '{{#toAlphaNumericString repositoryName 45}}{{/toAlphaNumericString}}-plan'
  ACR.ImageName: ${{ env.ACR.Name }}:${{ env.Build.BuildId }}
  ACR.FullName: ${{ env.ACR.Name }}.azurecr.io
  ACR.Sku: Standard
  Azure.CreateResources: true
  System.Debug: false
jobs:
  CreateResources:
    name: Create resources
    runs-on: ubuntu-latest
    if: and(success(),eq(variables['Azure.CreateResources'], 'true'))
    steps:
    - uses: actions/checkout@v2
    - #: ""Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets""
      name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_SP }}
    - name: Azure Deployment:Create Azure Container Registry, Azure WebApp Service
      uses: Azure/github-actions/arm@master
      env:
        AZURE_RESOURCE_GROUP: ${{ env.Azure.ResourceGroupName }}
        AZURE_TEMPLATE_LOCATION: 
        AZURE_TEMPLATE_PARAM_FILE: 
  BuildImage:
    name: Build
    runs-on: ubuntu-latest
    needs: CreateResources
    if: or(success(),ne(variables['Azure.CreateResources'], 'true'))
    steps:
    - uses: actions/checkout@v2
    - name: Build an image
      run: docker build . --file **/Dockerfile --tag
    - name: Push an image
      run: docker build . --file  --tag
  DeployApp:
    name: Deploy
    runs-on: ubuntu-latest
    needs: BuildImage
    if: success()
    steps:
    - uses: actions/checkout@v2
    - name: 'Azure Function App on Container Deploy: ${{ env.FunctionApp.Name }}'
      uses: Azure/webapps-deploy@v2
      with:
        app-name: ${{ env.FunctionApp.Name }}
        images: ${{ env.ACR.FullName }}/${{ env.ACR.ImageName }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void XamarinAndroidPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: 
            string yaml = @"
# Xamarin.Android
# Build a Xamarin.Android project.
# Add steps that test, sign, and distribute an app, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/xamarin

trigger:
- master

pool:
  vmImage: 'macos-latest'

variables:
  buildConfiguration: 'Release'
  outputDirectory: '$(build.binariesDirectory)/$(buildConfiguration)'

steps:
- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '**/*.sln'

- task: XamarinAndroid@1
  inputs:
    projectFile: '**/*droid*.csproj'
    outputDirectory: '$(outputDirectory)'
    configuration: '$(buildConfiguration)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  buildConfiguration: Release
  outputDirectory: ${{ env.build.binariesDirectory }}/${{ env.buildConfiguration }}
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - #: 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  **/*.sln
      shell: powershell
    - run: |
        cd Blank
        nuget restore
        cd Blank.Android
        msbuild **/*droid*.csproj /verbosity:normal /t:Rebuild /p:Configuration=${{ env.buildConfiguration }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void TestJobsWithAzurePipelineYamlToObject()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master
variables:
  buildConfiguration: Release
  vmImage: ubuntu-latest
jobs:
- job: Build
  displayName: Build job
  pool: 
    vmImage: ubuntu-latest
  timeoutInMinutes: 23
  variables:
    buildConfiguration: Debug
    myJobVariable: 'data'
    myJobVariable2: 'data2'
  steps: 
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 1
- job: Build2
  displayName: Build job
  dependsOn: Build
  pool: 
    vmImage: ubuntu-latest
  variables:
    myJobVariable: 'data'
  steps:
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 2
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 3";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  buildConfiguration: Release
  vmImage: ubuntu-latest
jobs:
  Build:
    name: Build job
    runs-on: ubuntu-latest
    timeout-minutes: 23
    env:
      buildConfiguration: Debug
      myJobVariable: data
      myJobVariable2: data2
    steps:
    - uses: actions/checkout@v2
    - name: dotnet build part 1
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
  Build2:
    name: Build job
    runs-on: ubuntu-latest
    needs: Build
    env:
      myJobVariable: data
    steps:
    - uses: actions/checkout@v2
    - name: dotnet build part 2
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
    - name: dotnet build part 3
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void TestHTMLPipeline()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
# HTML
# Archive your static HTML project and save it with the build record.

trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: ArchiveFiles@2
  inputs:
    rootFolderOrFile: '$(build.sourcesDirectory)'
    includeRootFolder: false
- task: PublishBuildArtifacts@1";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/marketplace/actions/create-zip-file
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - #: 'Note: This is a third party action: https://github.com/marketplace/actions/create-zip-file'
      uses: montudor/action-zip@v0.1.0
      with:
        args: zip -qq -r  ${{ env.build.sourcesDirectory }}
    - uses: actions/upload-artifact@master
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void AntPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: Ant@1
  inputs:
    workingDirectory: ''
    buildFile: 'build.xml'
    javaHomeOption: 'JDKVersion'
    jdkVersionOption: '1.8'
    jdkArchitectureOption: 'x64'
    publishJUnitResults: true
    testResultsFiles: '**/TEST -*.xml'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: ant -noinput -buildfile build.xml
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void RubyPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseRubyVersion@0
  inputs:
    versionSpec: '>= 2.5'
- script: ruby HelloWorld.rb
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup Ruby >= 2.5
      uses: actions/setup-ruby@v1
      with:
        ruby-version: '>= 2.5'
    - run: ruby HelloWorld.rb
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void AzureARMTemplatePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
jobs:
- job: Deploy
  displayName: Deploy job
  pool:
    vmImage: ubuntu-latest
  variables:
    AppSettings.Environment: 'data'
    ArmTemplateResourceGroupLocation: 'eu'
    ResourceGroupName: 'MyProjectRG'
  steps:
  - task: DownloadBuildArtifacts@0
    displayName: 'Download the build artifacts'
    inputs:
      buildType: 'current'
      downloadType: 'single'
      artifactName: 'drop'
      downloadPath: '$(build.artifactstagingdirectory)'
  - task: AzureResourceGroupDeployment@2
    displayName: 'Deploy ARM Template to resource group'
    inputs:
      azureSubscription: 'connection to Azure Portal'
      resourceGroupName: $(ResourceGroupName)
      location: '[resourceGroup().location]'
      csmFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.json'
      csmParametersFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.parameters.json'
      overrideParameters: '-environment $(AppSettings.Environment) -locationShort $(ArmTemplateResourceGroupLocation)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets
jobs:
  Deploy:
    name: Deploy job
    runs-on: ubuntu-latest
    env:
      AppSettings.Environment: data
      ArmTemplateResourceGroupLocation: eu
      ResourceGroupName: MyProjectRG
    steps:
    - uses: actions/checkout@v2
    - #: ""Note that 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets""
      name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_SP }}
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
    - name: Deploy ARM Template to resource group
      uses: Azure/github-actions/arm@master
      env:
        AZURE_RESOURCE_GROUP: ${{ env.ResourceGroupName }}
        AZURE_TEMPLATE_LOCATION: ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.json
        AZURE_TEMPLATE_PARAM_FILE: ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.parameters.json
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }
    }
}