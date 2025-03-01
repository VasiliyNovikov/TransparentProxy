# Demo of Transparent Proxy with SSL Bump

## Some useful scripts

### Allow app listening privileged ports on Windows
In PowerShell (Admin):

```powershell
netsh http add urlacl url=http://+:80/ user=<your username>
netsh http add urlacl url=https://+:443/ user=<your username>
```

### Trust self-signed root CA on Windows
In PowerShell (Admin):

```powershell
Import-PfxCertificate -FilePath <you PFX root CA path> -CertStoreLocation "Cert:\LocalMachine\Root"
```