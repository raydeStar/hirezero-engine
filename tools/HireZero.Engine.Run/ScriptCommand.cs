using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;
using HireZero.Integrations.Sandbox;

internal static class ScriptCommand
{
    public static async Task<int> RunAsync(
        string goal,
        string runs,
        InferenceClient model,
        Func<string, string> approvalFor
    )
    {
        if (Environment.GetEnvironmentVariable("HIREZERO_SANDBOX_ENABLED") != "1")
        {
            Console.Error.WriteLine(
                "Script execution is disabled. An operator must qualify and enable the runtime first."
            );
            return 2;
        }
        const string endpoint = "unix:///var/run/docker.sock";
        var docker = Environment.GetEnvironmentVariable("HIREZERO_SANDBOX_DOCKER") ?? "/usr/bin/docker";
        var path =
            Environment.GetEnvironmentVariable("HIREZERO_SANDBOX_QUALIFICATION")
            ?? "/opt/hirezero-qualification/receipts/sandbox-qualification.json";
        var receipt = SandboxQualification.Read(path);
        if (receipt is null || receipt.Endpoint != endpoint)
        {
            Console.Error.WriteLine($"A script qualification receipt for {endpoint} is required at {path}.");
            return 2;
        }
        try
        {
            var sandbox = new QualifiedDockerSandbox(
                new DockerCommands(docker, endpoint),
                receipt,
                () => SandboxQualification.Read(path),
                () => SandboxQualification.ExecutableHash(docker),
                enabled: true
            );
            await sandbox.RefreshAsync(CancellationToken.None);
            if (!sandbox.Availability.Available)
            {
                Console.Error.WriteLine($"Script runtime unavailable ({sandbox.Availability.Status}).");
                return 2;
            }
            var task = new ScriptTask(model, sandbox, runs);
            var packet = await task.DraftAsync(goal, CancellationToken.None);
            // JSON escaping keeps terminal control sequences in the script from repainting the approval prompt.
            Console.WriteLine($"Script packet (JSON): {CompanyJson.Encode(packet)}");
            var digest = CompanyJson.Digest(packet);
            Console.WriteLine($"Packet digest: {digest}");
            var approved = approvalFor(digest);
            if (approved != digest)
            {
                Console.WriteLine("Script packet declined.");
                return 1;
            }
            var outcome = await task.RunAsync(packet, approved, CancellationToken.None);
            Console.WriteLine(
                $"Script: {outcome.Result.State}; stdout: {JsonSerializer.Serialize(outcome.Result.Stdout)}"
            );
            Console.WriteLine($"Teardown: {outcome.Release.Status}; records: {outcome.RunFolder}");
            return outcome.Result.State == "completed" && outcome.Release.Confirmed ? 0 : 1;
        }
        catch (Exception e)
            when (e
                    is ArgumentException
                        or JsonException
                        or WorkspaceException
                        or InferenceRefusedException
                        or InferenceUncertainException
                        or InferenceBudgetException
                        or IOException
                        or UnauthorizedAccessException
            )
        {
            Console.Error.WriteLine($"Script task stopped: {e.Message}");
            return 1;
        }
    }
}
