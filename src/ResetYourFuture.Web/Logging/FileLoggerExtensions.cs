namespace ResetYourFuture.Web.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logDirectory = "Logs")
    {
        builder.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider(logDirectory));
        return builder;
    }
}
