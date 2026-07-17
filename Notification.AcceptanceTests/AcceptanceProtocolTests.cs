using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace Notification.AcceptanceTests;

public sealed class AcceptanceProtocolTests
{
    public static IEnumerable<object[]> AcceptanceCriteria()
    {
        using var document = LoadScenarioDocument();
        foreach (var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            yield return [scenario.GetProperty("criterion_id").GetString()!];
        }
    }

    [Theory]
    [MemberData(nameof(AcceptanceCriteria))]
    public async Task Approved_acceptance_criterion_is_observed_without_weakened_expectations(string criterionId)
    {
        using var scenarios = LoadScenarioDocument();
        var scenario = scenarios.RootElement.GetProperty("scenarios").EnumerateArray()
            .Single(item => item.GetProperty("criterion_id").GetString() == criterionId);

        using var actual = await ExternalAcceptanceDriver.RunAsync(criterionId);
        Assert.Equal(criterionId, actual.RootElement.GetProperty("criterion_id").GetString());
        Assert.False(actual.RootElement.TryGetProperty("accepted", out _),
            "The driver must return observations, not an implementation-decided pass flag.");
        Assert.False(actual.RootElement.TryGetProperty("passed", out _),
            "The driver must return observations, not an implementation-decided pass flag.");

        JsonSubsetAssert.EqualLeaves(
            scenario.GetProperty("expected_observations"),
            actual.RootElement.GetProperty("observations"),
            "$observations");
    }

    private static JsonDocument LoadScenarioDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "acceptance-scenarios.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}

internal static class ExternalAcceptanceDriver
{
    public static async Task<JsonDocument> RunAsync(string criterionId)
    {
        var driver = Required("NOTIFICATION_ACCEPTANCE_DRIVER");
        var baseUrl = Required("NOTIFICATION_BASE_URL");
        var fixture = Required("NOTIFICATION_FIXTURE_MANIFEST");

        var start = new ProcessStartInfo
        {
            FileName = driver,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("--criterion");
        start.ArgumentList.Add(criterionId);
        start.ArgumentList.Add("--base-url");
        start.ArgumentList.Add(baseUrl);
        start.ArgumentList.Add("--fixture-manifest");
        start.ArgumentList.Add(fixture);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The acceptance driver could not be started.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(process.ExitCode == 0,
            $"Acceptance driver failed for {criterionId} with exit code {process.ExitCode}: {stderr}");
        Assert.False(string.IsNullOrWhiteSpace(stdout),
            $"Acceptance driver returned no observation JSON for {criterionId}.");

        try
        {
            return JsonDocument.Parse(stdout);
        }
        catch (JsonException exception)
        {
            throw new XunitException(
                $"Acceptance driver output for {criterionId} was not JSON: {exception.Message}\n{stdout}");
        }
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new XunitException(
                $"{name} is required. These acceptance tests fail closed; see setup.md. " +
                "Do not replace the missing integrated Login/database/browser/DI environment with a bypass.");
        }

        return value;
    }
}

internal static class JsonSubsetAssert
{
    public static void EqualLeaves(JsonElement expected, JsonElement actual, string path)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in expected.EnumerateObject())
                {
                    Assert.True(actual.TryGetProperty(property.Name, out var actualProperty),
                        $"Missing required observation {path}.{property.Name}.");
                    EqualLeaves(property.Value, actualProperty, $"{path}.{property.Name}");
                }
                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                Assert.Equal(expectedItems.Length, actualItems.Length);
                for (var index = 0; index < expectedItems.Length; index++)
                {
                    EqualLeaves(expectedItems[index], actualItems[index], $"{path}[{index}]");
                }
                break;
            case JsonValueKind.String:
                Assert.True(expected.GetString() == actual.GetString(),
                    $"Expected {path} = {expected.GetRawText()}, actual {actual.GetRawText()}.");
                break;
            case JsonValueKind.Number:
                Assert.True(expected.GetRawText() == actual.GetRawText(),
                    $"Expected {path} = {expected.GetRawText()}, actual {actual.GetRawText()}.");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;
            case JsonValueKind.Null:
                Assert.Equal(JsonValueKind.Null, actual.ValueKind);
                break;
            default:
                throw new XunitException($"Unsupported JSON kind at {path}: {expected.ValueKind}.");
        }
    }
}
