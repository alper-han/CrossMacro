namespace CrossMacro.Cli.Services.Doctor;

internal interface IDoctorProbeGroup
{
    public IReadOnlyList<DoctorCheck> Run(bool verbose);
}
