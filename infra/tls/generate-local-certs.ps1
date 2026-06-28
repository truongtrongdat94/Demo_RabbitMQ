param(
    [string] $OutputDirectory = "$PSScriptRoot\generated",
    [int] $ServerCertificateDays = 825,
    [int] $CaCertificateDays = 3650
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$resolvedOutputDirectory = (Resolve-Path $OutputDirectory).Path
$localOpenSsl = Get-Command openssl -ErrorAction SilentlyContinue
$useDockerOpenSsl = $null -eq $localOpenSsl

if ($useDockerOpenSsl -and -not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "OpenSSL is not on PATH and Docker is not available for the fallback OpenSSL runner."
}

function Invoke-OpenSsl {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    if ($useDockerOpenSsl) {
        $volumeMount = "${resolvedOutputDirectory}:/tls"
        & docker run --rm -v $volumeMount -w /tls rabbitmq:4.3-management openssl @Arguments
    }
    else {
        & openssl @Arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "openssl $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$caKey = "ca.key"
$caCert = "ca.crt"
$caConfig = "ca.cnf"
$serverKey = "server.key"
$serverCsr = "server.csr"
$serverCert = "server.crt"
$serverConfig = "server.cnf"
$caSerial = Join-Path $resolvedOutputDirectory "ca.srl"

@"
[ req ]
prompt = no
default_bits = 4096
default_md = sha256
distinguished_name = dn
x509_extensions = v3_ca

[ dn ]
CN = IoT Data Pipeline Local CA

[ v3_ca ]
basicConstraints = critical, CA:true
keyUsage = critical, keyCertSign, cRLSign
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always, issuer
"@ | Set-Content -Path (Join-Path $resolvedOutputDirectory $caConfig) -Encoding ascii

@"
[ req ]
prompt = no
default_bits = 2048
default_md = sha256
distinguished_name = dn
req_extensions = req_ext

[ dn ]
CN = rabbitmq-lb

[ req_ext ]
basicConstraints = CA:false
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[ alt_names ]
DNS.1 = rabbitmq-lb
DNS.2 = rabbitmq-1
DNS.3 = rabbitmq-2
DNS.4 = rabbitmq-3
DNS.5 = localhost
IP.1 = 127.0.0.1
"@ | Set-Content -Path (Join-Path $resolvedOutputDirectory $serverConfig) -Encoding ascii

if (Test-Path $caSerial) {
    Remove-Item -LiteralPath $caSerial -Force
}

Push-Location $resolvedOutputDirectory
try {
    Invoke-OpenSsl @("genrsa", "-out", $caKey, "4096")
    Invoke-OpenSsl @("req", "-x509", "-new", "-nodes", "-key", $caKey, "-sha256", "-days", "$CaCertificateDays", "-out", $caCert, "-config", $caConfig)

    Invoke-OpenSsl @("genrsa", "-out", $serverKey, "2048")
    Invoke-OpenSsl @("req", "-new", "-key", $serverKey, "-out", $serverCsr, "-config", $serverConfig)
    Invoke-OpenSsl @("x509", "-req", "-in", $serverCsr, "-CA", $caCert, "-CAkey", $caKey, "-CAcreateserial", "-out", $serverCert, "-days", "$ServerCertificateDays", "-sha256", "-extensions", "req_ext", "-extfile", $serverConfig)
}
finally {
    Pop-Location
}

if ($useDockerOpenSsl) {
    $volumeMount = "${resolvedOutputDirectory}:/tls"
    & docker run --rm -v $volumeMount -w /tls rabbitmq:4.3-management sh -c "chmod 0644 ca.crt server.crt server.key && chmod 0600 ca.key"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update generated certificate permissions with exit code $LASTEXITCODE"
    }
}
elseif (Get-Command chmod -ErrorAction SilentlyContinue) {
    & chmod 0644 (Join-Path $resolvedOutputDirectory $caCert) (Join-Path $resolvedOutputDirectory $serverCert) (Join-Path $resolvedOutputDirectory $serverKey)
}

Write-Host "Generated local TLS materials:"
Write-Host "  CA certificate:     $(Join-Path $resolvedOutputDirectory $caCert)"
Write-Host "  Server certificate: $(Join-Path $resolvedOutputDirectory $serverCert)"
Write-Host "  Server key:         $(Join-Path $resolvedOutputDirectory $serverKey)"
Write-Host ""
Write-Host "These files are for local demo only and are ignored by git."
