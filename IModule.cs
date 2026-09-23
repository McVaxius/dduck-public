using System;
using Dalamud.Plugin;

namespace DDuck.PublicShell;

// Stable ABI v1. Public and private release versions advance independently.
public interface IModule : IDisposable
{
    void Initialize(IDalamudPluginInterface pluginInterface, string hostVersion);
    void OpenMainWindow();
    void OnCommand(string command, string arguments);
    void Draw();
}
