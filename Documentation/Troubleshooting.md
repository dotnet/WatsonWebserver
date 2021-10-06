# Troubleshooting

## Connection Refused

1) Check to ensure you have instantiated and used ```.Start()``` on your server

2) From within the machine, verify that you can connect using ```localhost``` from cURL and a browser

3) Ensure the machine firewall is permitting connections on TCP ports for which Watson is listening

## Bad Hostname Error (400)

1) Ensure that your client is attempting to access Watson using **EXACTLY** one of the hostnames on which Watson is configured to listen (e.g. ```localhost``` and ```127.0.0.1``` are **not** equivalent from the HTTP HOST header perspective)

2) If Watson is configured to listen on any IP address (i.e. ```*```, ```+```, or ```0.0.0.0```), ensure you are running with administrative privileges

3) If running on Windows, ensure a binding exists in the operating system for Watson:

- Show existing bindings:
```
> netsh http show urlacl
URL Reservations:
-----------------
    Reserved URL            : http://localhost:8000/
        User: \Everyone
            Listen: Yes
            Delegate: No
            SDDL: D:(A;;GX;;;WD)
```

- Add a binding:
```
> netsh http add urlacl url=http://localhost:8000/ user=everyone listen=yes
```

## Logging

Watson has facilities to direct log messages to your code for consumption.  When you encounter a problem, it is best to enable logging, reproduce the problem, and capture the log data.
```csharp
server.Events.Logger = Console.WriteLine;
```

Similarly, Watson can marshal exception data to your code for consumption.  When you encounter a problem, it is best to enable exception events, reproduce the problem, and capture the exception data.
```csharp
server.Events.ExceptionEncountered += ExceptionEncountered;

static void ExceptionEncountered(object sender, ExceptionEventArgs args)
{
  Console.WriteLine("Exception: " + args.Json);
}
```

The ```ExceptionEventArgs``` object includes:

- The ```Exception```
- A ```Json``` string representation of the ```Exception```
- The ```Ip```, ```Port```, ```Method```, ```Query```, and ```Url``` of the requestor and request
