using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

internal sealed class DesignKeyCodeMapper : IKeyCodeMapper
{
    public string GetKeyName(int keyCode) => $"Key{keyCode}";

    public int GetKeyCode(string keyName) => 0;

    public bool IsModifierKeyCode(int code) => false;

    public int GetKeyCodeForCharacter(char character) => character;

    public bool RequiresShift(char character) => char.IsUpper(character);

    public bool RequiresAltGr(char character) => false;

    public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => (char)keyCode;
}
