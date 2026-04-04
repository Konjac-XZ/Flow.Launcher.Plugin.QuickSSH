using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Represents a registered SSH key alias pointing to a local key file path.
    /// Only the alias and path are stored — never the private key content.
    /// </summary>
    public class SshKeyEntry
    {
        /// <summary>Path to the private key file on disk.</summary>
        [JsonProperty]
        public string Path { get; set; }

        /// <summary>Optional human-readable description.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>Returns a display-friendly summary of this key entry.</summary>
        public string ToDisplayString()
        {
            if (!string.IsNullOrEmpty(Description))
                return Description + " — " + Path;
            return Path ?? "";
        }
    }
}
