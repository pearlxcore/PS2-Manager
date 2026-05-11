public static class Ps2IdHelper
{
	/// <summary>
	/// Converts PS2 boot IDs to canonical format:
	/// SLUS_200.03 → SLUS-20003
	/// SLES_123.45 → SLES-12345
	/// ALCH-00001  → ALCH-00001 (unchanged)
	/// </summary>
	public static string Normalize(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return "Unknown";

		// If this is unknown-like input, return the proper casing
		if (id.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
			return "Unknown";

		// Continue normalizing real GameIDs
		id = id.Trim().ToUpperInvariant();

		id = id.Replace('_', '-');   // convert underscores to hyphens
		id = id.Replace(".", "");    // remove dots (SLUS_203.12 → SLUS-20312)

		return id;
	}

}
