// .NET 10 file-based app: dotnet run --file scripts/ci/CrossMacroCI.cs -- <command> [options]
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property SelfContained=false
#:property PackAsTool=false
#:include CrossMacroCI/*.cs

return CrossMacro.CI.CICommandLine.Run(args);
