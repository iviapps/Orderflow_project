using System.Diagnostics;

namespace Orderflow.ServiceDefaults;

/// <summary>
/// Clase helper para crear trazas (spans) personalizadas en OpenTelemetry.
/// Úsala para rastrear operaciones críticas en tus servicios.
/// </summary>
public static class OrderflowActivitySource
{
    private static readonly ActivitySource Source = new("Orderflow.Custom");

    /// <summary>
    /// Crea una actividad (span) para rastrear una operación.
    /// </summary>
    /// <param name="operationName">Nombre de la operación (ej: "Register User", "Create Order")</param>
    /// <param name="kind">Tipo de actividad (Internal, Server, Client, Producer, Consumer)</param>
    /// <returns>Activity que debes usar con 'using' para asegurar que se complete</returns>
    /// <example>
    /// using var activity = OrderflowActivitySource.StartActivity("Register User");
    /// activity?.SetTag("user.email", email);
    /// // tu código aquí
    /// </example>
    public static Activity? StartActivity(
        string operationName,
        ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(operationName, kind);
    }

    /// <summary>
    /// Agrega tags (metadatos) a la actividad actual.
    /// </summary>
    public static void AddTag(string key, object? value)
    {
        Activity.Current?.SetTag(key, value);
    }

    /// <summary>
    /// Registra un evento en la actividad actual.
    /// </summary>
    public static void AddEvent(string eventName, params (string Key, object? Value)[] tags)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            var activityEvent = new ActivityEvent(eventName);
            foreach (var (key, value) in tags)
            {
                activityEvent = new ActivityEvent(
                    eventName,
                    tags: new ActivityTagsCollection { { key, value } }
                );
            }
            activity.AddEvent(activityEvent);
        }
    }

    /// <summary>
    /// Marca la actividad actual como fallida.
    /// </summary>
    public static void RecordException(Exception exception)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.RecordException(exception);
        }
    }
}
