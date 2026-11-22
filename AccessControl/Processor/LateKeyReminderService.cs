namespace AccessControl.Processor
{
    /// <summary>
    /// Every few minutes, reminds the Keyman (and texts the worker) about keys still out
    /// after their return-by time. Each late key is reminded about once.
    /// Settings (optional): LateKeyReminders:Enabled (default true),
    /// LateKeyReminders:IntervalMinutes (default 5, 1 to 1440).
    /// </summary>
    public sealed class LateKeyReminderService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LateKeyReminderService> _log;

        public LateKeyReminderService(IConfiguration config, IWebHostEnvironment env, ILogger<LateKeyReminderService> log)
        {
            _config = config;
            _env = env;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool enabled;
            int minutes;
            try
            {
                enabled = _config.GetValue("LateKeyReminders:Enabled", true);
                minutes = Math.Clamp(_config.GetValue("LateKeyReminders:IntervalMinutes", 5), 1, 1440);
            }
            catch (Exception ex)
            {
                // A mistyped setting must not take the API down: fall back to the defaults.
                _log.LogWarning(ex, "LateKeyReminders settings are invalid; using the defaults.");
                enabled = true;
                minutes = 5;
            }

            if (!enabled)
            {
                _log.LogInformation("Late-key reminders are switched off.");
                return;
            }

            // Let the API finish starting before the first check.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using PeriodicTimer timer = new(TimeSpan.FromMinutes(minutes));
            do
            {
                try
                {
                    int reminded = await new AccessProcessor(_config, _env).RemindLateKeysAsync();
                    if (reminded > 0)
                    {
                        _log.LogInformation("Sent late-key reminders for {Count} key(s).", reminded);
                    }
                }
                catch (Exception ex)
                {
                    // Never let one bad run stop the reminders (or the API).
                    _log.LogError(ex, "Late-key reminder run failed.");
                }
            }
            while (await WaitForNextTick(timer, stoppingToken));
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
