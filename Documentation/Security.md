# Security

Best practices for securing your Watson webserver-based application.

## Use Common Sense

First things first, use common sense.  Some basics that you should hopefully already be doing:

- Deploy on a server that is behind a firewall
- Make sure the server's operating system firewall is operational and not disabled
- Do not use simple passwords anywhere

## Firewall Configuration

For every prefix on which Watson listens, the corresponding TCP port must be permitted through every firewall between your application and your users.  That is, if Watson is listening on ports ```8000-8003```, your network firewalls AND your operating system firewalls must permit these ports to pass.

Further, if your firewall is filtering based on IP address or subnet, you must have permit rules enabling your users to communicate with Watson.

## Listener Prefix Configuration

Prefixes are the hostname and port combinations on which Watson will listen.  You should only use:

- ```127.0.0.1```
- ```localhost```
- An actual interface IP address
- ```*```, ```+```, or ```0.0.0.0``` (any IP address)

If you listen on anything other than ```127.0.0.1``` or ```localhost```, you will likely have to run Watson with elevated (administrative) privileges.  

Incoming HTTP requests will have a ```HOST``` header, and the value for this header **must** match one of the bindings.

## Access Control Lists

Watson by default permits connections from any endpoint. 

### Blocking Specific IP Addresses or Networks
```csharp
server.AccessControl.DenyList.Add("1.1.1.1", "255.255.255.255"); // block a specific host
server.AccessControl.DenyList.Add("1.1.1.0", "255.255.255.0");   // block a network
```

### Changing from Default-Permit to Default-Deny
```csharp
server.AccessControl.Mode = AccessControlMode.DefaultDeny;
server.AccessControl.PermitList.Add("1.1.1.1", "255.255.255.255"); // permit a specific host
server.AccessControl.PermitList.Add("1.1.1.0", "255.255.255.0");   // permit a network
```

## SSL

To enable SSL, set ```ssl``` to ```true``` in the constructor.
```csharp
Server server = new Server("127.0.0.1", 8000, true, DefaultRoute);
server.Start();
```

Alternatively, set the Enable parameter of Ssl to true before calling ```Start()```.
```csharp
server.Settings.Ssl.Enable = true;
server.Start();
```

Using SSL with Watson on Windows requires that the certificate be installed in the Windows Certificate Manager MMC snap-in.  It is easiest to use IIS to generate your CSR, and then once you have your certificate and private key, import the certificate and private key into the Computer Account certificate store.  

Once the certificate has been imported, double click the certificate (from within MMC) and go to the 'Details' tab to retrieve the certificate thumbprint.

Copy this value and paste it into Notepad.

![Certificate Manager](https://github.com/jchristn/WatsonWebserver/blob/master/assets/certmgr.png)

### VERY IMPORTANT

You MUST use certificates installed in the Computer account and not the User account.

When copying from the Certificate Manager window into Notepad, there is often a special character, hidden from view, that is pasted into Notepad, that will cause you trouble.  As a result, I like to manually type in the first two characters and the last two characters, paste in the rest (after copying from the ```Thumbprint``` in the properties window), and then removing all whitespace.  

Next, you will need to use the ```netsh``` command to associate the certificate with the port upon which Watson is listening.  

```
C:\> netsh http add sslcert ipport="0.0.0.0:443" certhash="[thumbprint]" appid="{00000000-0000-0000-0000-000000000000}" certstore=My
```

If you supply ```0.0.0.0``` it will allow the request to come in on any IP address.  Replace ```0.0.0.0:443``` with the IP and port you are using if necessary.  Replace ```[thumbprint]``` with the actual certificate thumbprint.  The appid value is arbitrary and any GUID will do.  


### Verify Certificate Installation

From the Command Prompt, type ```netsh http show sslcert``` to verify that your certificate is installed.  THe output will appear as follows:
```
C:\Users\Administrator>netsh http show sslcert

SSL Certificate bindings:
-------------------------

    IP:port                      : 0.0.0.0:443
    Certificate Hash             : d0013e91aab93f437a4443b13e6d18bd60f0279c
    Application ID               : {00112233-4455-6677-8899-aabbccddeeff}
    Certificate Store Name       : (null)
    Verify Client Certificate Revocation : Enabled
    Verify Revocation Using Cached Client Certificate Only : Disabled
    Usage Check                  : Enabled
    Revocation Freshness Time    : 0
    URL Retrieval Timeout        : 0
    Ctl Identifier               : (null)
    Ctl Store Name               : (null)
    DS Mapper Usage              : Disabled
    Negotiate Client Certificate : Disabled
```

### Check for Existing Bindings

If a binding exists for the port which you wish to use, you must first delete it.  Check your existing bindings using:

```
C:\Users\Administrator>netsh http show urlacl

URL Reservations:
-----------------
    Reserved URL            : https://host.domain.com:443/
        User: \Everyone
            Listen: Yes
            Delegate: No
            SDDL: D:(A;;GX;;;WD)
```

To delete a pre-existing binding, use:

```
C:\Users\Administrator> netsh http delete urlacl url=https://host.domain.com:443/
```

### Add the Binding

Now add the binding.  It is generally best to use a specific hostname in the URL rather than ```+```.  

```
C:\Users\Administrator> netsh http add urlacl url=https://host.domain.com:443/ user=everyone listen=yes
```

A helpful article on Stack Overflow related to this process can be found here: https://stackoverflow.com/questions/779228/the-parameter-is-incorrect-error-using-netsh-http-add-sslcert

If you see 503 errors:
https://stackoverflow.com/questions/26412602/httplistener-server-returns-an-error-503-server-unavailable
https://stackoverflow.com/questions/8142396/what-causes-a-httplistener-http-503-error

Also be sure to set your listener hostname when starting Watson to either the exact hostname used above, or ```+```
