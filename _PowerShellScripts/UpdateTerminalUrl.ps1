﻿param(
    [Parameter(Mandatory = $true)]
	[string]$connectionString,

	[Parameter(Mandatory = $true)]
	[string]$newHostname,

	[Parameter(Mandatory = $false)]
	[string]$overrideDbName
)

Write-Host "Update terminal URLs to $newHostname"

$commandText = "
IF (EXISTS (SELECT * 
                 FROM INFORMATION_SCHEMA.TABLES 
                 WHERE TABLE_SCHEMA = 'dbo' 
                 AND  TABLE_NAME = 'TerminalRegistration'))
BEGIN
	UPDATE TerminalRegistration SET [Endpoint] = 
			REPLACE([Endpoint], 'localhost' COLLATE SQL_Latin1_General_Cp1_CS_AS, '$newHostname') 
			where 
			[Endpoint] like 'localhost:%'
			or [Endpoint] like '%//localhost:%'
END";


Write-Host $commandText

if ([System.String]::IsNullOrEmpty($overrideDbName) -ne $true) {
	$builder = new-object system.data.SqlClient.SqlConnectionStringBuilder($connectionString)
	$builder["Initial Catalog"] = $overrideDbName
	$connectionString = $builder.ToString()
}

$connection = new-object system.data.SqlClient.SQLConnection($connectionString)

$command = new-object system.data.sqlclient.sqlcommand($commandText, $connection)
$connection.Open()
$command.CommandTimeout = 20 #20 seconds

$command.ExecuteNonQuery()
Write-Host "Successfully updated terminal hostname."

$connection.Close()