using Baubit.DI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Baubit.Caching.LiteDB.DI.Test.Module.Setup.Logging
{
    /// <summary>
    /// Module that registers <see cref="ILoggerFactory"/> for dependency injection.
    /// </summary>
    public class Module : AModule<Configuration>
    {
        public Module(IConfiguration configuration) : base(configuration)
        {
        }

        public Module(Configuration configuration, List<IModule>? nestedModules = null) : base(configuration, nestedModules)
        {
        }

        public override void Load(IServiceCollection services)
        {
            services.AddLogging();
            base.Load(services);
        }
    }
}
