using Onec.DebugAdapter.DebugServer;
using System.Runtime.CompilerServices;

namespace Onec.DebugAdapter.Extensions
{
    internal static class DebugTargetIdExtensions
    {
        internal static DebugTargetIdLight ToLight(this DebugTargetId debugTarget)
            => new()
            {
                Id = debugTarget.Id,
            };

        internal static string GetUserName(this DebugTargetId debugTargetId)
            => string.IsNullOrEmpty(debugTargetId.UserName) ? "Неизвестный пользователь" : debugTargetId.UserName;
    }

    internal static class DebugTargetTypeExtensions
    {
        internal static string GetTypePresentation(this DebugTargetType type)
            => type switch
            {
                DebugTargetType.Unknown => "Неизвестный тип",
                DebugTargetType.Client => "Толстый клиент",
                DebugTargetType.ManagedClient => "Тонкий клиент",
                DebugTargetType.WebClient => "Веб-клиент",
                DebugTargetType.ComConnector => "COM-соединение",
                DebugTargetType.Server => "Сервер",
                DebugTargetType.ServerEmulation => "Сервер (файловый вариант)",
                DebugTargetType.WebService => "Веб-сервис",
                DebugTargetType.HttpService => "Http-сервис",
                DebugTargetType.OData => "Стандартный интерфейс OData",
                DebugTargetType.Job => "Фоновое задание",
                DebugTargetType.JobFileMode => "Фоновое задание (файловый вариант)",
                DebugTargetType.MobileClient => "Клиент (мобильное приложение)",
                DebugTargetType.MobileServer => "Сервер (мобильное приложение)",
                DebugTargetType.MobileJobFileMode => "Фоновое задание (мобильное приложение)",
                DebugTargetType.MobileManagedClient => "Мобильный клиент",
                DebugTargetType.MobileManagedServer => "Автономный сервер (мобильный клиент с автономным режимом)",
                _ => type.ToString(),
            };
    }
}
