using NuGet.Versioning;

namespace UpdatePackageReferences;

public static class StringExtensions {

	public static NuGetVersion? ToSemVer(this string? version)
		=> version == null ? null : NuGetVersion.Parse(version);

}