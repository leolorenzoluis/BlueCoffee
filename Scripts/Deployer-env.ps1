# Script written to be dot-sourced to help with building and deploying the Azure service packages.
# Assumes that you have Azure Powershell module installed, and the current subscription is set to a
# a valid subscription.

function Get-ScriptDirectory
{
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}

$rootDirectory = Split-Path $(Get-ScriptDirectory)
$solutionDirectory = $rootDirectory
$LocalJDK = "$rootDirectory\JDK\jdk.zip"

function Ensure-NugetRestored
{
	pushd $solutionDirectory
	nuget restore
	popd
}

function Build-TestService($testServiceName, $flavor = 'Release')
{
	Write-Host "Building package..."
	Ensure-NugetRestored
	pushd "$solutionDirectory\TestServices\$testServiceName\$testServiceName"
	$buildOutput = msbuild "$testServiceName.ccproj" /t:Publish "/p:Configuration=$flavor" /p:Platform="AnyCPU" /p:VisualStudioVersion="12.0"
	if ($LASTEXITCODE -ne 0)
	{
		$buildOutput | Write-Host
	}
	popd
}

function Discover-AccountsForLocation($location)
{
	Trap
	{
		return $_
	}
	Get-AzureStorageAccount | ?{$_.GeoPrimaryLocation -eq $location}
}

function Discover-Accounts($serviceName)
{
	Trap
	{
		return $_
	}
	$service = Get-AzureService $serviceName
	Discover-AccountsForLocation $service.Location
}

function Contextify-Account($storageAccount)
{
    if ($storageAccount -is [String] -or $storageAccount -is [Microsoft.WindowsAzure.Commands.ServiceManagement.Model.StorageServicePropertiesOperationContext])
    {
        $storageAccount = New-AzureStorageContext -ConnectionString $(Get-ConnectionString $storageAccount)
    }
    return $storageAccount
}

function Get-ConnectionString($storageAccount)
{
    if ($storageAccount -is [String])
    {
        $storageAccountName = $storageAccount
    }
    else
    {
	    $storageAccountName = $storageAccount.StorageAccountName
    }
    $key = Get-AzureStorageKey $storageAccountName
	"DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$($key.Primary)"
}

function Download-JDK()
{
    If (-not $(Test-Path $LocalJDK))
    {
        Write-Host "Downloading JDK..."
        $JDKDownloadUri = 'http://cdn.azulsystems.com/zulu/2014-10-8.4-bin/zulu1.8.0_25-8.4.0.1-win64.zip'
        $OutputDirectory = Split-Path $LocalJDK
        If (-not $(Test-Path $OutputDirectory))
        {
            $MDOutput = md $OutputDirectory
        }
        $WebRequest = [System.Net.WebRequest]::CreateHttp($JDKDownloadUri)
        $WebRequest.Referer = 'http://www.azulsystems.com/products/zulu/downloads'
        $WebResponse = [System.Net.HttpWebResponse]$WebRequest.GetResponse()
        If ($WebResponse.StatusCode -ne [System.Net.HttpStatusCode]::OK)
        {
            Throw $WebResponse.StatusDescription
        }
        $ResponseStream = $WebResponse.GetResponseStream()
        $FileStream = [System.IO.File]::Create($LocalJDK)
        $ResponseStream.CopyTo($FileStream)
        $FileStream.Close()
        $ResponseStream.Close()
    }
}

function Upload-Resource($storageContext, $blobNamePrefix, $container, $fileToUpload)
{
    $blobName = $blobNamePrefix + $fileToUpload.Name
    $existingBlob = Get-AzureStorageBlob -Blob $blobName -Context $storageContext -Container $container -ErrorAction SilentlyContinue
    if (($existingBlob -eq $null) -or ($existingBlob.Length -ne $fileToUpload.Length))
    {
        Write-Host "Uploading $blobName ..."
        $newBlob = Set-AzureStorageBlobContent -Blob $blobName -Context $storageContext -Container $container -File $($fileToUpload.FullName) -Force
    }
}

function Upload-ResourcesToContext($storageContext)
{
    Download-JDK
    $storageContext = Contextify-Account $storageContext
	Write-Host "Uploading resources..."
    $container = 'bluecoffeeresources'
    $containerReference = New-AzureStorageContainer -Name $container -Context $storageContext -ErrorAction SilentlyContinue
    $libraryPrefix = "Microsoft.Experimental.Azure."
    $libraryDirectories = Get-ChildItem "$rootDirectory\Libraries" | ?{$_.Name.StartsWith($libraryPrefix) -and -not $_.Name.EndsWith('JavaPlatform')};
    $libraryDirectories | %{
        $blobNamePrefix = $_.Name.Substring($libraryPrefix.Length) + "/"
        $myResources = Get-ChildItem "$($_.FullName)\Resources" | ?{$_.Extension -eq ".zip"}
        $myResources | %{
            Upload-Resource $storageContext $blobNamePrefix $container $_
        }
    }
    Upload-Resource $storageContext 'JavaPlatform/' $container $(Get-Item $LocalJDK)
}

function Upload-Resources($storageAccount)
{
    $storageAccount = Contextify-Account $storageAccount
    Upload-ResourcesToContext $storageAccount
}

function Upload-ResourcesToLocal()
{
    Upload-ResourcesToContext $(New-AzureStorageContext -ConnectionString "UseDevelopmentStorage=true")
}

function Delete-ExistingDeployments([Parameter(Mandatory=$true)]$serviceName)
{
	Write-Host "Deleting existing deployments..."
	try
	{
		$removeOutput = Remove-AzureDeployment -ServiceName $serviceName -Slot Production -Force -DeleteVHD
	} catch {}
}

function Deploy-TestService(
	[Parameter(Mandatory=$true)]$testServiceName,
	[Parameter(Mandatory=$true)]$serviceName,
	$storageAccount = $null,
	$flavor = 'Release',
	[Switch]$upgradeInPlace)
{
	Trap
	{
		return $_
	}
	$publishDirectory = "$solutionDirectory\TestServices\$testServiceName\$testServiceName\bin\Release\app.publish"
	if ($storageAccount -eq $null)
	{
		Write-Host "Discovering storage account..."
		$storageAccount = $(Discover-Accounts $serviceName)[0]
	}
    $storageAccount = Contextify-Account $storageAccount
    Upload-Resources $storageAccount
	Write-Host "Constructing connection string..."
	$connectionString = Get-ConnectionString $storageAccount
	Write-Host "Writing configuration file..."
	$tempConfigFile = "$env:TEMP\TestServiceFinalSettings.cscfg"
	if (Test-Path $tempConfigFile)
	{
		Remove-Item $tempConfigFile -Force
	}
	Get-Content "$publishDirectory\ServiceConfiguration.Cloud.cscfg" |
		%{$_ -replace "UseDevelopmentStorage=true",$connectionString} > $tempConfigFile
	Write-Host "Deploying..."
	if ($upgradeInPlace -and ($existingDeployment = Get-AzureDeployment $serviceName -Sl Production -ErrorAction Ignore))
	{
		$deployment = Set-AzureDeployment -ServiceName $serviceName -Package "$publishDirectory\$testServiceName.cspkg" -Configuration $tempConfigFile -Label "Deployment on $(Get-Date)" -Slot Production -Upgrade -Force
	}
	else
	{
		Delete-ExistingDeployments $serviceName
        Write-Host "Creating new deployment..."
		$deployment = New-AzureDeployment -ServiceName $serviceName -Package "$publishDirectory\$testServiceName.cspkg" -Configuration $tempConfigFile -Label "Deployment on $(Get-Date)" -Slot Production
	}
}

function BuildAndDeploy([Parameter(Mandatory=$true)]$testServiceName,
	[Parameter(Mandatory=$true)]$serviceName,
	$storageAccount = $null,
	[Switch]$upgradeInPlace)
{
	Trap
	{
		return $_
	}
	Build-TestService $testServiceName
	if ($LASTEXITCODE -eq 0)
	{
		Deploy-TestService $testServiceName $serviceName -storageAccount $storageAccount -upgradeInPlace:$upgradeInPlace
	}
}
