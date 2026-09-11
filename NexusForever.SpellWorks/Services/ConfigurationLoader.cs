using System.IO;
using Microsoft.Extensions.Configuration;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Reads <c>Configuration.json</c> without letting a bad one, stop the app from starting.
    /// </summary>
    public static class ConfigurationLoader
    {
        public const string FileName = "Configuration.json";

        /// <summary>
        /// The configuration in <paramref name="baseDirectory"/>, and a message when it could not be read.
        /// A missing file is not an error - it is a first run.
        /// </summary>
        public static (IConfiguration Configuration, string Error) Load(string baseDirectory, string fileName = FileName)
        {
            try
            {
                return (new ConfigurationBuilder()
                    .SetBasePath(baseDirectory)
                    .AddJsonFile(fileName, optional: true)
                    .Build(), null);
            }
            catch (Exception exception)
            {
                return (new ConfigurationBuilder().Build(), Describe(exception, Path.Combine(baseDirectory, fileName)));
            }
        }

        /// <summary>The innermost reason, which is the one that names the character that broke the parse.</summary>
        private static string Describe(Exception exception, string path)
        {
            Exception reason = exception;
            while (reason.InnerException != null)
                reason = reason.InnerException;

            return $"{path} could not be read and was ignored — {reason.Message} "
                 + "A Windows path in JSON needs its backslashes doubled.";
        }
    }
}
