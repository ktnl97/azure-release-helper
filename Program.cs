using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace azure
{
    internal static class Program
    {
        private static async Task Main()
        {
            var connection = GetAzureConnection();
            Console.WriteLine("Established azure connection successfully. Starting to fetch and analyse commits...");
            var releaseReportGenerator = new ReleaseReport(connection);
            await releaseReportGenerator.ComputeChangeSet();
            Console.WriteLine($"Commit diff report generated successfully at {Config.ReportPath}");
        }

        private static VssConnection GetAzureConnection()
        {
            var orgUrl = new Uri(Config.AzureDevopsUrl);
            return new VssConnection(orgUrl, new VssBasicCredential(string.Empty, Config.Pat));
        }
    }
}