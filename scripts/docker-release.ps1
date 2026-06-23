param(
    [Parameter(Mandatory = $true)]
    [string]$Registry,

    [string]$Tag = "1.0.0",

    [switch]$Push,

    [switch]$Pull
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$images = @(
    @{ Name = "iot-data-pipeline-rabbitmq"; Context = "infra/rabbitmq" },
    @{ Name = "iot-data-pipeline-mongodb"; Context = "infra/mongodb" },
    @{ Name = "iot-data-pipeline-node-red"; Context = "simulators/node-red-publisher" },
    @{ Name = "iot-data-pipeline-consumer-service"; Context = "apps/consumer-service" }
)

foreach ($image in $images) {
    $fullName = "$Registry/$($image.Name):$Tag"
    $context = Join-Path $root $image.Context
    $pullArgs = @()

    if ($Pull) {
        $pullArgs += "--pull"
    }

    Write-Host "Building $fullName from $($image.Context)"
    docker build @pullArgs -t $fullName $context

    if ($LASTEXITCODE -ne 0) {
        throw "docker build failed for $fullName"
    }
}

if ($Push) {
    foreach ($image in $images) {
        $fullName = "$Registry/$($image.Name):$Tag"
        Write-Host "Pushing $fullName"
        docker push $fullName

        if ($LASTEXITCODE -ne 0) {
            throw "docker push failed for $fullName"
        }
    }
}

Write-Host "Release image set is ready."
Write-Host "Registry: $Registry"
Write-Host "Tag:      $Tag"
