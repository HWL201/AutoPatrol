namespace AutoPatrol.Services
{
    public interface ITimerService
    {
        Task ExecuteScheduledTaskAsync();

        Task ExecuteFileCopyTaskAsync();
    }
}
