using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro file operations
/// </summary>
public interface IMacroFileManager
{
    /// <summary>
    /// Saves a macro sequence to a file
    /// </summary>
    Task SaveAsync(MacroSequence macro, string filePath);
    
    /// <summary>
    /// Loads a macro sequence from a file
    /// </summary>
    Task<MacroSequence?> LoadAsync(string filePath);
}
