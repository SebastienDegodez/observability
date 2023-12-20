using System.Diagnostics;
using System.Reflection;

namespace WeatherApi.OpenTelemetry
{
    public static class Instrumentation
    {
        /// <summary>
        /// The assembly name.
        /// </summary>
        public static readonly AssemblyName AssemblyName = typeof(Instrumentation).Assembly.GetName();

        /// <summary>
        /// The activity source name.
        /// </summary>
        public static readonly string ActivitySourceName = AssemblyName.Name!;

        /// <summary>
        /// The version.
        /// </summary>
        public static readonly Version Version = AssemblyName.Version!;

        /// <summary>
        /// The activity source.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
    }
}
