namespace Soenneker.Libvips.Runners.Linux.Tests;

public sealed class LibvipsLinuxRunnerTests
{
    [Test]
    public void Targets_linux_x64_library()
    {
        if (Constants.Library != "Soenneker.Libvips.Linux" || Constants.RuntimeIdentifier != "linux-x64")
            throw new System.InvalidOperationException("The Linux runner target is not configured correctly.");
    }
}
