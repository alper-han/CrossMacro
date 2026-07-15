
namespace CrossMacro.Core.Logging;

public interface ICoreLogger
{
    public bool IsEnabled(CoreLogLevel level);

    public void Verbose(string messageTemplate, params object?[] propertyValues);
    public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues);

    public void Debug(string messageTemplate, params object?[] propertyValues);
    public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues);

    public void Information(string messageTemplate, params object?[] propertyValues);
    public void Information(Exception exception, string messageTemplate, params object?[] propertyValues);

    public void Warning(string messageTemplate, params object?[] propertyValues);
    public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues);

    public void LogError(string messageTemplate, params object?[] propertyValues);
    public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues);

    public void Fatal(string messageTemplate, params object?[] propertyValues);
    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues);
}
