namespace ServerMessengerHttp
{
    public class Worker : BackgroundService
    {
#pragma warning disable CS1998
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
#pragma warning restore CS1998
        {
            Server.StartServer();
        }
    }
}
