namespace WatsonWebserver.Infrastructures
{
    public interface IHostBuilder<HostBuilder, InputAction>
    {
        public HostBuilder MapStaticRoute(WatsonWebserver.HttpMethod methid, InputAction action, string routePath = "/home");
        public HostBuilder MapParameteRoute(WatsonWebserver.HttpMethod methid, InputAction action, string routePath = "/home");
        public HostBuilder MapDynamicRoute(WatsonWebserver.HttpMethod methid, InputAction action, Regex rx);
    }
}
