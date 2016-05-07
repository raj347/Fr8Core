<#
    .SYNOPSIS
    The script updates the status of build on GitHub     
#>

param(
	[Parameter(Mandatory = $true)]
	[string]$vso_username,
	
	[Parameter(Mandatory = $true)]
	[string]$vso_password,

	[Parameter(Mandatory = $true)]
	[string]$github_username,

	[Parameter(Mandatory = $true)]
	[string]$github_password,

	[string]$buildDefinitionId = $env:SYSTEM_DEFINITIONID,
    [string]$buildId = $env:BUILD_BUILDID,
	[string]$branchName = $env:BUILD_SOURCEBRANCHNAME
)

$target_url = "https://fr8.visualstudio.com/DefaultCollection/fr8/_build?_a=summary&buildId=" + $buildId
[uri]$vsoTimelineUri = "https://fr8.visualstudio.com/defaultcollection/fr8/_apis/build/builds/" + $buildId + "/timeline?api-version=2.0"
[uri]$vsoBuildDefinitionUri = "https://fr8.visualstudio.com/defaultcollection/fr8/_apis/build/definitions/" + $buildDefinitionId + "?api-version=2.0"

$failure = @{
				state = "failure"
				target_url = $target_url
				description = "The build failed"
				context = "feature-branch-ci/vso"
			} | ConvertTo-Json

$success = @{
				state = "success"
				target_url = $target_url
				description = "The build succeeded!"
				context = "feature-branch-ci/vso"
			} | ConvertTo-Json


$basicAuthVSO = ("{0}:{1}" -f $vso_username,$vso_password)
$basicAuthVSO = [System.Text.Encoding]::UTF8.GetBytes($basicAuthVSO)
$basicAuthVSO = [System.Convert]::ToBase64String($basicAuthVSO)
$headersVSO = @{Authorization=("Basic {0}" -f $basicAuthVSO)}

$basicAuthGitHub = ("{0}:{1}" -f $github_username,$github_password)
$basicAuthGitHub = [System.Text.Encoding]::UTF8.GetBytes($basicAuthGitHub)
$basicAuthGitHub = [System.Convert]::ToBase64String($basicAuthGitHub)
$headersGitHub = @{Authorization=("Basic {0}" -f $basicAuthGitHub)}


Function UpdateGitHubBuildStatus($message)
{
	$sourceVersion = Invoke-Expression "git rev-parse origin/$branchName" | Out-String
	$sourceVersion = $sourceVersion.Trim()

	if ($LastExitCode -ne 0)
	{
		Write-Host "Failed to get latest commit hash."
        Write-Host "StatusCode:" $_.Exception.Response.StatusCode.value__ 
        Write-Host "StatusDescription:" $_.Exception.Response.StatusDescription
		
        exit 1;
	}
	else
	{
        Write-Host "Latest commit for branch $branchName is $sourceVersion"
		[uri]$githubRequestUri = "https://api.github.com/repos/alexed1/fr8company/statuses/" + $sourceVersion

		$githubResponse = try
		{
			$response = Invoke-RestMethod -Uri $githubRequestUri -headers $headersGitHub -Method Post -Body $message -ContentType 'application/json'
		}
		catch
		{
			Write-Host "Failed to update build status."
            Write-Host "StatusCode:" $_.Exception.Response.StatusCode.value__ 
            Write-Host "StatusDescription:" $_.Exception.Response.StatusDescription
			exit 1;
		}
	}	
}

$vsoBDResponse = try
{
	Invoke-RestMethod -Uri $vsoBuildDefinitionUri -headers $headersVSO -Method Get -ContentType 'application/json'
}
catch
{
	Write-Host "Failed to get current build definition info."
    Write-Host "StatusCode:" $_.Exception.Response.StatusCode.value__ 
    Write-Host "StatusDescription:" $_.Exception.Response.StatusDescription
	exit 1
}

# get all enabled build steps. 
# Note: "Get Souces" step is not included in the response
$activeBuildSteps = $vsoBDResponse.build | where {$_.enabled -eq "true"}

$vsoResponse = try
{
	Invoke-RestMethod -Uri $vsoTimelineUri -headers $headersVSO -Method Get -ContentType 'application/json'
}
catch
{
	Write-Host "Failed to get current build timeline."
    Write-Host "StatusCode:" $_.Exception.Response.StatusCode.value__ 
    Write-Host "StatusDescription:" $_.Exception.Response.StatusDescription
	exit 1
}
$activeTasks = $vsoResponse.records | where {$_.type -eq "Task" -and $_.name -notlike "Update GitHub status*"}
$succeededBuildSteps = $activeTasks | where {$_.state -eq "completed" -and $_.result -eq "succeeded"}

Write-Host "Comparing succeded steps with active build steps count " $succeededBuildSteps.Count " - " $activeBuildSteps.Count

if ($succeededBuildSteps.Count -eq $activeBuildSteps.Count)
{
    UpdateGitHubBuildStatus -message $success
}
else
{
    UpdateGitHubBuildStatus -message $failure
}

