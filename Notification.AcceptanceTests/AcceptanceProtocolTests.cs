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

    public static IEnumerable<object[]> TargetedChangeCriteria() => SuiteCriteria("targeted_change");

    public static IEnumerable<object[]> ImpactedRegressionCriteria() => SuiteCriteria("impacted_regression");

    [Theory]
    [MemberData(nameof(AcceptanceCriteria))]
    [Trait("Suite", "FullProjectRegression")]
    public async Task Approved_acceptance_criterion_is_observed_without_weakened_expectations(string criterionId)
        => await AssertCriterionAsync(criterionId);

    [Theory]
    [MemberData(nameof(TargetedChangeCriteria))]
    [Trait("Suite", "TargetedChange")]
    public async Task Changed_acceptance_criterion_is_observed_without_weakened_expectations(string criterionId)
        => await AssertCriterionAsync(criterionId);

    [Theory]
    [MemberData(nameof(ImpactedRegressionCriteria))]
    [Trait("Suite", "ImpactedRegression")]
    public async Task Impacted_area_regression_is_observed_without_weakened_expectations(string criterionId)
        => await AssertCriterionAsync(criterionId);

    private static async Task AssertCriterionAsync(string criterionId)
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

    private static IEnumerable<object[]> SuiteCriteria(string suiteName)
    {
        using var document = LoadScenarioDocument();
        foreach (var criterion in document.RootElement.GetProperty("test_suites").GetProperty(suiteName).EnumerateArray())
        {
            yield return [criterion.GetString()!];
        }
    }

    private static JsonDocument LoadScenarioDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "acceptance-scenarios.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}

public sealed class ScenarioContractTests
{
    [Fact]
    [Trait("Suite", "Protocol")]
    public void Scenario_manifest_targets_approved_versions_and_has_complete_suite_membership()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "acceptance-scenarios.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal(3, root.GetProperty("requirement_version").GetInt32());
        Assert.Equal(3, root.GetProperty("architecture_version").GetInt32());

        var scenarioIds = root.GetProperty("scenarios").EnumerateArray()
            .Select(item => item.GetProperty("criterion_id").GetString()!)
            .ToArray();
        Assert.Equal(27, scenarioIds.Length);
        Assert.Equal(scenarioIds.Length, scenarioIds.Distinct(StringComparer.Ordinal).Count());

        var suites = root.GetProperty("test_suites");
        var fullProjectIds = suites.GetProperty("full_project_regression").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Equal(scenarioIds.Order(StringComparer.Ordinal), fullProjectIds.Order(StringComparer.Ordinal));

        var changedIds = new HashSet<string>(
        [
            "AC-AUTH-001", "AC-AUTH-002", "AC-AUTH-003", "AC-AUTH-004", "AC-BOUNDARY-001",
            "AC-AUDIT-001", "AC-ERROR-001", "AC-NFR-001", "AC-NFR-003", "AC-NFR-004"
        ], StringComparer.Ordinal);
        var targetedIds = suites.GetProperty("targeted_change").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(changedIds.SetEquals(targetedIds));

        var impactedIds = suites.GetProperty("impacted_regression").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(13, impactedIds.Count);
        Assert.True(changedIds.IsSubsetOf(impactedIds));
    }

    [Fact]
    [Trait("Suite", "Protocol")]
    public void Every_scenario_has_a_unique_test_id_and_expected_observations()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "acceptance-scenarios.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var scenarios = document.RootElement.GetProperty("scenarios").EnumerateArray().ToArray();
        var testIds = scenarios.Select(item => item.GetProperty("test_id").GetString()!).ToArray();

        Assert.Equal(testIds.Length, testIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(scenarios, scenario =>
        {
            Assert.False(string.IsNullOrWhiteSpace(scenario.GetProperty("criterion_id").GetString()));
            Assert.Equal(JsonValueKind.Object, scenario.GetProperty("expected_observations").ValueKind);
        });
    }

    [Fact]
    [Trait("Suite", "Protocol")]
    public void Fixture_contract_covers_identity_modes_startup_boundaries_and_safe_codes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "fixture-contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var modeCases = root.GetProperty("required_identity_mode_scenarios").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("development_temporary_valid_unique_admin", modeCases);
        Assert.Contains("development_temporary_non_development_environment", modeCases);
        Assert.Contains("development_temporary_admin_not_found", modeCases);
        Assert.Contains("development_temporary_admin_not_unique", modeCases);
        Assert.Contains("development_temporary_admin_wrong_role", modeCases);
        Assert.Contains("development_temporary_admin_non_normal", modeCases);
        Assert.Contains("development_temporary_admin_inactive", modeCases);
        Assert.Contains("development_temporary_admin_locked", modeCases);
        Assert.Contains("development_temporary_admin_deleted", modeCases);
        Assert.Contains("development_temporary_eligibility_changes_after_startup", modeCases);
        Assert.Contains("account_login_failure_without_temporary_fallback", modeCases);
        Assert.Contains("identity_mode_missing", modeCases);
        Assert.Contains("identity_mode_invalid", modeCases);

        var expectedCodes = new HashSet<string>(
        [
            "NID-MODE-MISSING", "NID-MODE-INVALID", "NID-TEMP-NONDEVELOPMENT",
            "NID-TEMP-ADMIN-NOT-FOUND", "NID-TEMP-ADMIN-NOT-UNIQUE", "NID-TEMP-ADMIN-INELIGIBLE"
        ], StringComparer.Ordinal);
        var actualCodes = root.GetProperty("startup_safe_codes").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(expectedCodes.SetEquals(actualCodes));

        var forbidden = root.GetProperty("sensitive_values_forbidden").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("password", forbidden);
        Assert.Contains("password_hash", forbidden);
        Assert.Contains("cookie", forbidden);
        Assert.Contains("token", forbidden);
        Assert.Contains("secret", forbidden);
        Assert.Contains("connection_string", forbidden);
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
