namespace Test.XUnit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using Test.Automated;

    /// <summary>
    /// Loads the shared automated coverage results generated during build.
    /// </summary>
    public static class AutomatedCoverageCache
    {
        private static readonly IReadOnlyList<AutomatedTestResult> _Results = LoadResults();

        /// <summary>
        /// Shared result set.
        /// </summary>
        public static IReadOnlyList<AutomatedTestResult> Results
        {
            get
            {
                return _Results;
            }
        }

        private static IReadOnlyList<AutomatedTestResult> LoadResults()
        {
            string resultsPath = FindResultsPath();

            if (!File.Exists(resultsPath))
            {
                throw new FileNotFoundException("Shared automated results were not generated during build. Attempted path: " + resultsPath, resultsPath);
            }

            string serializedResults = File.ReadAllText(resultsPath);
            List<AutomatedTestResult> results = JsonSerializer.Deserialize<List<AutomatedTestResult>>(serializedResults);

            if (results == null || results.Count < 1)
            {
                throw new InvalidOperationException("Shared automated results file was empty.");
            }

            return results;
        }

        private static string FindResultsPath()
        {
            string fileName = "shared-automated-results.json";
            AssemblyMetadataAttribute[] metadataAttributes =
                (AssemblyMetadataAttribute[])Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
            string[] candidateDirectories = new string[]
            {
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                AppContext.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (AssemblyMetadataAttribute attribute in metadataAttributes)
            {
                if (attribute != null
                    && String.Equals(attribute.Key, "SharedAutomatedResultsPath", StringComparison.Ordinal)
                    && !String.IsNullOrEmpty(attribute.Value))
                {
                    return attribute.Value;
                }
            }

            foreach (string directory in candidateDirectories)
            {
                if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    continue;
                }

                string candidatePath = Path.Combine(directory, fileName);

                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                string[] discoveredPaths = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);

                if (discoveredPaths.Length > 0)
                {
                    return discoveredPaths[0];
                }
            }

            return Path.Combine(AppContext.BaseDirectory, fileName);
        }
    }
}
