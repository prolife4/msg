using System.Net;
using System.Net.Security;
using Vmx.Common.Settings.Services;

namespace Vmx.Messenger.Services.MAX
{
    internal static class MaxHttpClientFactory
    {
        private static readonly object Sync = new();
        private static SocketsHttpHandler? _handler;

        public static HttpClient Create()
        {
            var inner = GetOrCreateHandler();
           // var handler = new MaxLoggingHandler(inner);

            return new HttpClient(inner, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(GetTimeoutSeconds())
            };
        }

        private static SocketsHttpHandler GetOrCreateHandler()
        {
            if (_handler is not null)
                return _handler;

            lock (Sync)
            {
                if (_handler is not null)
                    return _handler;

                var disableValidation =
                    VmxSettings.Instance.AppProjectSettings.Messenger?.Max?.DisableCertificateValidation ?? false;

                var sslOptions = new SslClientAuthenticationOptions();
                if (disableValidation)
                {
                    // осознанно отключаем — только для dev/test
                    sslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
                }
                // иначе колбэк не задаём — работает стандартная валидация ОС

                _handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    SslOptions = sslOptions
                };
            }

            return _handler;
        }

        private static int GetTimeoutSeconds() =>
            VmxSettings.Instance.AppProjectSettings.Messenger?.Max?.TimeoutSeconds is > 0 and var t
                ? t
                : 100;
    }
}