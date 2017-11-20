using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;

namespace EFCore.Practices
{
    public class SelectPlusOneLoggerProvider 
        : ILoggerProvider
    {
        SelectPlusOneLogger _logger = new SelectPlusOneLogger();
        private int _treshold;
        private bool _verified;

        
        public SelectPlusOneLoggerProvider(int treshold)
        {
            _treshold = treshold;
        }

        public SelectPlusOneLoggerProvider(DbContext context, int treshold = 20)
            : this(treshold)
        {
            context
                .Database
                .GetService<ILoggerFactory>()
                .AddProvider(this);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
            if (!_verified)
            {
                try
                {
                    Verify();
                }
                catch (AggregateException ex)
                {
                    throw new NotVerifiedException("Not verified therefor throwing in Dispose.", ex);
                }

            }
        }

        public void Verify()
        {
            _verified = true;
            _logger.ThrowIfCommandsExceedTreshold(_treshold);
        }

        private class SelectPlusOneLogger : ILogger
        {
            private ConcurrentDictionary<string, int> commands = new ConcurrentDictionary<string, int>();

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter != null)
                {
                    var message = formatter(state, exception);
                    var count = commands.AddOrUpdate(message, 1, (key, old) => old + 1);
                }
            }

            public void ThrowIfCommandsExceedTreshold(int treshold)
            {
                var ex = commands.Where(kv => kv.Value >= treshold).Select(kv => new PossibleSlectPlusOneQueryException(kv.Key, kv.Value));
                if (ex.Any())
                {
                    throw new AggregateException(ex);
                }
            }
        }
    }
}
