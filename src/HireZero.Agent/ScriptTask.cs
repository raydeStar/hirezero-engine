using System.Text.Json;
using HireZero.Core;
using HireZero.Integrations.Inference;

namespace HireZero.Agent;

public sealed record ScriptOutcome(SandboxResult Result, SandboxRelease Release, string RunFolder);

public sealed class ScriptTask(InferenceClient model, ISandboxExecutor sandbox, string runsFolder)
{
    private sealed record ModelDraft(string Script);

    public async Task<SandboxRequest> DraftAsync(string goal, CancellationToken token)
    {
        var reply = await model.CompleteAsync(
            """
            Draft a self-contained Node.js ES module for this goal. It has no network, credentials, or input files.
            Use stdout for the result. Reply with just one JSON object containing a single string property "script".
            """,
            JsonSerializer.Serialize(new { goal }),
            "draft-script",
            8_000,
            token
        );
        // The model supplies code. The host alone names the job and sets its resource and file limits.
        var draft = CompanyJson.Decode<ModelDraft>(reply.Text);
        var packet = new SandboxRequest(
            Guid.NewGuid().ToString("N"),
            "demo",
            draft.Script,
            [],
            [],
            new SandboxLimits()
        );
        packet.Validate();
        return packet;
    }

    public async Task<ScriptOutcome> RunAsync(SandboxRequest draft, string approvedDigest, CancellationToken token)
    {
        // Approve and execute the same snapshot, even if a caller later mutates an input or output array.
        var snapshot = CompanyJson.Encode(draft);
        if (ContentGuard.Digest(snapshot) != approvedDigest)
            throw new WorkspaceException("The approval does not match this exact script packet.", 403);
        var packet = CompanyJson.Decode<SandboxRequest>(snapshot);
        packet.Validate();

        var folder = Path.Combine(runsFolder, "scripts", packet.JobId);
        if (Directory.Exists(folder))
            throw new WorkspaceException(
                "This script job already has a run folder. It cannot be dispatched again.",
                409
            );
        Directory.CreateDirectory(folder);
        SaveNew(
            folder,
            "packet.json",
            new
            {
                approvedDigest,
                runtime = sandbox.Identity,
                request = packet,
            }
        );
        var result = await sandbox.ExecuteAsync(packet, () => true, token);
        // A failed save leaves the engine's bounded log available for operator recovery.
        SaveNew(folder, "result.json", result);
        var release = result.Release.Confirmed
            ? result.Release
            : await sandbox.ReleaseAsync(
                packet.CompanyId,
                packet.JobId,
                result.CreationConfirmed,
                CancellationToken.None
            );
        SaveNew(folder, "release.json", release);
        return new(result, release, folder);
    }

    private static void SaveNew(string folder, string name, object value)
    {
        var path = Path.Combine(folder, name);
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new WorkspaceException("A record already exists for this script job. It cannot be overwritten.", 409);
        }
        using (file)
        {
            JsonSerializer.Serialize(file, value, CompanyJson.Options);
            file.Flush(flushToDisk: true);
        }
    }
}
