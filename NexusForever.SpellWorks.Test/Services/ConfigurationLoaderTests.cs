using Microsoft.Extensions.Configuration;
using NexusForever.SpellWorks.Services;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Reading <c>Configuration.json</c>. A bad one must not throw out of the <c>Application</c> constructor,
    /// which would kill the process before there is a window to say so in - no message, just an exit code.
    /// </summary>
    public class ConfigurationLoaderTests : IDisposable
    {
        private readonly string _directory =
            Directory.CreateTempSubdirectory("spellworks-configuration").FullName;

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private void Write(string content) =>
            File.WriteAllText(Path.Combine(_directory, ConfigurationLoader.FileName), content);

        [Fact]
        public void Reads_the_patch_path_out_of_a_good_file()
        {
            Write(@"{ ""PatchPath"": ""G:\\WildStar\\Patch"" }");

            (IConfiguration configuration, string error) = ConfigurationLoader.Load(_directory);

            Assert.Null(error);
            Assert.Equal(@"G:\WildStar\Patch", configuration["PatchPath"]);
        }

        [Fact]
        public void A_missing_file_is_a_first_run_not_a_failure()
        {
            (IConfiguration configuration, string error) = ConfigurationLoader.Load(_directory);

            Assert.Null(error);
            Assert.Null(configuration["PatchPath"]);
        }

        [Fact]
        public void A_path_written_with_single_backslashes_is_reported_rather_than_thrown()
        {
            // The slip anyone hand-editing this file makes: a Windows path is not valid JSON as typed.
            Write(@"{ ""PatchPath"": ""D:\WildStar\Patch"" }");

            (IConfiguration configuration, string error) = ConfigurationLoader.Load(_directory);

            Assert.NotNull(error);
            Assert.Null(configuration["PatchPath"]);
        }

        [Fact]
        public void The_message_names_the_file_the_reason_and_the_fix()
        {
            Write(@"{ ""PatchPath"": ""D:\WildStar\Patch"" }");

            (_, string error) = ConfigurationLoader.Load(_directory);

            Assert.Contains(ConfigurationLoader.FileName, error);
            Assert.Contains("backslashes doubled", error);

            // The innermost reason is the one that names what broke the parse; the wrappers say nothing.
            Assert.Contains("escapable character", error);
        }

        [Fact]
        public void Truncated_json_is_reported_too()
        {
            Write(@"{ ""PatchPath"": ");

            (_, string error) = ConfigurationLoader.Load(_directory);

            Assert.NotNull(error);
        }
    }
}
