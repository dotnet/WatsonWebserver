# SSL Certificates using OpenSSL

You may encounter issues when attempting to add an SSL certificate using ```netsh``` when the certificate was imported using the Certificates snap-in in the MMC.  This often results in an error 1312, as shown below.

```
C:\>netsh http add sslcert hostnameport="[hostname]:[port]" certhash="[hash]" appid="{[guid]}" certstore=My

SSL Certificate add failed, Error: 1312
A specified logon session does not exist. It may already have been terminated.
```

This is generally caused by one of two issues:

1) The certificate hash, when copied from the certificate details pane, contains extranneous non-ANSI characters.  It is best to **manually type the hash** instead of copying and pasting

2) The certificate you are attempting to install does not contain a private key

Given some inconsistencies in how CSRs are generated from IIS and the Certificates MMC snap-in, refer to instructions below for:

1) Generating a CSR using OpenSSL
2) Converting the received certificate to a PFX file, including the private key
3) Installing the certificate

## OpenSSL CSR Generation

Refer to this [link](https://www.ssl.com/how-to/manually-generate-a-certificate-signing-request-csr-using-openssl/) for more information.

You will need to have OpenSSL installed.  Alternatively, use the ```openssl.exe``` found in the git directory in Program Files, i.e. ```C:\Program Files\Git\usr\bin```

```
C:\> openssl req -newkey rsa:2048 -keyout priv.key -out csr.txt
```

Make sure you specify the hostname for ```Common Name```.

Use the created ```csr.txt``` with your SSL certificate provider as your certificate request.

## Converting Certificate to PFX and Including Private Key

Refer to this [link](https://www.networkinghowtos.com/howto/convert-certificate-file-from-crt-to-pfx-using-openssl/) for more information.

- ```cert.crt``` is provided by your SSL certificate provider
- ```priv.key``` is generated in the prior step

```
C:\> openssl pkcs12 -export -out cert.pfx -inkey priv.key -in cert.crt
```

## Installing the Certificate

Use the Certificates MMC snap-in to install the certificate.  It is best practice to install in the 'Computer account' under the 'Personal' store.
