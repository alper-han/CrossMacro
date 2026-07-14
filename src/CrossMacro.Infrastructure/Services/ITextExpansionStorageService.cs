using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Application.Automation;

namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService : ITextExpansionStore
{
    List<Core.Models.TextExpansion> Load();
    List<Core.Models.TextExpansion> GetCurrent();
    string FilePath { get; }
}
