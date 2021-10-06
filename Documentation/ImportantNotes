# Important Notes

If you only read one document, we ask that it be this one!

1) If you listen on anything other than ```localhost``` or ```127.0.0.1```, you may have to run as administrator or root in order to receive requests

2) The ```HOST``` header on incoming requests **absolutely must** match one of the listener bindings you use to instantiate Watson

3) Multiple bindings are supported in .NET Framework, but not yet in .NET Core

4) When using SSL, Watson uses the certificates installed in the operating system.  Refer to the Wiki for details

5) Routes are always evaluated in the following order: pre-routing, content routes, static routes, parameter routes, dynamic routes, and then the default route

6) For routes, the first match is always used, so define your routes most-specific first and least-specific last

7) Pre-routing should return either ```true``` (the connection should be terminated) or ```false``` (allow the connection to continue routing)

8) By dfeault Watson will **permit** all inbound connections.  Use ```server.AccessControl.DenyList``` to block certain IPs and networks, or change ```server.AccessControl.Mode``` to ```DefaultDeny``` and specify which IPs and networks to permit through ```server.AccessControl.PermitList```
