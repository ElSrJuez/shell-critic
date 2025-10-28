using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;

namespace ConsoleCritic.Provider;

// Registers/unregisters the provider with PowerShell subsystems
public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    // Replace with a stable GUID for your provider (New-Guid)
    private const string Id = "d1705641-3bdd-4088-8673-8a119c83c101";

    private static readonly Logging.CriticLogger Logger = new();

    public void OnImport()
    {
        // Log initialization
        ConsoleCritic.Provider.Logging.CriticLog.Event("INIT");

        var provider = new CriticFeedbackProvider(Id);
        SubsystemManager.RegisterSubsystem(SubsystemKind.FeedbackProvider, provider);
        ConsoleCritic.Provider.Logging.CriticLog.Event("Provider registered");
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(new Guid(Id));
        ConsoleCritic.Provider.Logging.CriticLog.Event("Provider unregistered");
    }
}