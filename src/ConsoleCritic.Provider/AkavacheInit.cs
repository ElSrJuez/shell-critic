using Akavache;              // ← REQUIRED for the builder extension methods
using Akavache.Core;
using Akavache.SystemTextJson;
using Akavache.Sqlite3;
using Splat;
using Splat.Builder;

namespace ConsoleCritic.Provider
{
    public static class AkavacheInit
    {
        public static void Initialize()
        {
            Splat.Builder.AppBuilder
                .CreateSplatBuilder()
                .WithAkavacheCacheDatabase<SystemJsonSerializer>(builder =>
                    builder
                        .WithApplicationName("ConsoleCritic")
                        .WithSqliteProvider()
                        .WithSqliteDefaults());
        }
    }
}
